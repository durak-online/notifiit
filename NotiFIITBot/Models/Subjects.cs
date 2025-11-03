using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotiFIITBot.Models;

public class Subjects
{
    [Table("subjects")]
    public class Subject
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("title")]
        [StringLength(255)]
        public string Title { get; set; }
    }
}