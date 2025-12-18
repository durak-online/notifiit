using NotiFIITBot.Consts;
using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Repo;

public interface IScheduleRepository
{
    Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<LessonModel> lessons);

    Task<List<LessonModel>> GetScheduleAsync(
        int groupNumber,
        int? subGroup,
        SchedulePeriod period,
        DateTime? now = null);

    Task<bool> GroupExistsAsync(int groupId, int subGroup);
}