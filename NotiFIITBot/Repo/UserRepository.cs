using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain.Interfaces;

namespace NotiFIITBot.Repo;

public class UserRepository : IUserRepository
{
    private readonly ScheduleDbContextFactory _contextFactory;

    public UserRepository()
    {
        _contextFactory = new ScheduleDbContextFactory();
    }

    // сохранение начальных настроек (Группа, Подгруппа, Уведы)
    public async Task UpdateUserPreferences(long chatId, int? group, int? subGroup, bool notificationsEnabled, int minutes)
    {
        using var context = _contextFactory.CreateDbContext(null);

        var user = await context.Users.FindAsync(chatId);

        if (user == null)
        {
            user = new User { TelegramId = chatId };
            context.Users.Add(user);
        }

        user.GroupNumber = group;
        user.SubGroupNumber = subGroup;
        user.NotificationsEnabled = notificationsEnabled;
        user.GlobalNotificationMinutes = minutes;

        await context.SaveChangesAsync();
    }

    // Редактор уведомлений
    public async Task SetLessonOverride(long chatId, Guid lessonId, bool? enableOverride, int? minutesOverride)
    {
        using var context = _contextFactory.CreateDbContext(null);

        var config = await context.UserNotificationConfigs
            .FirstOrDefaultAsync(c => c.TelegramId == chatId && c.LessonId == lessonId);

        if (config == null)
        {
            config = new UserNotificationConfig
            {
                TelegramId = chatId,
                LessonId = lessonId
            };
            context.UserNotificationConfigs.Add(config);
        }

        config.IsNotificationEnabledOverride = enableOverride;
        config.NotificationMinutesOverride = minutesOverride;

        if (enableOverride == null && minutesOverride == null)
        {
            context.UserNotificationConfigs.Remove(config);
        }

        await context.SaveChangesAsync();
    }

    public async Task<User?> GetUserAsync(long chatId)
    {
        await using var context = _contextFactory.CreateDbContext(null);
        return await context.Users.FindAsync(chatId);
    }
    
    public async Task UpdateUserAsync(User user)
    {
        await using var context = _contextFactory.CreateDbContext(null);
        var existingUser = await context.Users.FindAsync(user.TelegramId);
        if (existingUser != null)
        {
            existingUser.GroupNumber = user.GroupNumber;
            existingUser.SubGroupNumber = user.SubGroupNumber;
            existingUser.NotificationsEnabled = user.NotificationsEnabled;
            existingUser.GlobalNotificationMinutes = user.GlobalNotificationMinutes;

            await context.SaveChangesAsync();
        }
        else
        {
            // по хорошему бы тут залогировать типо пользователя с таким айди не существует
        }
    }
    
    public async Task DeleteUserAsync(long chatId)
    {
        await using var context = _contextFactory.CreateDbContext(null);
        var user = await context.Users.FindAsync(chatId);
        if (user != null)
        {
            context.Users.Remove(user);
            await context.SaveChangesAsync();
        }
    }
}