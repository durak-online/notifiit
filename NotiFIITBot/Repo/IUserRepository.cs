using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Domain.Interfaces;

public interface IUserRepository
{
    Task UpdateUserPreferences(long chatId, int? group, int? subGroup, bool notificationsEnabled, int minutes);

    Task SetLessonOverride(long chatId, Guid lessonId, bool? enableOverride, int? minutesOverride);

    Task<User?> GetUserAsync(long chatId);
}