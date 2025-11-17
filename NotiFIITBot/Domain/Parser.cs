using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Domain;

public class Parser
{
    
    private static readonly string[] ranges = ["ФИИТ-2!A1:J84"];
    private static readonly int[] SubGroups = [1, 2, 3, 4];
    private const int DaysToParse = 14;
    private const int MaxPairsPerDay = 7;
    /// <summary>
    /// all lessons for all groups
    /// </summary>
    /// <returns></returns>
    public static async Task<IEnumerable<Lesson>> GetLessons()
    {
        //нужно получить все группы а потом для всех кроме фиита пробежаться моим методом
        var lessons = new List<Lesson>();
        var groups = await GetAllGroupNames();
        var FIITGroups = await GetFiitGroupNames();

        var i = 0;
        foreach (var group in groups)
        {
            i++;
            if(i>1) break;
            if(FIITGroups.Contains(group)) continue;
            //if(group != 240801) continue;//TODO: test убрать
            foreach (var subGroup in SubGroups)
            {
                //if(subGroup!=1) continue;//TODO: test убрать
                var startDate = DateOnly.FromDateTime(DateTime.Now);
                for (int dayOffset = 0; dayOffset < DaysToParse; dayOffset++)
                {
                    var date = startDate.AddDays(dayOffset);
                    for (int pair = 1; pair <= MaxPairsPerDay; pair++)
                    {
                        var lesson = await ApiParser.GetLesson(group, date, pair, subGroup);
                        if (lesson != null)
                        {
                            lessons.Add(lesson);
                        }
                    }
                }
            }
        }

        //получаем группы фиита
        lessons = lessons.Union(await GetFiitLessons()).ToList();
        return lessons;
    }

    public static async Task<IEnumerable<int>> GetAllGroupNames()
    {
        int[] courses = [1, 2, 3, 4];
        var groups = new List<Group>();
        foreach (var course in courses)
        {
            var groupsForCourse = await ApiParser.GetGroups(course);
            groups = groups.Union(groupsForCourse).ToList();
        }

        return groups.Select(group => Convert.ToInt32(group.title.Split("-")[1]));
    }

    private static async Task<IEnumerable<int>> GetFiitGroupNames()
    {
        List<int> FIITGroups = [];
        foreach (var range in ranges)
        {
            var apiKey = "AIzaSyCGZyCs_HMeVEnqYRw2Bh5wDr3qzLfOR9g";
            var spreadsheetId = "1pj8fzVqrZVkNssSJiInxy_Cm54ddC8tm8eluMdV-XvM";
            var lessons = TableParser.GetTableData(apiKey, spreadsheetId, range);
            foreach (var lesson in lessons)
            {
                if(!FIITGroups.Contains(lesson.MenGroup))
                    FIITGroups.Add(lesson.MenGroup);
            }
        }
        return FIITGroups;
    }
    
    private static async Task<IEnumerable<Lesson>> GetFiitLessons()
    {
        List<Lesson> lessons = [];
        foreach (var range in ranges)
        {
            var apiKey = "AIzaSyCGZyCs_HMeVEnqYRw2Bh5wDr3qzLfOR9g";
            var spreadsheetId = "1pj8fzVqrZVkNssSJiInxy_Cm54ddC8tm8eluMdV-XvM";
            var lessonsForCourse = TableParser.GetTableData(apiKey, spreadsheetId, range);
            foreach (var lesson in lessonsForCourse)
            {
                lessons.Add(lesson);
            }
        }
        return lessons;
    }
}