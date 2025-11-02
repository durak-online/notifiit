using System.Text.Json;

namespace NotiFIITBot;

public static class Parser
{
    private static readonly int[] DivisionIds = [62404, 62403]; //Точно такие и удобно ли это? //нутипа

    public static async Task<Lesson> GetLesson(int group, DateOnly date, int pairNumber, int subGroup)
    {
        using var client = new HttpClient();
        var groupId = await GetGroupId(group);
        var url =
            $"https://urfu.ru/api/v2/schedule/groups/{groupId}/schedule?date_gte={date.ToString("yyyy-MM-dd")}&date_lte={date.ToString("yyyy-MM-dd")}";
        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var schedule = JsonSerializer.Deserialize<ScheduleResponse>(content);
            //Console.WriteLine($"Группа: {schedule.group.title}");
            //Console.WriteLine("События:");
            foreach (var ev in schedule.events)
                if (pairNumber == ev.pairNumber &&
                    (ev.comment.Contains($@"{subGroup} пг.") || !ev.comment.Contains("пг.")))
                {
                    var timeBegin = TimeOnly.Parse(ev.timeBegin);
                    var timeEnd = TimeOnly.Parse(ev.timeEnd);
                    var lesson = new Lesson(pairNumber, ev.title, ev.teacherName, ev.auditoryTitle, timeBegin, timeEnd,
                        ev.auditoryLocation, subGroup, group, Evenness.Even, DayOfWeek.Monday);//TODO четность и день недели
                    return lesson;
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        return null;
    }


    /// <summary>
    /// without мен
    /// </summary>
    /// <param name="groupName"></param>
    /// <returns></returns>
    public static async Task<int> GetGroupId(int groupName) //в формате МЕН-groupName
    {
        using var client = new HttpClient();
        var url = "https://urfu.ru/api/v2/schedule/groups?search=" + groupName;
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var groups = JsonSerializer.Deserialize<List<Group>>(json);
        foreach (var group in groups!)
            if (group.title.Contains("МЕН"))
                return group.id;
        return -1;
    }

    public static async Task<List<Group>> GetGroups(int course)
    {
        var groups = new List<Group>();
        foreach (var divisionId in DivisionIds) groups.AddRange(await GetGroups(course, divisionId));
        return groups;
    }

    private static async Task<List<Group>?> GetGroups(int course, int divisionId)
    {
        using var client = new HttpClient();
        var url = $"https://urfu.ru/api/v2/schedule/divisions/{divisionId}/groups?course={course}";
        var json = await client.GetStringAsync(url);
        var groups = JsonSerializer.Deserialize<List<Group>>(json);
        return groups;
    }
}