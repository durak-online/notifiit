using System.Text.RegularExpressions;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace NotiFIITBot;

// Временный для хранения результатов из ячеек
internal class ParsedLessonInfo
{
    public string? SubjectName { get; set; }
    public string? TeacherName { get; set; }
    public string? ClassRoom { get; set; }
    public DateOnly? Date { get; set; }
}

public class TableParser
{
    public static void ShowTables()
    {
        try
        {
            var apiKey = "AIzaSyCGZyCs_HMeVEnqYRw2Bh5wDr3qzLfOR9g";
            var spreadsheetId = "1pj8fzVqrZVkNssSJiInxy_Cm54ddC8tm8eluMdV-XvM";
            var range = "ФИИТ-2!A1:J84"; //вставь "ФИИТ-1, с 15.09" перед ! для обработки 1 курса

            var scheduleData = GetTableData(apiKey, spreadsheetId, range);

            foreach (var lesson in scheduleData)
                Console.WriteLine($"Группа: {lesson.MenGroup}, " + $"Подгруппа: {lesson.SubGroup}, " +
                                  $"Предмет: {lesson.SubjectName}, " +
                                  $"Преподаватель: {lesson.TeacherName}, " +
                                  $"Аудитория: {lesson.ClassRoom}, " +
                                  $"Пара №: {lesson.PairNumber}, " +
                                  $"Начало: {lesson.Begin}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    public static List<Lesson> GetTableData(string apiKey, string spreadsheetId, string range)
    {
        var service = new SheetsService(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "Schedule Parser"
        });

        var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
        var response = request.Execute();
        var values = response.Values;

        var lessons = new List<Lesson>();

        if (values == null || values.Count < 4)
            return lessons;

        // Определение групп
        var groupsRow = values[1];
        var columnGroupMap = new Dictionary<int, string>();
        for (var j = 0; j < groupsRow.Count; j++)
        {
            var groupCell = groupsRow[j]?.ToString();
            if (!string.IsNullOrWhiteSpace(groupCell) && groupCell.Contains("МЕН"))
            {
                columnGroupMap[j] = Regex.Match(groupCell, @"МЕН-\d+").Value;
                columnGroupMap[j + 1] = Regex.Match(groupCell, @"МЕН-\d+").Value;
            }
        }

        // Определение подгрупп
        var columnSubgroupMap = new Dictionary<int, int>();
        var subgroupsRow = values[2];

        for (var j = 0; j < subgroupsRow.Count; j++)
        {
            var subgroupCell = subgroupsRow[j]?.ToString();
            if (!string.IsNullOrWhiteSpace(subgroupCell))
                if (int.TryParse(subgroupCell.AsSpan().Slice(subgroupCell.Length - 1), out var subgroupNumber))
                    columnSubgroupMap[j] = subgroupNumber;
        }

        // Непосредственно обработка ячеек с расписанием
        var currentDayOfWeek = "";
        TimeOnly? currentTime = null;
        int? currentPairNumber = null;

        for (var i = 3; i < values.Count; i++)
        {
            var row = values[i];

            // Получаю день недели, может пригодится
            if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]?.ToString())) currentDayOfWeek = row[0].ToString();

            // Номер пары и время начала
            if (row.Count > 1 && !string.IsNullOrWhiteSpace(row[1]?.ToString()))
            {
                var timeCell = row[1].ToString();
                var parts = timeCell.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0) currentPairNumber = ParseRomanNumeral(parts[0]);

                if (TimeOnly.TryParse(parts[1], out var time)) currentTime = time;
            }

            if (currentTime == null || string.IsNullOrWhiteSpace(currentDayOfWeek)) continue; //скип информационной

            // Обработка ячеек
            for (var j = 2; j < row.Count; j++)
            {
                var lessonCell = row[j]?.ToString();

                if (string.IsNullOrWhiteSpace(lessonCell) || !columnSubgroupMap.ContainsKey(j)) continue;

                var lessonInfo = ParseLessonCell(lessonCell);
                if (lessonInfo != null)
                {
                    var lesson = new Lesson(
                        currentPairNumber,
                        lessonInfo.SubjectName,
                        lessonInfo.TeacherName,
                        lessonInfo.ClassRoom,
                        currentTime,
                        null,
                        null,
                        columnSubgroupMap[j],
                        columnGroupMap[j],
                        lessonInfo.Date
                    );
                    lessons.Add(lesson);
                }
            }
        }

        return lessons;
    }

    private static int? ParseRomanNumeral(string roman)
    {
        if (string.IsNullOrWhiteSpace(roman)) return null;
        return roman.ToUpper() switch
        {
            "I" => 1, "II" => 2, "III" => 3, "IV" => 4,
            "V" => 5, "VI" => 6, "VII" => 7,
            _ => null
        };
    }

    private static ParsedLessonInfo? ParseLessonCell(string cell)
    {
        var info = new ParsedLessonInfo();

        //Убираем пометки "такое-то время с такой-то даты", не убрал "углублённая группа"
        var cleanCell = Regex.Replace(cell, @"\s*(?:\d{1,2}:\d{2}-\d{1,2}:\d{2}\s+)?с\s+\d{1,2}\s+\w+.*$", "").Trim();

        var parts = cleanCell.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (parts.Count == 0) return null;

        var classroom = parts.FirstOrDefault(p => Regex.IsMatch(p, @"^\d{3}$"));
        if (classroom != null)
        {
            info.ClassRoom = classroom;
            parts.Remove(classroom);
        }

        if (parts.Count > 0)
        {
            info.TeacherName = parts.Last();
            parts.RemoveAt(parts.Count - 1);
        }

        info.SubjectName = string.Join(", ", parts);

        return info;
    }
}