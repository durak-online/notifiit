using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;
using System.Globalization;
using NotiFIITBot.Consts;
using NotiFIITBot.Repo;
using Serilog;

namespace NotiFIITBot.Database.Repositories
{
    public class ScheduleRepository : IScheduleRepository
    {
        private readonly ScheduleDbContext _context;

        public ScheduleRepository(ScheduleDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<Lesson> lessons,
            CancellationToken ct = default)
        {
            var result = new List<LessonModel>();
            // Начинаем транзакцию для атомарного пакетного обновления
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            try
            {
                foreach (var lesson in lessons)
                {
                    var lm = await UpsertLessonAsync(lesson, ct);
                    result.Add(lm);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }

            return result;
        }

        public async Task<LessonModel> UpsertLessonAsync(Lesson lesson, CancellationToken ct = default)
        {
            if (lesson == null) throw new ArgumentNullException(nameof(lesson));

            // ----------------------------
            // 1) вычисляем итоговый parity на основе ParityList (с логированием)
            // ----------------------------
            Evenness newParity;

            if (lesson.ParityList != null && lesson.ParityList.Any())
            {
                var distinct = lesson.ParityList.Distinct().ToList();
                Serilog.Log.Information("ParityList: [{Parities}]", string.Join(",", distinct));

                if (distinct.Contains(0) && distinct.Contains(1))
                {
                    Serilog.Log.Information("→ Устанавливаем Evenness.Always (2)");
                    newParity = Evenness.Always;
                }
                else if (distinct.Contains(0))
                {
                    Serilog.Log.Information("→ Чётная неделя (0)");
                    newParity = Evenness.Even;
                }
                else
                {
                    Serilog.Log.Information("→ Нечётная неделя (1)");
                    newParity = Evenness.Odd;
                }
            }
            else
            {
                Serilog.Log.Information("ParityList пуст, используем EvennessOfWeek = {Evenness}", lesson.EvennessOfWeek);
                newParity = lesson.EvennessOfWeek switch
                {
                    Evenness.Even => Evenness.Even,
                    Evenness.Odd => Evenness.Odd,
                    Evenness.Always => Evenness.Always,
                    _ => Evenness.Even
                };
            }

            // ----------------------------
            // 2) Если newParity = Always — создаём запись сразу, не пытаясь сливать старые
            // ----------------------------
            if (newParity == Evenness.Always)
            {
                Serilog.Log.Debug("Добавляем запись с Evenness.Always для {Subject} день={Day} пара={Pair}",
                    lesson.SubjectName, lesson.DayOfWeek, lesson.PairNumber);

                var newLessonModelAlways = new LessonModel
                {
                    Evenness = Evenness.Always,
                    DayOfWeek = lesson.DayOfWeek ?? DayOfWeek.Monday,
                    PairNumber = lesson.PairNumber ?? -1,
                    SubjectName = lesson.SubjectName?.Trim(),
                    TeacherName = lesson.TeacherName?.Trim(),
                    ClassroomNumber = ParseClassroomNumber(lesson.ClassRoom),
                    ClassroomRoute = MapAuditoryLocationToRoute(lesson.AuditoryLocation)
                };

                await _context.Lessons.AddAsync(newLessonModelAlways, ct);
                return newLessonModelAlways;
            }

            // ----------------------------
            // 3) Для Even / Odd — ищем существующие записи и обновляем или создаём
            // ----------------------------
            var existingLessons = await _context.Lessons
                .Where(l =>
                    l.DayOfWeek == lesson.DayOfWeek &&
                    l.PairNumber == lesson.PairNumber &&
                    EF.Functions.ILike(l.SubjectName ?? "", lesson.SubjectName ?? ""))
                .ToListAsync(ct);

            // Если запись с таким parity уже есть — обновляем её и возвращаем
            var existingSameParity = existingLessons.FirstOrDefault(l => l.Evenness == newParity);
            if (existingSameParity != null)
            {
                Serilog.Log.Debug("Обновляем существующую запись parity={Parity} для {Subject} день={Day} пара={Pair}",
                    (int)existingSameParity.Evenness, existingSameParity.SubjectName, existingSameParity.DayOfWeek, existingSameParity.PairNumber);

                existingSameParity.TeacherName = lesson.TeacherName?.Trim() ?? existingSameParity.TeacherName;
                existingSameParity.ClassroomNumber = ParseClassroomNumber(lesson.ClassRoom) ?? existingSameParity.ClassroomNumber;
                existingSameParity.ClassroomRoute = MapAuditoryLocationToRoute(lesson.AuditoryLocation) ?? existingSameParity.ClassroomRoute;

                return existingSameParity;
            }

            // Создаём новую запись для Even или Odd
            var newEvenOddLesson = new LessonModel
            {
                Evenness = newParity,
                DayOfWeek = lesson.DayOfWeek ?? DayOfWeek.Monday,
                PairNumber = lesson.PairNumber ?? -1,
                SubjectName = lesson.SubjectName?.Trim(),
                TeacherName = lesson.TeacherName?.Trim(),
                ClassroomNumber = ParseClassroomNumber(lesson.ClassRoom),
                ClassroomRoute = MapAuditoryLocationToRoute(lesson.AuditoryLocation)
            };

            Serilog.Log.Debug("Добавляем новую запись parity={Parity} для {Subject} день={Day} пара={Pair}",
                (int)newEvenOddLesson.Evenness, newEvenOddLesson.SubjectName, newEvenOddLesson.DayOfWeek, newEvenOddLesson.PairNumber);

            await _context.Lessons.AddAsync(newEvenOddLesson, ct);
            return newEvenOddLesson;
        }

        // --- Вспомогательные методы ---

        /// <summary>
        /// Пытается извлечь число аудитории из строки (например "123" или "к.123" или "123/1").
        /// Возвращает null, если не получилось.
        /// </summary>
        private int? ParseClassroomNumber(string? classRoom)
        {
            if (string.IsNullOrWhiteSpace(classRoom)) return null;
            // находим первое подряд идущее число длиной 1-4
            var m = System.Text.RegularExpressions.Regex.Match(classRoom, @"\d{1,4}");
            if (!m.Success) return null;
            if (int.TryParse(m.Value, out var n)) return n;
            return null;
        }

        /// <summary>
        /// Простая логика для преобразования AuditoryLocation -> ClassroomRoute (если нужно).
        /// Пока возвращает AuditoryLocation как есть; можно расширить.
        /// </summary>
        private string? MapAuditoryLocationToRoute(string? auditoryLocation)
        {
            if (string.IsNullOrWhiteSpace(auditoryLocation)) return null;
            return auditoryLocation.Trim();
        }
    }
}
