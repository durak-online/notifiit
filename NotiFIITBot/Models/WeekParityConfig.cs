using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NotiFIITBot;

namespace NotiFIITBot.Models;

[Table("week_parity_configs")]
public class WeekParityConfig
{
    [Key]
    [Column("parity")]
    public Evenness Parity { get; set; } // "четная" или "нечетная" или "всегда"

    [Column("first_monday")]
    public DateOnly FirstMonday { get; set; } // "первый понедельник"
}

