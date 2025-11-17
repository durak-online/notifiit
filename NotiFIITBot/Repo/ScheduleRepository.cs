using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;
using System.Globalization;
using NotiFIITBot.Repo;

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

            // Маппинг доменной сущности -> модель БД (подготовка значений)
            var parity = (NotiFIITBot.Consts.Evenness)lesson.EvennessOfWeek;
            var dayOfWeek = lesson.DayOfWeek ?? DayOfWeek.Monday; // если null — по умолчанию
            var pairNumber = lesson.PairNumber ?? -1;
            var subject = lesson.SubjectName?.Trim();
            var teacher = lesson.TeacherName?.Trim();

            // Попытка найти существующую запись по совпадающим ключам
            // Поиск с учётом нормализации - регистронезависимо
            var existing = await _context.Lessons
                .FirstOrDefaultAsync(l =>
                        l.Parity == parity &&
                        l.DayOfWeek == dayOfWeek &&
                        l.PairNumber == pairNumber &&
                        EF.Functions.ILike(l.SubjectName ?? string.Empty, subject ?? string.Empty) &&
                        (string.IsNullOrEmpty(teacher) ||
                         EF.Functions.ILike(l.TeacherName ?? string.Empty, teacher ?? string.Empty)),
                    ct);

            if (existing != null)
            {
                // Обновляем поля, если они изменились
                existing.SubjectName = subject ?? existing.SubjectName;
                existing.TeacherName = teacher ?? existing.TeacherName;

                // Попытка распарсить номер аудитории из текстового поля Lesson.ClassRoom (доменная модель)
                existing.ClassroomNumber = ParseClassroomNumber(lesson.ClassRoom) ?? existing.ClassroomNumber;

                // Метод для Auditorium/route (если у вас есть логика — можно заполнить)
                existing.ClassroomRoute =
                    MapAuditoryLocationToRoute(lesson.AuditoryLocation) ?? existing.ClassroomRoute;

                // Обновлённая сущность будет сохранена при SaveChangesAsync
                return existing;
            }
            else
            {
                // Создание новой записи
                var newLesson = new LessonModel
                {
                    Parity = parity,
                    DayOfWeek = dayOfWeek,
                    PairNumber = pairNumber,
                    SubjectName = subject,
                    TeacherName = teacher,
                    ClassroomNumber = ParseClassroomNumber(lesson.ClassRoom),
                    ClassroomRoute = MapAuditoryLocationToRoute(lesson.AuditoryLocation)
                };

                await _context.Lessons.AddAsync(newLesson, ct);
                // НЕ вызываем SaveChanges здесь — вызываем в UpsertLessonsAsync для батча
                return newLesson;
            }
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
