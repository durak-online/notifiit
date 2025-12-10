using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Repo;

public class UserRepository : IUserRepository
{
    private readonly ScheduleDbContextFactory _contextFactory;

    public UserRepository()
    {
        _contextFactory = new ScheduleDbContextFactory();
    }

    public async Task UpdateUserPreferences(long chatId, int? group, int? subGroup, bool notificationsEnabled, int minutes)
    {
        using var context = _contextFactory.CreateDbContext(null);

        var user = await context.Users.FindAsync(chatId);

        if (user != null)
        {
            user.GroupNumber = group;
            user.SubGroupNumber = subGroup;
            user.NotificationsEnabled = notificationsEnabled;
            user.GlobalNotificationMinutes = minutes;

            await context.SaveChangesAsync();
        }
        else
        {
            //логи (если нужно)
        }
    }

    public async Task AddLessonOverrideAsync(long chatId, Guid lessonId, bool? enableOverride, int? minutesOverride)
    {
        await using var context = _contextFactory.CreateDbContext(null);

        var exists = await context.UserNotificationConfigs
            .AnyAsync(c => c.TelegramId == chatId && c.LessonId == lessonId);

        if (!exists)
        {
            var config = new UserNotificationConfig
            {
                TelegramId = chatId,
                LessonId = lessonId,
                IsNotificationEnabledOverride = enableOverride,
                NotificationMinutesOverride = minutesOverride
            };

            context.UserNotificationConfigs.Add(config);
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateLessonOverrideAsync(long chatId, Guid lessonId, bool? enableOverride, int? minutesOverride)
    {
        await using var context = _contextFactory.CreateDbContext(null);

        var config = await context.UserNotificationConfigs
            .FirstOrDefaultAsync(c => c.TelegramId == chatId && c.LessonId == lessonId);

        if (config != null)
        {
            config.IsNotificationEnabledOverride = enableOverride;
            config.NotificationMinutesOverride = minutesOverride;

            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteLessonOverrideAsync(long chatId, Guid lessonId)
    {
        await using var context = _contextFactory.CreateDbContext(null);

        var config = await context.UserNotificationConfigs
            .FirstOrDefaultAsync(c => c.TelegramId == chatId && c.LessonId == lessonId);

        if (config != null)
        {
            context.UserNotificationConfigs.Remove(config);
            await context.SaveChangesAsync();
        }
    }

    public async Task<User?> GetUserAsync(long chatId)
    {
        await using var context = _contextFactory.CreateDbContext(null);
        return await context.Users.FindAsync(chatId);
    }
    
    public async Task AddUserAsync(long chatId, int group, int subGroup)
    {
        await using var context = _contextFactory.CreateDbContext(null);
        var exists = await context.Users.AnyAsync(u => u.TelegramId == chatId);

        if (!exists)
        {
            var newUser = new User
            {
                TelegramId = chatId,
                GroupNumber = group,
                SubGroupNumber = subGroup,
                NotificationsEnabled = true,
                GlobalNotificationMinutes = 15
            };

            context.Users.Add(newUser);
            await context.SaveChangesAsync();
        }
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