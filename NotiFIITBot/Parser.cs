using System.Text.Json;

namespace NotiFIITBot;

public static class Parser
{
    public static async Task<Lesson> GetLesson(string group, DateOnly date, int pairNumber, int subGroup)
    {
        using var client = new HttpClient();
        return await GetLesson(client, group, date, pairNumber, subGroup);
    }

    private static async Task<Lesson> GetLesson(HttpClient client, string group, DateOnly date, int pairNumber, int subGroup)
    {
        var groupId = await GetGroupId(group);
        var url = $"https://urfu.ru/api/v2/schedule/groups/{groupId}/schedule?date_gte={date.ToString("yyyy-MM-dd")}&date_lte={date.ToString("yyyy-MM-dd")}";
        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var schedule = JsonSerializer.Deserialize<ScheduleResponse>(content);
            Console.WriteLine($"Группа: {schedule.group.title}");
            Console.WriteLine("События:");
            foreach (var ev in schedule.events)
            {
                if (pairNumber == ev.pairNumber && (ev.comment.Contains($@"{subGroup} пг.") || !ev.comment.Contains("пг.")))
                {
                    var timeBegin = TimeOnly.Parse(ev.timeBegin);
                    var timeEnd = TimeOnly.Parse(ev.timeEnd);
                    var lesson = new Lesson(pairNumber, ev.title, ev.teacherName, ev.auditoryTitle, timeBegin, timeEnd, ev.auditoryLocation, subGroup, date);
                    return lesson;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        return null;
    }

    private static async Task<int> GetGroupId(string groupName)
    {
        using var client = new HttpClient();
        var url = "https://urfu.ru/api/v2/schedule/groups?search=" +  groupName;
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var groups = JsonSerializer.Deserialize<List<Group>>(json);
        foreach (var group in groups)
        {
            if (group.title.Contains("МЕН"))
                return group.id;
        }
        return -1;
    }

    public static Lesson GetLesson(string groupId, DateTime date, int subGroup)//ищем ближайшее начало пары и возвращаем его
    {
        throw new NotImplementedException();
    }
    
    
}

public class Lesson
{
    public Lesson(int? pairNumber, string? subjectName, string? teacherName, string? сlassroom, TimeOnly? begin, TimeOnly? end, string? auditoryLocation, int? subGroup, DateOnly? date)
    {
        PairNumber = pairNumber;
        SubjectName = subjectName;
        TeacherName = teacherName;
        Сlassroom = сlassroom;
        Begin = begin;
        End = end;
        AuditoryLocation = auditoryLocation;
        SubGroup = subGroup;
        Date = date;
    }

    public int? PairNumber { get; set; }
    public string? SubjectName { get; set; }
    public string? TeacherName { get; set; }
    public string? Сlassroom { get; set; }
    public TimeOnly? Begin { get; set; }
    public TimeOnly? End { get; set; }
    
    public DateOnly? Date { get; set; }
    public string? AuditoryLocation  { get; set; }
    public int? SubGroup {get; set; }
}