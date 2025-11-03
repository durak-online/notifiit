using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotiFIITBot.Models;

[Table("classrooms")]
public class Classroom
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("number")]
    [StringLength(50)]
    public string Number { get; set; }
}