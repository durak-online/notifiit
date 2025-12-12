using System.Globalization;
using System.Text;
using NotiFIITBot.Logging;
using Quartz;
using Serilog;


namespace NotiFIITBot.Metrics;

public class MetricsReporter
{
    private readonly string metricsDirectory;
    private readonly char separator = ';';
    private readonly ILogger logger;
    
    public MetricsReporter(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger("Metrics");
        metricsDirectory = Path.Combine(AppContext.BaseDirectory, "metrics");
        Directory.CreateDirectory(metricsDirectory);
        logger.Information("Metrics reporter initialized. Directory: {Directory}", metricsDirectory);
    }
    
    public void GenerateWeeklyReport()
    {
        try
        {
            var reportDate = DateTime.Now.AddDays(-7); 
            var weekStart = GetWeekStart(reportDate);
            
            GenerateReportForWeek(weekStart);
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Error generating weekly report");
        }
    }
    
    /// <summary>
    /// Генерирует отчет за конкретную неделю (понедельник которой указан)
    /// </summary>
    /// <param name="weekStart">Дата понедельника начала недели</param>
    public void GenerateReportForWeek(DateTime weekStart)
    {
        try
        {
            var weekEnd = weekStart.AddDays(7);
            
            logger.Information($"Generating weekly report for {weekStart:yyyy-MM-dd} - {weekEnd:yyyy-MM-dd}");
            
            var allRequests = LoadWeeklyRequests(weekStart, weekEnd);
            
            if (!allRequests.Any())
            {
                logger.Information("No data for weekly report");
                return;
            }
            
            var report = CreateWeeklyReport(allRequests, weekStart, weekEnd);
            
            SaveReport(report, weekStart);
            
            logger.Information($"Weekly report saved: {report.TotalRequests} requests, {report.UniqueUsers} users");
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Error generating report for week starting {weekStart:yyyy-MM-dd}");
        }
    }
    
    public void GenerateAllReports()
    {
        logger.Information("Generating reports for all available weeks");
        
        var dailyFiles = Directory.GetFiles(metricsDirectory, "requests_*.csv");
        var dates = dailyFiles
            .Select(f => Path.GetFileName(f).Replace("requests_", "").Replace(".csv", ""))
            .Where(d => DateTime.TryParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .Select(d => DateTime.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture))
            .OrderBy(d => d)
            .ToList();
        
        if (!dates.Any())
        {
            logger.Information("No daily files found");
            return;
        }
        
        var earliestDate = dates.First();
        var latestDate = dates.Last();
        var currentWeekStart = GetWeekStart(earliestDate);
        var latestWeekStart = GetWeekStart(latestDate);
        
        while (currentWeekStart <= latestWeekStart)
        {
            GenerateReportForWeek(currentWeekStart);
            currentWeekStart = currentWeekStart.AddDays(7);
        }
    }
    
    private List<UserRequestMetric> LoadWeeklyRequests(DateTime weekStart, DateTime weekEnd)
    {
        var allRequests = new List<UserRequestMetric>();
        
        for (var date = weekStart; date < weekEnd; date = date.AddDays(1))
        {
            var dailyFile = Path.Combine(metricsDirectory, $"requests_{date:yyyy-MM-dd}.csv");
            
            if (File.Exists(dailyFile))
            {
                var dailyRequests = LoadDailyMetrics(dailyFile);
                allRequests.AddRange(dailyRequests);
            }
        }
        
        return allRequests;
    }
    
    private List<UserRequestMetric> LoadDailyMetrics(string filePath)
    {
        var requests = new List<UserRequestMetric>();
        
        try
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                var fields = ParseCsvLine(line);
                
                if (fields.Count >= 4)
                {
                    try
                    {
                        var request = new UserRequestMetric
                        {
                            Timestamp = DateTime.Parse(fields[0]),
                            UserId = long.Parse(fields[1]),
                            RequestType = fields[2],
                            Command = fields[3] == string.Empty ? null : fields[3],
                        };
                        requests.Add(request);
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"Failed to parse line {i} in {filePath}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Error loading metrics from {filePath}");
        }
        
        return requests;
    }
    
