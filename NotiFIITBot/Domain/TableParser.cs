using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using NotiFIITBot.Consts;
using Serilog;

namespace NotiFIITBot.Domain;

internal class ParsedLessonInfo
{
    public string? SubjectName { get; set; }
    public string? TeacherName { get; set; }
    public string? ClassRoom { get; set; }
}

public abstract class TableParser
{
    /// <summary>
    /// Метод для отладки. Выводит расписание в консоль.
    /// </summary>
    public static void ShowTables()
    {
        try
        {
            var scheduleData = GetTableData(EnvReader.GoogleApiKey, EnvReader.TableId, EnvReader.Fiit2Range);

            foreach (var lesson in scheduleData)
                Console.WriteLine($"День: {lesson.DayOfWeek}, " +
                                  $"Группа: {lesson.MenGroup}, " + $"Подгруппа: {lesson.SubGroup}, " +
                                  $"Предмет: {lesson.SubjectName}, " +
                                  $"Преподаватель: {lesson.TeacherName}, " +
                                  $"Локация: {lesson.AuditoryLocation}, " +
                                  $"Аудитория: {lesson.ClassRoom}, " +
                                  $"Пара №: {lesson.PairNumber}, " +
                                  $"Начало: {lesson.Begin} " +
                                  $"Четность: {lesson.EvennessOfWeek}");
        }
        catch (Exception ex)
        {
            Log.Error($"Ошибка в ShowTables: {ex.Message}");
        }
    }

    public static List<Lesson> GetTableData(string apiKey, string spreadsheetId, string range, int[]? targetGroups = null)
    {
        var service = new SheetsService(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "Schedule Parser"
        });

        var values = GetValuesWithMergedCells(spreadsheetId, range, service);
        var gridData = GetGridData(spreadsheetId, range, service);

        if (values == null || values.Count < 4) 
            return new List<Lesson>();

        var columnGroupMap = BuildGroupMap(values[1]);
        var columnSubgroupMap = BuildSubgroupMap(values[2]);

