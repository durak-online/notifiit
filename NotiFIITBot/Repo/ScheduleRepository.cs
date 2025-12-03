using System.Globalization;
 using Microsoft.EntityFrameworkCore;
 using NotiFIITBot.Consts;
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

        public async Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<LessonModel> lessonModels, CancellationToken ct = default)
        {
            var result = new List<LessonModel>();

            foreach (var model in lessonModels)
            {
                if (model.LessonId == Guid.Empty)
                    continue; 

                var existing = await _context.Lessons.FindAsync(new object[] { model.LessonId }, ct);

                if (existing == null)
                {
                    await _context.Lessons.AddAsync(model, ct);
                    result.Add(model);
                }
                else
                {
                    existing.MenGroup = model.MenGroup;
                    existing.SubGroup = model.SubGroup;
                    existing.SubjectName = model.SubjectName;
                    existing.TeacherName = model.TeacherName;
                    existing.ClassroomNumber = model.ClassroomNumber;
                    existing.AuditoryLocation = model.AuditoryLocation;
                    existing.PairNumber = model.PairNumber;
                    existing.DayOfWeek = model.DayOfWeek;
                    existing.Evenness = model.Evenness;

                    result.Add(existing);
                }
            }

            await _context.SaveChangesAsync(ct);
            return result;
        }

        public async Task<List<LessonModel>> GetScheduleAsync(
            int groupNumber,
            int? subGroup,
            IScheduleRepository.SchedulePeriod period,
            DateTime? now = null,
            CancellationToken ct = default)
        {
            now ??= DateTime.Now;

            var q = _context.Lessons.AsNoTracking()
                .Where(l => l.MenGroup == groupNumber);

            if (subGroup.HasValue)
                q = q.Where(l => l.SubGroup == subGroup.Value || l.SubGroup == null);

            var daysToLoad = period switch
            {
                IScheduleRepository.SchedulePeriod.Today => new() { now.Value.DayOfWeek },
                IScheduleRepository.SchedulePeriod.Tomorrow => new() { now.Value.AddDays(1).DayOfWeek },
                IScheduleRepository.SchedulePeriod.Week => Enum.GetValues<DayOfWeek>().ToList(),
                IScheduleRepository.SchedulePeriod.TwoWeeks => Enum.GetValues<DayOfWeek>().ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(period))
            };

            q = q.Where(l => daysToLoad.Contains(l.DayOfWeek));

            return await q.OrderBy(l => l.DayOfWeek)
                .ThenBy(l => l.PairNumber)
                .ToListAsync(ct);
        }
    }
}
