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

    public async Task<List<Lesson>> GetFormattedScheduleAsync(
        int groupNumber, 
        int? subGroup, 
        SchedulePeriod period)
    {
        var models = await _repo.GetScheduleAsync(groupNumber, subGroup, period);


        var lessons = models.Select(m =>
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
        });

        var mergedLessons = LessonProcessor.MergeByParity(lessons);

        return mergedLessons.OrderBy(l => l.DayOfWeek).ThenBy(l => l.PairNumber).ToList();
    }
}