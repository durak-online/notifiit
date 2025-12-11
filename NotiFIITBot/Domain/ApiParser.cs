using System.Text.Json;
using System.Text.RegularExpressions;
using NotiFIITBot.Consts;

namespace NotiFIITBot.Domain;

public static class ApiParser
{
    private static readonly int[] DivisionIds = [62404, 62403];

    /// <summary>
    ///  Получает список занятий на 2 недели вперед.
    /// Использует Regex для очистки и возвращает список.
    /// </summary>
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

            foreach (var ev in schedule.events)
            {
                var isForMySubgroup = ev.comment == null ||
                                      ev.comment.Contains($"{subGroup} пг.") ||
                                      !ev.comment.Contains("пг.");

                if (!isForMySubgroup) continue;

                if (!DateOnly.TryParse(ev.date, out var lessonDate)) continue;

                var timeBegin = TimeOnly.Parse(ev.timeBegin);
                var timeEnd = TimeOnly.Parse(ev.timeEnd);

                var cleanTitle = Regex.Replace(ev.title, @"\s*\((подгруппа|лекция|практика|лаб.*?|семинар).*?\)", "",
                    RegexOptions.IgnoreCase).Trim();

                var lesson = new Lesson(
                    ev.pairNumber,
                    cleanTitle, 
                    ev.teacherName,
                    ev.auditoryTitle,
                    timeBegin,
                    timeEnd,
                    ev.auditoryLocation,
                    subGroup,
                    groupId, 
                    DateOnlyExtensions.GetEvenness(lessonDate),
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

    /// <summary>
    /// Получает внутренний ID группы по её номеру (например, 240801 -> 63804)
    /// </summary>
    public static async Task<int> GetGroupId(int groupNumber)
    {
        using var client = new HttpClient();
        var url = "https://urfu.ru/api/v2/schedule/groups?search=" + groupNumber;
        try 
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var groups = JsonSerializer.Deserialize<List<Group>>(json);

            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group.title.Contains($"МЕН-{groupNumber}") || group.title.Contains(groupNumber.ToString()))
                        return group.id;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetGroupId Error] {ex.Message}");
        }
        return -1;
    }

    public static async Task<List<Group>> GetGroups(int course)
    {
        var groups = new List<Group>();
        foreach (var divisionId in DivisionIds) 
        {
            var divisionGroups = await GetGroups(course, divisionId);
            groups.AddRange(divisionGroups.Where(g => g.title.StartsWith("МЕН-")));
        }
        return groups;
    }

    private static async Task<List<Group>> GetGroups(int course, int divisionId)
    {
        using var client = new HttpClient();
        var url = $"https://urfu.ru/api/v2/schedule/divisions/{divisionId}/groups?course={course}";
        try 
        {
            var json = await client.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<Group>>(json) ?? new List<Group>();
        }
        catch
        {
            return new List<Group>();
        }
    }
}