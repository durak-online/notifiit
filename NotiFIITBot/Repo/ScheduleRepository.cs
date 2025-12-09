using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;

namespace NotiFIITBot.Repo
{
    public class ScheduleRepository : IScheduleRepository
    {
        private readonly ScheduleDbContextFactory _contextFactory;

        public ScheduleRepository()
        {
            _contextFactory = new ScheduleDbContextFactory();
        }

        public async Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<LessonModel> lessonModels, CancellationToken ct = default)
        {
            using var context = _contextFactory.CreateDbContext(null);

            var result = new List<LessonModel>();

            foreach (var model in lessonModels)
            {
                if (model.LessonId == Guid.Empty)
                    continue; 

                var existing = await context.Lessons.FindAsync(new object[] { model.LessonId }, ct);

                if (existing == null)
                {
                    await context.Lessons.AddAsync(model, ct);
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
                    
                    // Время НЕ трогаем, так как модель старая

                    result.Add(existing);
                }
            }

            await context.SaveChangesAsync(ct);
            return result;
        }

        public async Task<List<LessonModel>> GetScheduleAsync(
            int groupNumber,
            int? subGroup,
            SchedulePeriod period,
            DateTime? now = null,
            CancellationToken ct = default)
        {
            using var context = _contextFactory.CreateDbContext(null);

            now ??= DateTime.Now;

            // Фильтр по группе
            var q = context.Lessons.AsNoTracking()
                .Where(l => l.MenGroup == groupNumber);

            if (subGroup.HasValue)
            {
                q = q.Where(l => l.SubGroup == subGroup.Value || l.SubGroup == 0 || l.SubGroup == null);
            }

            //  Фильтр по дням недели
            var daysToLoad = period switch
            {
                SchedulePeriod.Today => new() { now.Value.DayOfWeek },
                SchedulePeriod.Tomorrow => new() { now.Value.AddDays(1).DayOfWeek },
                SchedulePeriod.Week => Enum.GetValues<DayOfWeek>().ToList(),
                SchedulePeriod.TwoWeeks => Enum.GetValues<DayOfWeek>().ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(period))
            };

            q = q.Where(l => daysToLoad.Contains(l.DayOfWeek));
            
            //  Фильтр по четности 
            var todayDate = DateOnly.FromDateTime(now.Value);
            
            if (period == SchedulePeriod.Today)
            {
                var todayEvenness = DateOnlyExtensions.GetEvenness(todayDate);
                q = q.Where(l => l.Evenness == Evenness.Always || l.Evenness == todayEvenness);
            }
            else if (period == SchedulePeriod.Tomorrow)
            {
                var tmrwEvenness = DateOnlyExtensions.GetEvenness(todayDate.AddDays(1));
                q = q.Where(l => l.Evenness == Evenness.Always || l.Evenness == tmrwEvenness);
            }

            return await q.OrderBy(l => l.DayOfWeek)
                .ThenBy(l => l.PairNumber)
                .ToListAsync(ct);
        }
    }
}