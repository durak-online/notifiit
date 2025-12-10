using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotiFIITBot.Database.Models;

[Table("users")]
public class User
{
    [Key]
    [Column("telegram_id")]
    public long TelegramId { get; set; } // id тг чата

    [Column("men_group")]
    public int MenGroup { get; set; } // номер группы

    [Column("sub_group")]
    public int? SubGroup { get; set; } // номер подгруппы

    [Column("notifications_enabled")]
    public bool NotificationsEnabled { get; set; } = true; // включить/выключить ВСЕ уведы

    [Column("global_notification_minutes")]
    public int GlobalNotificationMinutes { get; set; } = 5; // за сколько "n минут" (0-15) выставить все уведы
    
    
}
