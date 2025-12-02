using System.Text.RegularExpressions;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using NotiFIITBot.Consts;

namespace NotiFIITBot.Domain;

internal class ParsedLessonInfo
{
    public string? SubjectName { get; set; }
    public string? TeacherName { get; set; }
    public string? ClassRoom { get; set; }
}

public abstract class TableParser
{
    public static List<Lesson> GetTableData(string apiKey, string spreadsheetId, string range, int[]? targetGroups = null)
    {
        var service = new SheetsService(new Google.Apis.Services.BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "Schedule Parser"
        });

        // 1. Получаем данные с учетом merged cells
        var values = GetValuesWithMergedCells(spreadsheetId, range, service);
        var gridData = GetGridData(spreadsheetId, range, service);

        if (values == null || values.Count < 4) return new List<Lesson>();

        // 2. Строим карты колонок
        var columnGroupMap = BuildGroupMap(values[1]);
        // ФИКС: Умный поиск подгрупп (работает даже с пустыми заголовками)
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
            // Защита от пустых строк и строк с инфой о звонках
            if (row == null || row.Count == 0) continue;
            var firstCell = row.Count > 0 ? row[0]?.ToString() : "";
            if (firstCell != null && firstCell.Contains("Общая информация")) continue;

            // Обновляем текущий день и время
            currentDayOfWeek = GetDayOfWeek(row, currentDayOfWeek);
            var (time, pairNum) = GetTimeAndPairNumber(row);
            
            if (time != null)
            {
                currentTime = time;
                currentPairNumber = pairNum;
            }

