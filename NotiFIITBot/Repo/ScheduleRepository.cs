using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;
using NotiFIITBot.Logging;
using Serilog;

namespace NotiFIITBot.Repo;

public class ScheduleRepository(
    ScheduleDbContext context,
    CancellationTokenSource cts,
    ILoggerFactory loggerFactory) : IScheduleRepository
{
    private readonly ScheduleDbContext _context = context;
    private readonly CancellationTokenSource ct = cts;
    private readonly ILogger logger = loggerFactory.CreateLogger("SCHED_REPO");

    public async Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<LessonModel> lessonModels)
    {
        logger.Information($"Starting upsert for {lessonModels.Count()} lessons.");
        var result = new List<LessonModel>();
        var addedCount = 0;
        var updatedCount = 0;

        try
        {
            foreach (var model in lessonModels)
            {
                if (model.LessonId == Guid.Empty)
                {
                    logger.Warning("Skipping lesson with empty ID.");
                    continue;
                }

                var existing = await _context.Lessons.FindAsync([model.LessonId], cancellationToken: ct.Token);

                if (existing == null)
                {
                    await _context.Lessons.AddAsync(model, ct.Token);
                    result.Add(model);
                    addedCount++;
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
                    existing.StartTime = model.StartTime;
                    existing.DayOfWeek = model.DayOfWeek;
                    existing.Evenness = model.Evenness;

                    result.Add(existing);
                    updatedCount++;
                }
            }

            await _context.SaveChangesAsync(ct.Token);
            logger.Information($"Upsert finished. Added: {addedCount}, Updated: {updatedCount}.");
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during UpsertLessonsAsync.");
            throw;
        }
    }

    public async Task<List<LessonModel>> GetScheduleAsync(
        int groupNumber,
        int? subGroup,
        SchedulePeriod period,
        DateTime? now = null)
    {
        logger.Information($"Getting schedule for group {groupNumber}, subgroup {subGroup ?? 0}, period {period}.");
        now ??= DateTime.Now;

        try
        {
            // Фильтр по группе
            var q = _context.Lessons.AsNoTracking()
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

            var lessons = await q.OrderBy(l => l.DayOfWeek)
                .ThenBy(l => l.PairNumber)
                .ToListAsync(ct.Token);

            logger.Information($"Found {lessons.Count} lessons for group {groupNumber}, period {period}.");
            return lessons;
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Error getting schedule for group {groupNumber}, period {period}.");
            throw;
        }
    }
}