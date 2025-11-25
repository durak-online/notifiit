using System.Text.RegularExpressions;
using NotiFIITBot.Consts;
using System.Text.Json;

namespace NotiFIITBot.Domain;

public abstract class ApiParser
{
    private static readonly int[] DivisionIds = [62404, 62403];

    public static async Task<IEnumerable<Lesson>> GetLessons(int groupId, int subGroup)
    {
        var lessons = new List<Lesson>();
        var startDate = DateOnly.FromDateTime(DateTime.Now);
        var endDate = startDate.AddDays(14); 

        using var client = new HttpClient();
        
        var url = $"https://urfu.ru/api/v2/schedule/groups/{groupId}/schedule?date_gte={startDate:yyyy-MM-dd}&date_lte={endDate:yyyy-MM-dd}";

        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var schedule = JsonSerializer.Deserialize<ScheduleResponse>(content);

            if (schedule?.events == null) return lessons;

            for (var index = 0; index < schedule.events.Count; index++)
            {
                var ev = schedule.events[index];
                // Фильтр подгруппы
                var isForMySubgroup = ev.comment == null ||
                                      ev.comment.Contains($"{subGroup} пг.") ||
                                      !ev.comment.Contains("пг.");

                if (!isForMySubgroup) continue;

                if (!DateOnly.TryParse(ev.date, out var lessonDate)) continue;

                var timeBegin = TimeOnly.Parse(ev.timeBegin);
                var timeEnd = TimeOnly.Parse(ev.timeEnd);

                // ОЧИСТКА НАЗВАНИЯ
                // Убираем "(подгруппа)", "(лекция)", "(практика)" и лишние пробелы
                var cleanTitle = Regex.Replace(ev.title, @"\s*\((подгруппа|лекция|практика|лаб.*?|семинар).*?\)", "",
                    RegexOptions.IgnoreCase).Trim();

                var lesson = new Lesson(
                    ev.pairNumber,
                    cleanTitle, // Используем чистое название
                    ev.teacherName,
                    ev.auditoryTitle,
                    timeBegin,
                    timeEnd,
                    ev.auditoryLocation,
                    subGroup,
                    0, // Заглушка
                    lessonDate.Evenness(),
                    lessonDate.DayOfWeek
                );

                lessons.Add(lesson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[API ERROR] {url}: {ex.Message}");
        }

        return lessons;
    }

    public static async Task<List<Group>> GetGroups(int course)
    {
        var groups = new List<Group>();
        foreach (var divisionId in DivisionIds) 
            groups.AddRange(await GetGroups(course, divisionId));
        return groups;
    }

    private static async Task<List<Group>> GetGroups(int course, int divisionId)
    {
        using var client = new HttpClient();
        var url = $"https://urfu.ru/api/v2/schedule/divisions/{divisionId}/groups?course={course}";
        var json = await client.GetStringAsync(url);
        return JsonSerializer.Deserialize<List<Group>>(json) ?? new List<Group>();
    }

    public class ScheduleResponse { public List<Event> events { get; set; } }
    
    public class Event 
    { 
        public int pairNumber { get; set; } 
        public string date { get; set; }
        public string timeBegin { get; set; } 
        public string timeEnd { get; set; } 
        public string title { get; set; } 
        public string teacherName { get; set; } 
        public string auditoryTitle { get; set; } 
        public string auditoryLocation { get; set; } 
        public string comment { get; set; } 
    }
    
    public class Group { public int id { get; set; } public string title { get; set; } }
}

public static class Extensions
{
    public static Evenness Evenness(this DateOnly date)
    {
        var firstMonday = GetFirstStudyDay(date);
        var indexOfWeek = (date.DayNumber - firstMonday.DayNumber) / 7;
        return indexOfWeek % 2 == 0 ? Consts.Evenness.Odd : Consts.Evenness.Even;
    }
    private static bool IsFirstSem(DateOnly date) => date.Month is >= 9 and <= 12 or 1;
    private static DateOnly GetFirstStudyDay(DateOnly date)
    {
        var studyYear = date.Year;
        if (date.Month == 1) studyYear--;
        var firstStudyDay = new DateOnly(studyYear, 9, 1);
        if (!IsFirstSem(date)) return new DateOnly(studyYear, 2, 9);
        if (firstStudyDay.DayOfWeek == DayOfWeek.Saturday) firstStudyDay = firstStudyDay.AddDays(2);
        else if (firstStudyDay.DayOfWeek == DayOfWeek.Sunday) firstStudyDay = firstStudyDay.AddDays(1);
        else if (firstStudyDay.DayOfWeek != DayOfWeek.Monday) firstStudyDay = firstStudyDay.AddDays(1 - (int)firstStudyDay.DayOfWeek);
        return firstStudyDay;
    }
}