using NotiFIITBot.Consts;
using NotiFIITBot.Logging;
using NotiFIITBot.Repo;
using Serilog;
using System.Text;

namespace NotiFIITBot.Domain;

public class ScheduleService(IScheduleRepository scheduleRepository, IUserRepository userRepository,
    ILoggerFactory loggerFactory)
{
    private readonly IScheduleRepository scheduleRepository = scheduleRepository;
    private readonly IUserRepository userRepository = userRepository;
    private readonly ILogger logger = loggerFactory.CreateLogger("SCHED_SERV");

    public async Task<string> GetSchedForPeriodAsync(long userId, SchedulePeriod period)
    {
        try
        {
            var user = await userRepository.FindUserAsync(userId);
            if (user == null)
                return "Ты еще не зарегестрирован в базе данных";

            var scheduleDays = await GetFormattedScheduleAsync(user.MenGroup, user.SubGroup, period);
            if (scheduleDays == null || scheduleDays.Count == 0)
            {
                if (period == SchedulePeriod.Today)
                    return $"Пар для группы МЕН-{user.MenGroup}-{user.SubGroup} на сегодня нет 🎉";
                else if (period == SchedulePeriod.Tomorrow)
                    return $"Пар для группы МЕН-{user.MenGroup}-{user.SubGroup} на завтра нет 🎉";

                return $"Пар для группы МЕН-{user.MenGroup}-{user.SubGroup} нет 🎉";
            }

            var strBuilder = new StringBuilder();

            foreach (var (date, lessons) in scheduleDays)
                strBuilder.Append(ScheduleFormatter.BuildDailySchedule(date, lessons));
            

            return strBuilder.ToString();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error while getting schedule from DB");
            return "Произошла ошибка при получении данных из базы";
        }
    }

    private async Task<List<(DateOnly Date, List<Lesson> Lessons)>> GetFormattedScheduleAsync(
        int groupNumber, 
        int? subGroup, 
        SchedulePeriod period)
    {
        var models = await scheduleRepository.GetScheduleAsync(groupNumber, subGroup, period);

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