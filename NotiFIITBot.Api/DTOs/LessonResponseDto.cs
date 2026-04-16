namespace NotiFIITBot.Api.DTOs;

public class LessonResponseDto
{
    public DateTime Date { get; set; }
    public string DayOfWeek { get; set; }
    public int PairNumber { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public string? SubjectName { get; set; }
    public string? TeacherName { get; set; }
    public string? ClassroomNumber { get; set; }
    public string? AuditoryLocation { get; set; }
    public string Evenness { get; set; }
}