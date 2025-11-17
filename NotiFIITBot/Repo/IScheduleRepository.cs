using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;
namespace NotiFIITBot.Repo;

public interface IScheduleRepository
{
    /// <summary>
    /// Вставить или обновить много записей (batch).
    /// Возвращает список созданных/обновлённых LessonModel.
    /// </summary>
    Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<Lesson> lessons, CancellationToken ct = default);

    /// <summary>
    /// Вставить или обновить одну запись.
    /// </summary>
    Task<LessonModel> UpsertLessonAsync(Lesson lesson, CancellationToken ct = default);
}