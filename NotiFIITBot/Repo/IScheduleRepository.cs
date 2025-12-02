using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Repo
{
    public interface IScheduleRepository
    {
        Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<LessonModel> lessons, CancellationToken ct = default);

        Task<List<LessonModel>> GetScheduleAsync(
            int groupNumber,
            int? subGroup,
            SchedulePeriod period,
            DateTime? now = null,
            CancellationToken ct = default);

        public enum SchedulePeriod { Today, Tomorrow, Week, TwoWeeks }
    }
}