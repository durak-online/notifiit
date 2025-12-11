using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Repo;

public class UserRepository(ScheduleDbContext context) : IUserRepository
{
    private readonly ScheduleDbContext _context = context;

    public async Task UpdateUserPreferences(long chatId, int group, int? subGroup, bool notificationsEnabled, int minutes)
    {
        var user = await _context.Users.FindAsync(chatId);

        if (user != null)
        {
            user.MenGroup = group;
            user.SubGroup = subGroup;
            user.NotificationsEnabled = notificationsEnabled;
            user.GlobalNotificationMinutes = minutes;

            await _context.SaveChangesAsync();
        }
        else
        {
            //логи (если нужно)
        }
    }

    public async Task AddLessonOverrideAsync(long chatId, Guid lessonId, bool? enableOverride, int? minutesOverride)
    {
        var exists = await _context.UserNotificationConfigs
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

            _context.UserNotificationConfigs.Add(config);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateLessonOverrideAsync(long chatId, Guid lessonId, bool? enableOverride, int? minutesOverride)
    {
        var config = await _context.UserNotificationConfigs
            .FirstOrDefaultAsync(c => c.TelegramId == chatId && c.LessonId == lessonId);

        if (config != null)
        {
            config.IsNotificationEnabledOverride = enableOverride;
            config.NotificationMinutesOverride = minutesOverride;

            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteLessonOverrideAsync(long chatId, Guid lessonId)
    {
        var config = await _context.UserNotificationConfigs
            .FirstOrDefaultAsync(c => c.TelegramId == chatId && c.LessonId == lessonId);

        if (config != null)
        {
            _context.UserNotificationConfigs.Remove(config);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<User?> FindUserAsync(long chatId)
    {
        return await _context.Users.FindAsync(chatId);
    }
    
    public async Task AddUserAsync(long chatId, int group, int subGroup)
    {
        var exists = await _context.Users.AnyAsync(u => u.TelegramId == chatId);

        if (!exists)
        {
            var newUser = new User
            {
                TelegramId = chatId,
                MenGroup = group,
                SubGroup = subGroup,
                NotificationsEnabled = true,
                GlobalNotificationMinutes = 15
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
        }
    }
    
    public async Task UpdateUserAsync(User user)
    {
        var existingUser = await _context.Users.FindAsync(user.TelegramId);
        if (existingUser != null)
        {
            existingUser.MenGroup = user.MenGroup;
            existingUser.SubGroup = user.SubGroup;
            existingUser.NotificationsEnabled = user.NotificationsEnabled;
            existingUser.GlobalNotificationMinutes = user.GlobalNotificationMinutes;

            await _context.SaveChangesAsync();
        }
        else
        {
            // по хорошему бы тут залогировать типо пользователя с таким айди не существует
        }
    }
    
    public async Task DeleteUserAsync(long chatId)
    {
        var user = await _context.Users.FindAsync(chatId);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}