            if (currentTime == null || string.IsNullOrWhiteSpace(currentDayOfWeek)) continue;

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
        TimeOnly? currentTime,
        string currentDayOfWeek,
        int currentPairNumber, 
        HashSet<string> seenLessons, 
        List<Lesson> lessons,
        int[]? targetGroups)
    {
        for (var j = 2; j < row.Count; j++)
        {
            // Если нет маппинга для колонки - пропускаем
            if (!columnSubgroupMap.ContainsKey(j) || !columnGroupMap.ContainsKey(j)) continue;

            var cellContent = row[j]?.ToString();
            if (string.IsNullOrWhiteSpace(cellContent)) continue;

            // Парсим номер группы
            var groupStr = columnGroupMap[j];
            var match = Regex.Match(groupStr, @"\d{6}");
            if (!match.Success) continue;
            var menGroup = int.Parse(match.Value);

            // Фильтр по группам
            if (targetGroups != null && targetGroups.Length > 0 && !targetGroups.Contains(menGroup)) continue;

            // ФИКС: Regex очистка данных
            var lessonInfo = GetCleanLessonInfo(cellContent);
            if (lessonInfo == null) continue;

            // Локация
            var location = "Тургенева, 4";
            if (cellContent.Contains("онлайн", StringComparison.InvariantCultureIgnoreCase))
            {
                location = "Онлайн";
            }
            else if (gridData.RowData != null && gridData.RowData.Count > i && 
                     gridData.RowData[i].Values != null && gridData.RowData[i].Values.Count > j)
            {
                var colorLoc = GetLocationByColor(gridData.RowData[i].Values[j]);
                if (colorLoc == "Онлайн") location = "Онлайн";
                else if (colorLoc == "Куйбышева, 48") location = "Куйбышева, 48";
            }
            if (lessonInfo.SubjectName == "Физкультура") location = null;

            var eveness = GetEvenness(values, i, currentTime, j);

            var lessonKey = $"{currentDayOfWeek}_{currentPairNumber}_{menGroup}_{columnSubgroupMap[j]}_{lessonInfo.SubjectName}_{eveness}";
            
            if (seenLessons.Add(lessonKey))
            {
                lessons.Add(new Lesson(
                    currentPairNumber,
                    lessonInfo.SubjectName,
                    lessonInfo.TeacherName,
                    lessonInfo.ClassRoom,
                    currentTime,
                    null,
                    location,
                    columnSubgroupMap[j],
                    menGroup,
                    eveness,
                    ParseDayOfWeek(currentDayOfWeek)
                ));
            }
        }
    }

    private static Evenness GetEvenness(IList<IList<object>> values, int i, TimeOnly? currentTime, int j)
    {
        // 1. Смотрим на следующую строку
        if (i + 1 < values.Count)
        {
            var nextRow = values[i + 1];
            var (nextTime, _) = GetTimeAndPairNumber(nextRow);
            
            if (nextTime != null && nextTime == currentTime)
            {
                // Сравниваем содержимое ячеек БЕЗОПАСНО
                var currentVal = (values[i].Count > j ? values[i][j] : null)?.ToString() ?? "";
                var nextVal = (nextRow.Count > j ? nextRow[j] : null)?.ToString() ?? "";
                
                if (currentVal == nextVal) return Evenness.Always; // Объединено визуально
                return Evenness.Odd; // Мы сверху -> Нечет
            }
        }
        
        // 2. Смотрим на предыдущую строку
        if (i - 1 >= 0)
        {
            var prevRow = values[i - 1];
            var (prevTime, _) = GetTimeAndPairNumber(prevRow);
            if (prevTime != null && prevTime == currentTime)
            {
                 return Evenness.Even; // Мы снизу -> Чет
            }
        }

        return Evenness.Always;
    }

    private static ParsedLessonInfo? GetCleanLessonInfo(string cell)
    {
        if (cell.Contains("Физкультура", StringComparison.InvariantCultureIgnoreCase))
            return new ParsedLessonInfo { SubjectName = "Физкультура" };

        var info = new ParsedLessonInfo();
        
        var clean = Regex.Replace(cell, @"[\u00A0\n\r]+", " ").Trim();
        clean = Regex.Replace(clean, @"\b(онлайн|углубл[её]нная группа)\b", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\b[сc]\s+\d{1,2}[:.]\d{2}\b.*$", "", RegexOptions.IgnoreCase);

        var roomMatch = Regex.Match(clean, @"\b\d{3}[а-яА-Я]?\b");
        if (roomMatch.Success)
        {
            info.ClassRoom = roomMatch.Value;
            clean = clean.Replace(roomMatch.Value, "").Trim();
        }

        string teacherPattern = @"[А-Я][а-яё-]+\s+[А-Я]\.?\s*[А-Я]\.?";
        var teacherMatch = Regex.Match(clean, teacherPattern);

        if (teacherMatch.Success)
        {
            info.TeacherName = teacherMatch.Value;
            clean = clean.Replace(teacherMatch.Value, "").Trim();
        }
        else
        {
            var parts = clean.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var lastWord = parts.Last();
                if (Regex.IsMatch(lastWord, @"^[А-Я][а-яё]{2,}$") && !lastWord.Contains("язык"))
                {
                    info.TeacherName = lastWord;
                    var lastIdx = clean.LastIndexOf(lastWord);
                    if (lastIdx > 0) clean = clean.Substring(0, lastIdx).Trim();
                }
            }
        }

        clean = clean.Trim(',', '.', ' ');
        clean = Regex.Replace(clean, @"\s*,\s*", ", ");
        
        if (string.IsNullOrWhiteSpace(clean)) return null;
        info.SubjectName = clean;

        if (info.SubjectName.Contains("Иностранный язык", StringComparison.InvariantCultureIgnoreCase))
        {
            info.SubjectName = "Иностранный язык";
            info.ClassRoom = null;
        }

        return info;
    }

    // --- ФИКС ПОДГРУПП (для пустых заголовков) ---
    private static Dictionary<int, int> BuildSubgroupMap(IList<object> subgroupsRow)
    {
        var map = new Dictionary<int, int>();
        for (var j = 2; j < subgroupsRow.Count; j++)
        {
            var val = (j < subgroupsRow.Count ? subgroupsRow[j] : null)?.ToString() ?? "";
            
            if (val.Contains("1")) map[j] = 1;
            else if (val.Contains("2")) map[j] = 2;
            else map[j] = (j % 2 == 0) ? 1 : 2; // Эвристика: четный индекс столбца = 1 пг, нечетный = 2
        }
        return map;
    }

    private static Dictionary<int, string> BuildGroupMap(IList<object> groupsRow)
    {
        var map = new Dictionary<int, string>();
        for (var j = 0; j < groupsRow.Count; j++)
        {
            var cell = (j < groupsRow.Count ? groupsRow[j] : null)?.ToString();
            if (!string.IsNullOrWhiteSpace(cell) && cell.Contains("МЕН"))
            {
                map[j] = Regex.Match(cell, @"МЕН-?\s*\d+").Value.Replace(" ", ""); 
            }
        }
        return map;
    }

    private static (TimeOnly?, int) GetTimeAndPairNumber(IList<object> row)
    {
        var timeCell = row.Count > 1 ? row[1]?.ToString() : "";
        var pairCell = row.Count > 0 ? row[0]?.ToString() : "";

        if (string.IsNullOrWhiteSpace(timeCell) && Regex.IsMatch(pairCell ?? "", @"\d{1,2}:\d{2}"))
            timeCell = pairCell;

        if (string.IsNullOrWhiteSpace(timeCell)) return (null, -1);

        int pairNum = -1;
        var roman = Regex.Match(timeCell + " " + pairCell, @"\b(I|II|III|IV|V|VI|VII)\b").Value;
        if (!string.IsNullOrEmpty(roman)) pairNum = ParseRomanNumeral(roman);

        var timeMatch = Regex.Match(timeCell, @"\d{1,2}:\d{2}");
        TimeOnly? t = null;
        if (timeMatch.Success)
        {
            TimeOnly.TryParse(timeMatch.Value, out var parsedT);
            t = parsedT; 
        };

        return (t, pairNum);
    }

    private static string? GetDayOfWeek(IList<object> row, string? current)
    {
        if (row.Count > 0 && !string.IsNullOrWhiteSpace(row[0]?.ToString())) 
        {
            var d = row[0].ToString();
            if (d.Length > 10) return current;
            return d;
        }
        return current;
    }

    // --- СТАРЫЕ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ (Без изменений) ---
    private static GridData GetGridData(string spreadsheetId, string range, SheetsService service)
    {
        var req = service.Spreadsheets.Get(spreadsheetId);
        req.Ranges = new[] { range };
        req.IncludeGridData = true;
        var res = req.Execute();
        return res.Sheets.FirstOrDefault()?.Data.FirstOrDefault() ?? new GridData();
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
            if (merge.StartRowIndex == null || merge.EndRowIndex == null ||
                merge.StartColumnIndex == null || merge.EndColumnIndex == null) continue;

            var startRow = (int)merge.StartRowIndex;
            var endRow = (int)merge.EndRowIndex;
            var startCol = (int)merge.StartColumnIndex;
            var endCol = (int)merge.EndColumnIndex;

            object? mergedValue = null;
            if (values.Count > startRow && values[startRow].Count > startCol)
                mergedValue = values[startRow][startCol];

            if (mergedValue != null)
            {
                for (var i = startRow; i < endRow; i++)
                {
                    while (values.Count <= i) values.Add(new List<object>());
                    for (var j = startCol; j < endCol; j++)
                    {
                        while (values[i].Count <= j) values[i].Add(null);
                        values[i][j] = mergedValue;
                    }
                }
            }
        }
        return values;
    }

    private static DayOfWeek? ParseDayOfWeek(string day) => day.ToUpper().Trim() switch
    {
        "ПН" => DayOfWeek.Monday, "ВТ" => DayOfWeek.Tuesday, "СР" => DayOfWeek.Wednesday,
        "ЧТ" => DayOfWeek.Thursday, "ПТ" => DayOfWeek.Friday, "СБ" => DayOfWeek.Saturday,
        "ВС" => DayOfWeek.Sunday, _ => null
    };

    private static int ParseRomanNumeral(string roman) => roman.ToUpper().Trim() switch
    {
        "I" => 1, "II" => 2, "III" => 3, "IV" => 4, "V" => 5, "VI" => 6, "VII" => 7, _ => -1
    };

    private static string GetLocationByColor(CellData cell)
    {
        var bg = cell?.EffectiveFormat?.BackgroundColor;
        if (bg == null) return "Тургенева, 4";
        bool Is(float? v, float t) => Math.Abs((v ?? 0) - t) < 0.05f;
        
        if (Is(bg.Red, 0.94f) && Is(bg.Green, 1f) && Is(bg.Blue, 0.68f)) return "Куйбышева, 48";
        if (Is(bg.Red, 0.89f) && Is(bg.Green, 0.93f) && Is(bg.Blue, 1f)) return "Онлайн";
        return "Тургенева, 4";
    }
}