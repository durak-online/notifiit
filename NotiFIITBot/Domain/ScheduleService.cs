using NotiFIITBot.Consts;
using NotiFIITBot.Repo;

namespace NotiFIITBot.Domain;

public class ScheduleService
{
    private readonly IScheduleRepository _repo;

    public ScheduleService(IScheduleRepository repo)
    {
        _repo = repo;
    }

    public async Task<List<(DateOnly Date, List<Lesson> Lessons)>> GetFormattedScheduleAsync(
        int groupNumber, 
        int? subGroup, 
        SchedulePeriod period)
    {
        var models = await _repo.GetScheduleAsync(groupNumber, subGroup, period);

        var today = DateOnly.FromDateTime(DateTime.Now);
        DateOnly startDay;
        int daysCount;

        switch (period)
        {
            case SchedulePeriod.Today:
                startDay = today;
                daysCount = 1;
                break;
            case SchedulePeriod.Tomorrow:
                startDay = today.AddDays(1);
                daysCount = 1;
                break;
            default:
            {
                var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
                startDay = today.AddDays(-daysFromMonday);
            
                daysCount = period == SchedulePeriod.TwoWeeks ? 14 : 7;
                break;
            }
        }

        var result = new List<(DateOnly Date, List<Lesson> Lessons)>();

        for (var i = 0; i < daysCount; i++)
        {
            var currentDate = startDay.AddDays(i);
            var currentEvenness = currentDate.GetEvenness();

            var dailyLessons = models
                .Where(m => m.DayOfWeek == currentDate.DayOfWeek)
                .Where(m => m.Evenness == Evenness.Always || m.Evenness == currentEvenness)
                .Select(m =>
                {
                    var start = m.StartTime;
                    var end = start.AddMinutes(90);

                    return new Lesson(
                        pairNumber: m.PairNumber,
                        subjectName: m.SubjectName,
                        teacherName: m.TeacherName,
                        classRoom: m.ClassroomNumber,
                        begin: start,
                        end: end,
                        auditoryLocation: m.AuditoryLocation,
                        subGroup: m.SubGroup ?? 0,
                        menGroup: m.MenGroup,
                        evennessOfWeek: m.Evenness,
                        dayOfWeek: m.DayOfWeek
                    )
                    {
                        LessonId = m.LessonId
                    };
                })
                .OrderBy(l => l.PairNumber)
                .ToList();

            if (dailyLessons.Count != 0)
            {
                result.Add((currentDate, dailyLessons));
            }
        }

        return result;
    }
}