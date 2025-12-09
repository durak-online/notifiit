using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Repo;

public interface IUserRepository
{
    Task UpdateUserPreferences(long chatId, int? group, int? subGroup, bool notificationsEnabled, int minutes);

    Task AddLessonOverrideAsync(long chatId, Guid lessonId, bool? enableOverride, int? minutesOverride);

    Task UpdateLessonOverrideAsync(long chatId, Guid lessonId, bool? enableOverride, int? minutesOverride);

    Task DeleteLessonOverrideAsync(long chatId, Guid lessonId);

    Task<User?> GetUserAsync(long chatId);
    
    Task DeleteUserAsync(long chatId);
    
    Task AddUserAsync(long chatId);
    
    Task UpdateUserAsync(User user);
}