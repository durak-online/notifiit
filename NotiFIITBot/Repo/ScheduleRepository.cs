using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;
using NotiFIITBot.Consts;
using NotiFIITBot.Repo;

namespace NotiFIITBot.Database.Repo
{
    public interface IScheduleRepository
    {
        /// <summary>
        /// Вставить или обновить много записей (batch).
        /// Возвращает список созданных/обновлённых LessonModel.
        /// </summary>
        Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<Lesson> lessons, CancellationToken ct = default);

        /// <summary>
        /// Вставить или обновить одну запись.
        /// </summary>
        Task<LessonModel> UpsertLessonAsync(Lesson lesson, CancellationToken ct = default);
    }
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

            var parity = lesson.EvennessOfWeek; // используем только EvennessOfWeek
            var dayOfWeek = lesson.DayOfWeek ?? DayOfWeek.Monday;
            var pairNumber = lesson.PairNumber ?? -1;
            var subject = lesson.SubjectName?.Trim();
            var teacher = lesson.TeacherName?.Trim();

            // Генерация LessonId: group-subGroup-parity-day-pair
            int gNum = lesson.MenGroup ?? 0;
            int sg = lesson.SubGroup ?? 0;
            int parityInt = parity switch
            {
                Evenness.Even => 0,
                Evenness.Odd => 1,
                Evenness.Always => 2,
                _ => 0
            };
            int day = (int)dayOfWeek;
            int pair = pairNumber;
            int lessonId = gNum * 10000 + sg * 1000 + parityInt * 100 + day * 10 + pair;

            // Проверяем, есть ли уже урок с таким LessonId
            var existing = await _context.Lessons.FirstOrDefaultAsync(l => l.LessonId == lessonId, ct);

            if (existing != null)
            {
                existing.SubjectName = subject ?? existing.SubjectName;
                existing.TeacherName = teacher ?? existing.TeacherName;
                existing.ClassroomNumber = ParseClassroomNumber(lesson.ClassRoom) ?? existing.ClassroomNumber;
                existing.ClassroomRoute = MapAuditoryLocationToRoute(lesson.AuditoryLocation) ?? existing.ClassroomRoute;
                existing.Evenness = parity; // сохраняем Evenness
                return existing;
            }

            var newLesson = new LessonModel
            {
                LessonId = lessonId,
                Evenness = parity,
                DayOfWeek = dayOfWeek,
                PairNumber = pairNumber,
                SubjectName = subject,
                TeacherName = teacher,
                ClassroomNumber = ParseClassroomNumber(lesson.ClassRoom),
                ClassroomRoute = MapAuditoryLocationToRoute(lesson.AuditoryLocation)
            };

            await _context.Lessons.AddAsync(newLesson, ct);
            return newLesson;
        }

        private int? ParseClassroomNumber(string? classRoom)
        {
            if (string.IsNullOrWhiteSpace(classRoom)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(classRoom, @"\d{1,4}");
            if (!m.Success) return null;
            if (int.TryParse(m.Value, out var n)) return n;
            return null;
        }

        private string? MapAuditoryLocationToRoute(string? auditoryLocation)
        {
            if (string.IsNullOrWhiteSpace(auditoryLocation)) return null;
            return auditoryLocation.Trim();
        }
    }
}
