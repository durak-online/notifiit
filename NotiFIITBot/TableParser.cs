using System.Text.RegularExpressions;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

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
                Console.WriteLine($"День: {lesson.DayOfWeek}, " +
                                  $"Группа: {lesson.MenGroup}, " + $"Подгруппа: {lesson.SubGroup}, " +
                                  $"Предмет: {lesson.SubjectName}, " +
                                  $"Преподаватель: {lesson.TeacherName}, " +
                                  $"Локация: {lesson.AuditoryLocation}, " +
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
            ApplicationName = "Schedule Parser",
        });

        //var request = service.Spreadsheets.Values.Get(spreadsheetId, range); Было раньше
        //var response = request.Execute();
        //var valuesOld = response.Values;
        
        var values = GetValuesWithMergedCells(spreadsheetId, range, service);
        
        var detailRequest = service.Spreadsheets.Get(spreadsheetId);
        detailRequest.Ranges = range;
        detailRequest.IncludeGridData = true; 
        var spreadsheet = detailRequest.Execute();
        var sheet = spreadsheet.Sheets[0];
        var gridData = sheet.Data[0];
        
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
        int currentPairNumber = -1;
        
        //Console.WriteLine($"!!! {valuesMerged[35][2].ToString()}");
        
        for (var i = 3; i < values.Count; i++)
        {
            var row = values[i];
            
            // Пропускаем пустые строки или строки с общей информацией
            if (row == null || row.Count == 0)
            {
                //Console.WriteLine($"{values[i-1][2]}");
                continue;
            };
            
            // Получаю день недели, может пригодится
            if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]?.ToString())) currentDayOfWeek = row[0].ToString();

            // Номер пары и время начала
            if (row.Count > 1 && !string.IsNullOrWhiteSpace(row[1]?.ToString()))
            {
                var timeCell = row[1].ToString();
                var parts = timeCell.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0) currentPairNumber = ParseRomanNumeral(parts[0]);

                if (TimeOnly.TryParse(parts[1], out var time))
                {
                    currentTime = time;
                }
                else
                {
                    currentTime = null;
                }
            }
            else
            {
                currentTime = null;
            }

            if (currentTime == null || string.IsNullOrWhiteSpace(currentDayOfWeek)) continue; //скип информационной

            // Обработка ячеек
            for (var j = 2; j < row.Count; j++)
            {
                var lessonCell = row[j]?.ToString();

                if (string.IsNullOrWhiteSpace(lessonCell) || !columnSubgroupMap.ContainsKey(j)) continue;

                var lessonInfo = ParseLessonCell(lessonCell);
                var location = GetLocationByColor(gridData.RowData[i].Values[j]);
                var menGroup = int.Parse(columnGroupMap[j].Split("-")[1]);
                if (lessonInfo != null)
                {
                    var lesson = new Lesson(
                        currentPairNumber,
                        lessonInfo.SubjectName,
                        lessonInfo.TeacherName,
                        lessonInfo.ClassRoom,
                        currentTime,
                        null,
                        location,
                        columnSubgroupMap[j],
                        menGroup, 
                        Evenness.Even,//TODO хз как у тебя тут все работает но думаю понятно че сюда запихать 
                        ParseDayOfWeek(currentDayOfWeek) 
                    );
                    lessons.Add(lesson);
                }
            }
        }

        return lessons;
    }

    private static DayOfWeek? ParseDayOfWeek(string day)
    {
        if (string.IsNullOrWhiteSpace(day)) return null;
        
        return day.ToUpper() switch
        {
            "ПН" => DayOfWeek.Monday,
            "ВТ" => DayOfWeek.Tuesday,
            "СР" => DayOfWeek.Wednesday,
            "ЧТ" => DayOfWeek.Thursday, 
            "ПТ" => DayOfWeek.Friday,
            "СБ" => DayOfWeek.Saturday,
            "ВС" => DayOfWeek.Sunday,
            _ => null
        };
    }
    private static int ParseRomanNumeral(string roman)
    {
        if (string.IsNullOrWhiteSpace(roman)) return -1;
        return roman.ToUpper() switch
        {
            "I" => 1, "II" => 2, "III" => 3, "IV" => 4,
            "V" => 5, "VI" => 6, "VII" => 7,
            _ => -1
        };
    }
    
    private static string GetLocationByColor(CellData cell)
    {
        var bgColor = cell.EffectiveFormat?.BackgroundColor;
        //Console.WriteLine($"!{bgColor.Red} {bgColor.Green} {bgColor.Blue}");
        
        if ( bgColor.Red == 0.9411765f && bgColor.Green == 1f && bgColor.Blue == 0.6862745f)
            return "Куйбышева, 48";
        
        if (bgColor.Red == 0.8980392f && bgColor.Green == 0.9372549f &&  bgColor.Blue == 1f)
            return "Онлайн";
        
        return "Тургенева, 4";
    }

    private static ParsedLessonInfo? ParseLessonCell(string cell)
    {
        var info = new ParsedLessonInfo();
        if (cell.Contains("Физкультура"))
        {
            info.SubjectName = "Физкультура";
            return info;
        }

        //Убираем пометки "такое-то время с такой-то даты", не убрал "углублённая группа"
        var cleanCell = Regex.Replace(cell, @"\s*(?:\d{1,2}:\d{2}-\d{1,2}:\d{2}\s+)?с\s+\d{1,2}\s+\w+.*$", "").Trim();
        
        //Отдельно убираем "углублённая группа"
        cleanCell = Regex.Replace(cleanCell, @"углублённая группа", "", RegexOptions.IgnoreCase).Trim();
        
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
    
    /// <summary>
    /// Метод, который распространяет объединённые ячейки на все касающиеся их индексы (по дефолту значение значение лежит только в левом верхнем)
    /// </summary>
    /// <param name="spreadsheetId"></param>
    /// <param name="range"></param>
    /// <param name="service"></param>
    /// <returns></returns>
    public static IList<IList<object>> GetValuesWithMergedCells(string spreadsheetId, string range, SheetsService service)
{
    var spreadsheetRequest = service.Spreadsheets.Get(spreadsheetId);
    spreadsheetRequest.Ranges = new List<string> { range };
    spreadsheetRequest.Fields = "sheets.merges";
    var spreadsheet = spreadsheetRequest.Execute();
    var merges = new List<Google.Apis.Sheets.v4.Data.GridRange>();
    if (spreadsheet.Sheets != null)
    {
        merges = spreadsheet.Sheets.First().Merges?.ToList() ?? new List<Google.Apis.Sheets.v4.Data.GridRange>();
    }
    
    var valuesRequest = service.Spreadsheets.Values.Get(spreadsheetId, range);
    var valuesResponse = valuesRequest.Execute();
    var values = valuesResponse.Values;

    if (values == null || values.Count == 0)
    {
        return new List<IList<object>>();
    }
    
    foreach (var merge in merges)
    {
        if (merge.StartRowIndex == null || merge.StartColumnIndex == null || 
            merge.EndRowIndex == null || merge.EndColumnIndex == null) continue;

        int startRow = (int)merge.StartRowIndex;
        int endRow = (int)merge.EndRowIndex;
        int startCol = (int)merge.StartColumnIndex;
        int endCol = (int)merge.EndColumnIndex;

        object mergedValue = null;
        if (values.Count > startRow && values[startRow].Count > startCol)
        {
            mergedValue = values[startRow][startCol];
        }

        if (mergedValue != null)
        {
            for (int i = startRow; i < endRow; i++)
            {
                while (values.Count <= i)
                {
                    values.Add(new List<object>());
                }
                var row = values[i];

                for (int j = startCol; j < endCol; j++)
                {
                    while (row.Count <= j)
                    {
                        row.Add(null);
                    }
                    row[j] = mergedValue;
                }
            }
        }
    }

    return values;
}
}