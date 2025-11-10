using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NotiFIITBot.Consts;

namespace NotiFIITBot.Database.Models;

[Table("lessons")]
public class LessonModel
{ 
    [Key] 
    [Column("lesson_id")]
    public int LessonId { get; set; } // id пары

    [Column("parity")]
    public Evenness Parity { get; set; } // четность

    [Column("day_of_week")]
    public DayOfWeek DayOfWeek { get; set; } // день недели

    [Column("pair_number")]
    public int PairNumber { get; set; } // номер пары

    [Column("subject_name")] 
    [StringLength(255)]
    public string? SubjectName { get; set; } // название предмета

    [Column("teacher_name")]
    [StringLength(255)]
    public string? TeacherName { get; set; } // ФИО препода

    [Column("classroom_number")]
    public int? ClassroomNumber { get; set; } // номер кабинета
    
    [Column("classroom_route_url")]
    public string? ClassroomRoute { get; set; } // маршрут до кабинета
    
}
