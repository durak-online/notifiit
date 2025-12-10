using NotiFIITBot.Consts;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotiFIITBot.Database.Models;

[Table("week_evenness_configs")]
public class WeekEvennessConfig
{
    [Key]
    [Column("evenness")]
    public Evenness Evenness { get; set; } // "четная" или "нечетная" или "всегда"

    [Column("first_monday")]
    public DateOnly FirstMonday { get; set; } // "первый понедельник"
}

