using NotiFIITBot.Consts;
using System.Text.Json;

namespace NotiFIITBot.Domain;

public abstract class ApiParser : IParser
{
    private static readonly int[] DivisionIds = [62404, 62403];

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
            foreach (var ev in schedule.events)
                if (pairNumber == ev.pairNumber &&
                    (ev.comment.Contains($@"{subGroup} пг.") || !ev.comment.Contains("пг.")))
                {
                    var timeBegin = TimeOnly.Parse(ev.timeBegin);
                    var timeEnd = TimeOnly.Parse(ev.timeEnd);
                    var lesson = new Lesson(pairNumber, ev.title, ev.teacherName, ev.auditoryTitle, timeBegin, timeEnd,
                        ev.auditoryLocation, subGroup, group, 
                        date.Evenness(),
                        date.DayOfWeek);
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

public static class Extensions
{
    public static Evenness Evenness(this DateOnly date)
    {
        var firstMonday = GetFirstStudyDay(date);
        var indexOfWeek = (date.DayNumber - firstMonday.DayNumber) / 7;
        if (indexOfWeek % 2 == 0) return Consts.Evenness.Odd;
        return Consts.Evenness.Even;
    }

    private static bool IsFirstSem(DateOnly date)
    {
        return date.Month is >= 9 and <= 12 or 1;
    }

    private static DateOnly GetFirstStudyDay(DateOnly date)
    {
        //ну теперь получается ищем первый учебный день сентября
        //подумал чуть считерим и будем искать понедельник первой учебной недели, условно если учеба началась во вторник 1 сентября, то я возьму 31 августа
        var studyYear = date.Year;
        if (date.Month == 1) studyYear--;
        var firstStudyDay = new DateOnly(studyYear, 9, 1);
        if (!IsFirstSem(date)) return new DateOnly(studyYear, 2, 9);
        
        if (firstStudyDay.DayOfWeek == DayOfWeek.Saturday) firstStudyDay = firstStudyDay.AddDays(2);
        else if (firstStudyDay.DayOfWeek == DayOfWeek.Sunday) firstStudyDay = firstStudyDay.AddDays(1);
        else if (firstStudyDay.DayOfWeek != DayOfWeek.Monday)
            firstStudyDay = firstStudyDay.AddDays(1 - (int)firstStudyDay.DayOfWeek);
        return firstStudyDay;
    }
    
}