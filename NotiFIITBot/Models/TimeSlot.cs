using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotiFIITBot.Models;

[Table("time_slots")]
public class TimeSlot
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("pair_number")]
    public int PairNumber { get; set; }

    [Column("start_time")]
    public TimeOnly StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly EndTime { get; set; } 
}