    private List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    currentField.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == separator && !inQuotes)
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        fields.Add(currentField.ToString());
        return fields;
    }
    
    private WeeklyReport CreateWeeklyReport(List<UserRequestMetric> requests, DateTime weekStart, DateTime weekEnd)
    {
        var report = new WeeklyReport
        {
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            TotalRequests = requests.Count,
            UniqueUsers = requests.Select(r => r.UserId).Distinct().Count()
        };
        
        var previousWeekStart = weekStart.AddDays(-7);
        var previousWeekUsers = GetUniqueUsersForWeek(previousWeekStart);
        var currentWeekUsers = requests.Select(r => r.UserId).Distinct().ToHashSet();
        
        if (previousWeekUsers.Any())
        {
            var retainedUsers = currentWeekUsers.Intersect(previousWeekUsers).Count();
            report.UserRetentionRate = (double)retainedUsers / previousWeekUsers.Count * 100;
            report.RetainedUsers = retainedUsers;
            report.NewUsers = currentWeekUsers.Count - retainedUsers;
        }
        else
        {
            report.NewUsers = currentWeekUsers.Count;
        }
        
        // конкретные запросы 
        report.RequestsByType = requests
            .GroupBy(r => r.RequestType)
            .ToDictionary(g => g.Key, g => g.Count());
        
        // по командам
        report.PopularCommands = requests
            .Where(r => !string.IsNullOrEmpty(r.Command))
            .GroupBy(r => r.Command)
            .ToDictionary(g => g.Key!, g => g.Count())
            .OrderByDescending(x => x.Value)
            .ToDictionary(x => x.Key, x => x.Value);
        
        return report;
    }
    
    private HashSet<long> GetUniqueUsersForWeek(DateTime weekStart)
    {
        var weekEnd = weekStart.AddDays(7);
        var users = new HashSet<long>();
        
        for (var date = weekStart; date < weekEnd; date = date.AddDays(1))
        {
            var dailyFile = Path.Combine(metricsDirectory, $"requests_{date:yyyy-MM-dd}.csv");
            if (File.Exists(dailyFile))
            {
                var dailyRequests = LoadDailyMetrics(dailyFile);
                foreach (var request in dailyRequests)
                {
                    users.Add(request.UserId);
                }
            }
        }
        
        return users;
    }
    
    private DateTime GetWeekStart(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }
    
    private void SaveReport(WeeklyReport report, DateTime weekStart)
    {
        var reportFile = Path.Combine(metricsDirectory, $"weekly_report_{weekStart:yyyy-MM-dd}.csv");
        
        try
        {
            using (var writer = new StreamWriter(reportFile, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("ОТЧЕТ О РАБОТЕ БОТА ЗА НЕДЕЛЮ");
                writer.WriteLine($"{report.WeekStart:dd.MM.yyyy} - {report.WeekEnd:dd.MM.yyyy}");
                writer.WriteLine();
                
                writer.WriteLine($"Метрика{separator}Значение");
                writer.WriteLine($"Количество запросов расписания{separator}{report.TotalRequests}");
                writer.WriteLine();
                
                writer.WriteLine($"Уникальных пользователей за неделю{separator}{report.UniqueUsers}");
                writer.WriteLine($"Новых пользователей{separator}{report.NewUsers}");
                
                if (report.RetainedUsers.HasValue)
                {
                    writer.WriteLine($"Вернувшихся пользователей{separator}{report.RetainedUsers}");
                    writer.WriteLine($"Процент удержания{separator}{report.UserRetentionRate:F1}%");
                }
                writer.WriteLine();
                
                writer.WriteLine("Распределение по типам");
                writer.WriteLine($"Тип запроса{separator}Количество{separator}Процент");
                
                foreach (var kvp in report.RequestsByType.OrderByDescending(x => x.Value))
                {
                    var percentage = (double)kvp.Value / report.TotalRequests * 100;
                    var requestTypeName = GetRequestTypeName(kvp.Key);
                    writer.WriteLine($"{requestTypeName}{separator}{kvp.Value}{separator}{percentage:F1}%");
                }
                writer.WriteLine();
                

                if (report.PopularCommands.Any())
                {
                    writer.WriteLine("Популярные команды");
                    writer.WriteLine($"Команда{separator}Количество использований{separator}Процент");
                    
                    foreach (var kvp in report.PopularCommands.Take(10))
                    {
                        var percentage = (double)kvp.Value / report.TotalRequests * 100;
                        writer.WriteLine($"{kvp.Key}{separator}{kvp.Value}{separator}{percentage:F1}%");
                    }
                }
            }
            
            logger.Information($"Report saved to: {reportFile}");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to save report");
        }
    }
    
    private string GetRequestTypeName(string requestType)
    {
        return requestType switch
        {
            "Today" => "Расписание на сегодня",
            "Tomorrow" => "Расписание на завтра",
            "Week" => "Расписание на неделю",
            "TwoWeeks" => "Расписание на 2 недели",
            "Menu" => "Меню расписания",
            "Inline" => "Встроенная клавиатура",
            "Start" => "Старт",
            "Help" => "Помощь",
            "Slots" => "Игровые слоты",
            "Unknown" => "Неизвестная команда",
            _ => requestType
        };
    }
}

public class WeeklyReport
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public int TotalRequests { get; set; }                 // Ключевая метрика
    public int UniqueUsers { get; set; }                   
    public double UserRetentionRate { get; set; }          
    public int? RetainedUsers { get; set; }                // Количество вернувшихся пользователей
    public int NewUsers { get; set; }                      
    public Dictionary<string, int> RequestsByType { get; set; } = new(); // Распределение по типам запросов
    public Dictionary<string, int> PopularCommands { get; set; } = new(); // Популярные команды
}

public class WeeklyMetricsJob : IJob
{
    private readonly MetricsReporter metricsReporter;
    private readonly ILogger logger;

    public WeeklyMetricsJob(MetricsReporter metricsReporter, ILoggerFactory logger)
    {
        this.metricsReporter = metricsReporter;
        this.logger = logger.CreateLogger("Metrics");
    }
    public Task Execute(IJobExecutionContext context)
    {
        try
        {
            logger.Information("Starting weekly metrics report generation...");
            metricsReporter.GenerateWeeklyReport();
            logger.Information("Weekly metrics report generated successfully");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error executing weekly metrics report job");
        }
        
        return Task.CompletedTask;
    }
}

public class InitialMetricsJob : IJob
{
    private readonly MetricsReporter metricsReporter;
    private readonly ILogger logger;

    public InitialMetricsJob(MetricsReporter metricsReporter,  ILoggerFactory logger)
    {
        this.metricsReporter = metricsReporter;
        this.logger = logger.CreateLogger("Metrics");
    }
    public Task Execute(IJobExecutionContext context)
    {
        try
        {
            logger.Information("Starting initial generation of all metrics reports...");
            metricsReporter.GenerateAllReports();
            logger.Information("All metrics reports generated successfully");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error executing initial metrics reports job");
        }
        
        return Task.CompletedTask;
    }
}