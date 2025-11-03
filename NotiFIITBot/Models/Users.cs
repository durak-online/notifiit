using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotiFIITBot.Models;

[Table("users")]
public class User
{
    [Key]
    [Column("telegram_id")]
    public long TelegramId { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }
    public Group Group { get; set; }
}