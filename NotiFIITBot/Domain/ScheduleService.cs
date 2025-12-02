using NotiFIITBot.Repo; 
using NotiFIITBot.Database.Models;

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
        IScheduleRepository.SchedulePeriod period)
    {
        var models = await _repo.GetScheduleAsync(groupNumber, subGroup, period);

        var lessons = models.Select(m => new Lesson(
            pairNumber: m.PairNumber,
            subjectName: m.SubjectName,
            teacherName: m.TeacherName,
            classRoom: m.ClassroomNumber,
            begin: null, 
            end: null,
            auditoryLocation: m.AuditoryLocation,
            subGroup: m.SubGroup ?? 0,
            menGroup: m.MenGroup ?? 0,
            evennessOfWeek: m.Evenness,
            dayOfWeek: m.DayOfWeek
        ) { LessonId = m.LessonId });

        var mergedLessons = LessonProcessor.MergeByParity(lessons);

        return mergedLessons.OrderBy(l => l.DayOfWeek).ThenBy(l => l.PairNumber).ToList();
    }
}