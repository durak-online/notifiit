using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotiFIITBot.Models;

[Table("schedule")]
public class Schedule
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("group_id")]
    public int GroupId { get; set; }
    public Group Group { get; set; }

    [Column("subject_id")]
    public int SubjectId { get; set; }
    public Subjects.Subject Subject { get; set; }

    [Column("teacher_id")]
    public int TeacherId { get; set; }
    public Teacher Teacher { get; set; }

    [Column("classroom_id")]
    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; }

    [Column("timeslot_id")]
    public int TimeSlotId { get; set; }
    public TimeSlot TimeSlot { get; set; }

    [Column("day_of_week")]
    public int DayOfWeek { get; set; }

    [Column("week_pority")]
    public WeekPority WeekPority { get; set; }
}