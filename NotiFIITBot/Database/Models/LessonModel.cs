using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NotiFIITBot.Consts;

namespace NotiFIITBot.Database.Models;

[Table("lessons")]
public class LessonModel
{
    [Key]
    [Column("lesson_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] 
    public long LessonId { get; set; }
    
    [Column("men_group")]
    public int? MenGroup { get; set; }

    [Column("sub_group")]
    public int? SubGroup { get; set; } // 0, 1 или 2

    [Column("evenness")]
    public Evenness Evenness { get; set; } // четность

    [Column("day_of_week")]
    public DayOfWeek DayOfWeek { get; set; } 
    
    [Column("pair_number")]
    public int PairNumber { get; set; }


    [Column("subject_name")]
    [StringLength(255)]
    public string? SubjectName { get; set; } // название предмета

    [Column("teacher_name")]
    [StringLength(255)]
    public string? TeacherName { get; set; } // ФИО препода

    [Column("classroom_number")]
    public string? ClassroomNumber { get; set; } // номер кабинета

    [Column("classroom_route_url")]
    public string? AuditoryLocation { get; set; }
}