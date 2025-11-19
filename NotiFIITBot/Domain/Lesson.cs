using NotiFIITBot.Consts;

namespace NotiFIITBot.Domain;

public class Lesson
{
    public Lesson(int? pairNumber, string? subjectName, string? teacherName, string? classRoom, TimeOnly? begin,
        TimeOnly? end, string? auditoryLocation, int subGroup, int menGroup, Evenness evennessOfWeek, DayOfWeek? dayOfWeek)
    {
        PairNumber = pairNumber;
        SubjectName = subjectName;
        TeacherName = teacherName;
        ClassRoom = classRoom;
        Begin = begin;
        End = end;
        AuditoryLocation = auditoryLocation;
        SubGroup = subGroup;
        MenGroup = menGroup;
        DayOfWeek = dayOfWeek;
        EvennessOfWeek = evennessOfWeek;
    }

    public int? PairNumber { get; set; }
    public string? SubjectName { get; set; }
    public string? TeacherName { get; set; }
    public string? ClassRoom { get; set; }
    public TimeOnly? Begin { get; set; }
    public TimeOnly? End { get; set; }
    
    public DayOfWeek? DayOfWeek { get; set; }
    public Evenness EvennessOfWeek { get; set; }
    
    public List<int>? ParityList { get; set; }
    public string? AuditoryLocation { get; set; }
    public int? SubGroup { get; set; }
    public int? MenGroup { get; set; }
}