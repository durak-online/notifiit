namespace NotiFIITBot;

public class Group
{
    public int id { get; set; }
    public int divisionId { get; set; }
    public int course { get; set; }
    public string title { get; set; }
}

public class Event
{
    public string id { get; set; }
    public int eventId { get; set; }
    public string title { get; set; }
    public string loadType { get; set; }
    public List<string> loadKeys { get; set; }
    public string date { get; set; }
    public string timeBegin { get; set; }
    public string timeEnd { get; set; }
    public int pairNumber { get; set; }
    public string auditoryTitle { get; set; }
    public string auditoryLocation { get; set; }
    public string teacherAuditoryTitle { get; set; }
    public string teacherAuditoryLocation { get; set; }
    public string comment { get; set; }
    public string teacherName { get; set; }
    public string teacherComment { get; set; }
    public string teacherLink { get; set; }
}

public class ScheduleResponse
{
    public Group group { get; set; }
    public List<Event> events { get; set; }
}