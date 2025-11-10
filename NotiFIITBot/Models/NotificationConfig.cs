using System.ComponentModel.DataAnnotations.Schema;

namespace NotiFIITBot.Models;

[Table("user_notification_config")]
public class UserNotificationConfig
{
    [Column("telegram_id")]
    public long TelegramId { get; set; }
    public User User { get; set; }

    [Column("lesson_id")]
    public int LessonId { get; set; }
    public Lesson Lesson { get; set; }
    
        
    // Переопределяет 'User.NotificationsEnabled'
    // null или false = использовать глобальную настройку
    // true = использовать это значение
    [Column("is_notification_enabled_override")]
    public bool? IsNotificationEnabledOverride { get; set; } 

    // Переопределяет 'User.GlobalNotificationMinutes'
    // null = использовать глобальную настройку
    // int (5-60) = использовать это значение
    [Column("notification_minutes_override")]
    public int? NotificationMinutesOverride { get; set; } 
}