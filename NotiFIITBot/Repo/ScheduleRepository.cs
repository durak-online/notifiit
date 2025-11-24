using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;

namespace NotiFIITBot.Repo
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

            foreach (var lesson in lessons)
            {
                if (lesson.LessonId == null) continue;

                var lessonId = lesson.LessonId.Value;

                // 1. Ищем запись
                var existing = await _context.Lessons.FindAsync(new object[] { lessonId }, ct);

                if (existing == null)
                {
                    // 2. Создаем новую
                    var newModel = new LessonModel
                    {
                        LessonId = lessonId,
                        MenGroup = lesson.MenGroup,
                        SubGroup = lesson.SubGroup,
                        SubjectName = lesson.SubjectName?.Trim(),
                        TeacherName = lesson.TeacherName?.Trim(),
                        ClassroomNumber = lesson.ClassRoom,
                        AuditoryLocation = lesson.AuditoryLocation,

                        PairNumber = lesson.PairNumber ?? 0,
                        DayOfWeek = lesson.DayOfWeek ?? DayOfWeek.Monday,
                        Evenness = lesson.EvennessOfWeek,
                    };

                    await _context.Lessons.AddAsync(newModel, ct);
                    result.Add(newModel);
                }
                else
                {
                    // 3. Обновляем существующую
                    existing.MenGroup = lesson.MenGroup;
                    existing.SubGroup = lesson.SubGroup;
                    existing.SubjectName = lesson.SubjectName?.Trim();
                    existing.TeacherName = lesson.TeacherName?.Trim();
                    existing.ClassroomNumber = lesson.ClassRoom;
                    existing.AuditoryLocation = lesson.AuditoryLocation;

                    existing.PairNumber = lesson.PairNumber ?? 0;
                    existing.DayOfWeek = lesson.DayOfWeek ?? DayOfWeek.Monday;
                    existing.Evenness = lesson.EvennessOfWeek;

                    result.Add(existing);
                }
            }

            await _context.SaveChangesAsync(ct);
            return result;
        }
    }
}