using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;

namespace NotiFIITBot.Repo
{
    public interface IScheduleRepository
    {
        Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<Lesson> lessons, CancellationToken ct = default);

        Task<List<LessonModel>> GetScheduleAsync(
            int groupNumber,
            int? subGroup,
            SchedulePeriod period,
            DateTime? now = null,
            CancellationToken ct = default);


        public enum SchedulePeriod
        {
            Today,
            Tomorrow,
            Week,
            TwoWeeks
        }

    }
}