        return ProcessSheetRows(values, gridData, columnGroupMap, columnSubgroupMap, targetGroups);
    }

    private static List<Lesson> ProcessSheetRows(
        IList<IList<object>> values, 
        GridData gridData,
        Dictionary<int, string> columnGroupMap, 
        Dictionary<int, int> columnSubgroupMap,
        int[]? targetGroups)
    {
        var currentDayOfWeek = "";
        TimeOnly? currentTime = null;
        var currentPairNumber = -1;

        var lessons = new List<Lesson>();
        var seenLessons = new HashSet<string>();

        // Начинаем с 3-й строки (после заголовков)
        for (var i = 3; i < values.Count; i++)
        {
            var row = values[i];
            if (row == null || row.All(c => c == null || string.IsNullOrWhiteSpace(c.ToString()))) 
                continue;

            var firstCell = row.Count > 0 ? row[0]?.ToString() ?? "" : "";
            if (firstCell.Contains("Общая информация", StringComparison.InvariantCultureIgnoreCase) ||
                firstCell.Contains("расписание звонков", StringComparison.InvariantCultureIgnoreCase)) 
                continue;

            currentDayOfWeek = GetDayOfWeek(row, currentDayOfWeek);
            var (time, pairNum) = GetTimeAndPairNumber(row);
            
            if (time != null)
            {
                currentTime = time;
                currentPairNumber = pairNum;
            }

            if (currentTime == null || string.IsNullOrWhiteSpace(currentDayOfWeek))
                continue;

            ProcessLessonCells(values, gridData, columnGroupMap, columnSubgroupMap, 
                row, i, currentTime, currentDayOfWeek, currentPairNumber, 
                seenLessons, lessons, targetGroups);
        }

        return lessons;
    }

    private static void ProcessLessonCells(
        IList<IList<object>> values, 
        GridData gridData,
        Dictionary<int, string> columnGroupMap,
        Dictionary<int, int> columnSubgroupMap, 
        IList<object> row, int i, 
        [DisallowNull] TimeOnly? currentTime,
        string currentDayOfWeek,
        int currentPairNumber, 
        HashSet<string> seenLessons, 
        List<Lesson> lessons,
        int[]? targetGroups)
    {
        for (var j = 2; j < row.Count; j++)
        {
            var lessonCell = row[j]?.ToString();
            if (string.IsNullOrWhiteSpace(lessonCell) || !columnSubgroupMap.ContainsKey(j)) 
                continue;
            
            if (i > 0 && i - 1 < values.Count)
            {
                var prevRow = values[i - 1];
                var (prevTime, _) = GetTimeAndPairNumber(prevRow);
                if (prevTime != null && prevTime.Equals(currentTime))
                {
                    var prevCell = prevRow.Count > j ? prevRow[j]?.ToString() : null;
                    if (prevCell == lessonCell) 
                    {
                        continue; 
                    }
                }
            }
            
            if (!columnGroupMap.TryGetValue(j, out var menGroupStr))
                continue;

            var match = Regex.Match(menGroupStr, @"\d+");
            if (!match.Success) 
                continue;
            var menGroup = int.Parse(match.Value);

            if (targetGroups != null && targetGroups.Length > 0 && !targetGroups.Contains(menGroup)) 
                continue;

            var lessonInfo = GetCleanLessonInfo(lessonCell);
            if (lessonInfo == null) continue; 

            var location = "Тургенева, 4"; 
            
            if (lessonCell.Contains("онлайн", StringComparison.InvariantCultureIgnoreCase))
            {
                location = "Онлайн";
            }
            else 
            {
                try
                {
                    if (gridData?.RowData != null && gridData.RowData.Count > i &&
                        gridData.RowData[i].Values != null && gridData.RowData[i].Values.Count > j)
                    {
                        var colorLoc = GetLocationByColor(gridData.RowData[i].Values[j]);
                        if (colorLoc == "Онлайн") location = "Онлайн";
                        else if (colorLoc != "Тургенева, 4") location = colorLoc;
                    }
                }
                catch { /* ignore */ }
            }

            if (lessonInfo.SubjectName == "Физкультура") location = null;
            
            if (location == "Онлайн") 
                lessonInfo.ClassRoom = "Онлайн";

            var eveness = GetEvenness(values, i, currentTime, j);
            
            var lessonKey = $"{currentDayOfWeek}_{currentPairNumber}_{currentTime}_{menGroup}_{columnSubgroupMap[j]}_{lessonInfo.SubjectName}_{lessonInfo.TeacherName}_{lessonInfo.ClassRoom}_{eveness}";

            if (!seenLessons.Add(lessonKey))
                continue;

            var subject = lessonInfo.SubjectName;
            if (string.IsNullOrWhiteSpace(subject))
                continue;

            var teacher = lessonInfo.TeacherName; 
            var room = lessonInfo.ClassRoom;      

            var lesson = new Lesson(
                currentPairNumber,
                subject,
                teacher,
                room,
                currentTime,
                null,
                location,
                columnSubgroupMap[j],
                menGroup,
                eveness,
                ParseDayOfWeek(currentDayOfWeek));
                
            lessons.Add(lesson);
        }
    }
    

    private static ParsedLessonInfo? GetCleanLessonInfo(string cell)
    {
        if (cell.Contains("Общая информация", StringComparison.InvariantCultureIgnoreCase) ||
            cell.Contains("расписание звонков", StringComparison.InvariantCultureIgnoreCase))
            return null;

        var info = new ParsedLessonInfo();

        if (cell.Contains("Физкультура", StringComparison.InvariantCultureIgnoreCase) || 
            cell.Contains("Фузкультура", StringComparison.InvariantCultureIgnoreCase))
        {
            info.SubjectName = "Физкультура"; 
            info.ClassRoom = null;            
            info.TeacherName = null;          
            return info; 
        }

        var cleanCell = Regex.Replace(cell, @"\s*(?:\d{1,2}:\d{2}-\d{1,2}:\d{2}\s+)?с\s+\d{1,2}\s+\w+.*$", "").Trim();
        cleanCell = Regex.Replace(cleanCell, @",?\s*онлайн", "", RegexOptions.IgnoreCase).Trim();

        if (cleanCell.Contains("Иностранный язык", StringComparison.InvariantCultureIgnoreCase))
        {
            info.SubjectName = "Иностранный язык";
            info.ClassRoom = null; 
            var engParts = cell.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries); 
            foreach (var p in engParts)
            {
                if (Regex.IsMatch(p.Trim(), @"[А-Я]\.[А-Я]\."))
                    info.TeacherName = p.Trim();
            }
            if (info.TeacherName != null && info.TeacherName.Contains("Иностранный язык")) info.TeacherName = null;
            return info;
        }

        var parts = cleanCell.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (parts.Count == 0) return null;

        var classroom = parts.FirstOrDefault(p => Regex.IsMatch(p, @"^\d{3}[а-яА-Я]?$"));
        if (classroom != null)
        {
            info.ClassRoom = classroom;
            parts.Remove(classroom);
        }

        if (parts.Count > 1)
        {
            var potentialTeacher = parts.Last();
            info.TeacherName = potentialTeacher;
            parts.RemoveAt(parts.Count - 1);
            
            if (parts.Count > 1 && Regex.IsMatch(parts.Last(), @"^[А-Я]\.[А-Я]\.?$")) 
            {
                 info.TeacherName = parts.Last() + " " + info.TeacherName;
                 parts.RemoveAt(parts.Count - 1);
            }
        }

        info.SubjectName = string.Join(", ", parts);
        return info;
    }


    private static Evenness GetEvenness(IList<IList<object>> values, int i, TimeOnly? currentTime, int j)
    {
        var currentContent = values[i][j]?.ToString() ?? "";

        // 1. Смотрим ВНИЗ (i+1)
        if (i + 1 < values.Count)
        {
            var nextRow = values[i + 1];
            var (nextTime, _) = GetTimeAndPairNumber(nextRow);
            
            if (nextTime != null && nextTime.Equals(currentTime))
            {
                var nextContent = nextRow.Count > j ? nextRow[j]?.ToString() ?? "" : "";
                return nextContent == currentContent ? Evenness.Always : Evenness.Odd;
            }
        }

        // 2. Смотрим ВВЕРХ (i-1)
        if (i - 1 >= 0)
        {
            var prevRow = values[i - 1];
            var (prevTime, _) = GetTimeAndPairNumber(prevRow);
            
            if (prevTime != null && prevTime.Equals(currentTime))
            {
                string prevContent = prevRow.Count > j ? prevRow[j]?.ToString() ?? "" : "";
                if (prevContent == currentContent) return Evenness.Always;

                return Evenness.Even;
            }
        }

        return Evenness.Always;
    }
    
    private static (TimeOnly?, int) GetTimeAndPairNumber(IList<object> row)
    {
        var timeCell = "";
        var pairCell = "";
        if (row.Count > 1) timeCell = row[1].ToString() ?? "";
        if (row.Count > 0) pairCell = row[0].ToString() ?? "";
        if (string.IsNullOrWhiteSpace(timeCell) && Regex.IsMatch(pairCell, @"\d{1,2}:\d{2}")) timeCell = pairCell;
        return GetTimeAndNumberOfPairFromStr(timeCell, pairCell);
    }

    private static (TimeOnly?, int) GetTimeAndNumberOfPairFromStr(string timeCell, string pairCell)
    {
        TimeOnly? currentTime = null;
        var currentPairNumber = -1;
        var romanMatch = Regex.Match(timeCell + " " + pairCell, @"\b(I|II|III|IV|V|VI|VII)\b", RegexOptions.IgnoreCase);
        if (romanMatch.Success) currentPairNumber = ParseRomanNumeral(romanMatch.Value);
        var timeMatch = Regex.Match(timeCell, @"\d{1,2}:\d{2}");
        if (timeMatch.Success && TimeOnly.TryParse(timeMatch.Value, out var t)) currentTime = t;
        return (currentTime, currentPairNumber);
    }

    private static string GetLocationByColor(CellData cell)
    {
        if (cell?.EffectiveFormat?.BackgroundColor == null) return "Тургенева, 4";
        var bgColor = cell.EffectiveFormat.BackgroundColor;
        bool IsColor(float? r, float? g, float? b, float targetR, float targetG, float targetB)
        {
            var e = 0.05f;
            return Math.Abs((r ?? 0) - targetR) < e && Math.Abs((g ?? 0) - targetG) < e && Math.Abs((b ?? 0) - targetB) < e;
        }
        if (IsColor(bgColor.Red, bgColor.Green, bgColor.Blue, 0.941f, 1f, 0.686f)) return "Куйбышева, 48";
        if (IsColor(bgColor.Red, bgColor.Green, bgColor.Blue, 0.898f, 0.937f, 1f)) return "Онлайн";
        return IsColor(bgColor.Red, bgColor.Green, bgColor.Blue, 0.81f, 0.88f, 0.95f) ? "Онлайн" : "Тургенева, 4";
    }

    private static string? GetDayOfWeek(IList<object> row, string? currentDayOfWeek)
    {
        if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0].ToString())) currentDayOfWeek = row[0].ToString();
        return currentDayOfWeek;
    }

    private static Dictionary<int, int> BuildSubgroupMap(IList<object> subgroupsRow)
    {
        var columnSubgroupMap = new Dictionary<int, int>();
        for (var j = 0; j < subgroupsRow.Count; j++)
        {
            var subgroupCell = subgroupsRow[j]?.ToString();
            if (string.IsNullOrWhiteSpace(subgroupCell)) continue;
            var match = Regex.Match(subgroupCell, @"\d+", RegexOptions.RightToLeft);
            if (match.Success && int.TryParse(match.Value, out var subgroupNumber)) columnSubgroupMap[j] = subgroupNumber;
        }
        return columnSubgroupMap;
    }

    private static Dictionary<int, string> BuildGroupMap(IList<object> groupsRow)
    {
        var columnGroupMap = new Dictionary<int, string>();
        for (var j = 0; j < groupsRow.Count; j++)
        {
            var groupCell = groupsRow[j]?.ToString();
            if (string.IsNullOrWhiteSpace(groupCell) ||
                !groupCell.Contains("МЕН", StringComparison.InvariantCultureIgnoreCase)) continue;
            var match = Regex.Match(groupCell, @"МЕН\s*-?\s*\d+", RegexOptions.IgnoreCase);
            var groupName = match.Success ? match.Value.Replace(" ", "") : groupCell.Trim();
            columnGroupMap[j] = groupName;
        }
        return columnGroupMap;
    }

    private static GridData GetGridData(string spreadsheetId, string range, SheetsService service)
    {
        var detailRequest = service.Spreadsheets.Get(spreadsheetId);
        detailRequest.Ranges = range;
        detailRequest.IncludeGridData = true;
        var spreadsheet = detailRequest.Execute();
        if (spreadsheet.Sheets.Count == 0 || spreadsheet.Sheets[0].Data.Count == 0) return new GridData();
        return spreadsheet.Sheets[0].Data[0];
    }

    private static DayOfWeek? ParseDayOfWeek(string day)
    {
        if (string.IsNullOrWhiteSpace(day)) return null;
        return day.ToUpper().Trim() switch
        {
            "ПН" => DayOfWeek.Monday, "ВТ" => DayOfWeek.Tuesday, "СР" => DayOfWeek.Wednesday, "ЧТ" => DayOfWeek.Thursday,
            "ПТ" => DayOfWeek.Friday, "СБ" => DayOfWeek.Saturday, "ВС" => DayOfWeek.Sunday, _ => null
        };
    }

    private static int ParseRomanNumeral(string roman)
    {
        if (string.IsNullOrWhiteSpace(roman)) return -1;
        return roman.ToUpper().Trim() switch { "I" => 1, "II" => 2, "III" => 3, "IV" => 4, "V" => 5, "VI" => 6, "VII" => 7, _ => -1 };
    }

    public static IList<IList<object>> GetValuesWithMergedCells(string spreadsheetId, string range, SheetsService service)
    {
        var spreadsheetRequest = service.Spreadsheets.Get(spreadsheetId);
        spreadsheetRequest.Ranges = new List<string> { range };
        spreadsheetRequest.Fields = "sheets.merges";
        var spreadsheet = spreadsheetRequest.Execute();
        var merges = spreadsheet.Sheets.FirstOrDefault()?.Merges?.ToList() ?? new List<GridRange>();
        var valuesRequest = service.Spreadsheets.Values.Get(spreadsheetId, range);
        var values = valuesRequest.Execute().Values;
        if (values == null || values.Count == 0) return new List<IList<object>>();

        foreach (var merge in merges)
        {
            if (merge.StartRowIndex == null || merge.EndRowIndex == null || merge.StartColumnIndex == null || merge.EndColumnIndex == null) continue;
            var startRow = (int)merge.StartRowIndex;
            var endRow = (int)merge.EndRowIndex;
            var startCol = (int)merge.StartColumnIndex;
            var endCol = (int)merge.EndColumnIndex;
            object? mergedValue = null;
            if (values.Count > startRow && values[startRow].Count > startCol)
                mergedValue = values[startRow][startCol];

            if (mergedValue == null) 
                continue;
            for (var i = startRow; i < endRow; i++)
            {
                while (values.Count <= i) values.Add(new List<object>());
                for (var j = startCol; j < endCol; j++) {
                    while (values[i].Count <= j) values[i].Add(null);
                    values[i][j] = mergedValue;
                }
            }
        }
        return values;
    }
}