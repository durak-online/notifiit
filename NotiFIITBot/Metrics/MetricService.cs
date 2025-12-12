using System.Globalization;
using NotiFIITBot.Logging;
using Serilog;

namespace NotiFIITBot.Metrics;

public class MetricsService : IDisposable
{
    private readonly string metricsDirectory;
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<long, UserActivity> activeUsers = new();
    private readonly Dictionary<string, int> weeklyRequests = new();
    private DateTime currentWeekStart;
    private readonly char separator = ';';
    private readonly ILogger logger;


    public MetricsService(ILoggerFactory loggerFactory)
    {
        logger = loggerFactory.CreateLogger("Metrics");  
        metricsDirectory = Path.Combine(AppContext.BaseDirectory, "metrics");
        Directory.CreateDirectory(metricsDirectory);
        logger.Information("Metrics service initialized. Directory: {Directory}", metricsDirectory); 
        
        currentWeekStart = GetWeekStart(DateTime.UtcNow);
        LoadExistingData();
        
        //периодическое сохранение?
        _ = Task.Run(PeriodicSave);
    }
    
    private DateTime GetWeekStart(DateTime date)
    {
        var diff = date.DayOfWeek - CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        if (diff < 0) diff += 7;
        return date.AddDays(-diff).Date;
    }
    
    private void LoadExistingData()
    {
        try
        {
            var currentWeekFile = GetWeeklyMetricsFilePath(currentWeekStart);
            if (File.Exists(currentWeekFile))
            {
                var lines = File.ReadAllLines(currentWeekFile);
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    var parts = ParseCsvLine(line);
                    if (parts.Count >= 3)
                    {
                        var key = $"{parts[0]}{separator}{parts[1]}";
                        if (int.TryParse(parts[2], out int count))
                        {
                            weeklyRequests[key] = count;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to load metrics data");
        }
    }
    
    private List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new System.Text.StringBuilder();
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
    
    public void RecordRequest(long userId, string requestType, string? command = null)
    {
        var now = DateTime.UtcNow;
        var weekStart = GetWeekStart(now);
        
        _lock.EnterWriteLock();
        try
        {
            if (weekStart != currentWeekStart)
            {
                SaveWeeklyMetrics();
                weeklyRequests.Clear();
                currentWeekStart = weekStart;
            }
            
            var requestKey = $"{now:yyyy-MM-dd}{separator}{requestType}";
            weeklyRequests[requestKey] = weeklyRequests.GetValueOrDefault(requestKey) + 1;
            
            if (!activeUsers.TryGetValue(userId, out var activity))
            {
                activity = new UserActivity
                {
                    UserId = userId,
                    FirstSeen = now,
                    LastSeen = now,
                    RequestCount = 0
                };
                activeUsers[userId] = activity;
            }
            
            activity.LastSeen = now;
            activity.RequestCount++;
            activity.RequestTypes.Add(requestType);
            
            var request = new UserRequestMetric
            {
                Timestamp = now,
                UserId = userId,
                RequestType = requestType,
                Command = command,
            };
            
            AppendRequest(request);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
    
    private void AppendRequest(UserRequestMetric request)
    {
        var dailyFile = Path.Combine(metricsDirectory, $"requests_{DateTime.UtcNow:yyyy-MM-dd}.csv");
        
        try
        {
            var line = $"{request.Timestamp:O}{separator}{request.UserId}{separator}{request.RequestType}{separator}{ToCsv(request.Command)}";
            
            using (var stream = new FileStream(dailyFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream))
            {
                if (stream.Length == 0)
                {
                    writer.WriteLine($"Timestamp{separator}UserId{separator}RequestType{separator}Command");
                }
                writer.WriteLine(line);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to write detailed metrics request");
            logger.Error(ex, "Failed to write detailed metrics request");
        }
    }
    
    private void SaveWeeklyMetrics()
    {
        var filePath = GetWeeklyMetricsFilePath(currentWeekStart);
        
        try
        {
            var lines = new List<string> { $"Date{separator}RequestType{separator}Count" };
            foreach (var kvp in weeklyRequests.OrderBy(x => x.Key))
            {
                lines.Add($"{kvp.Key}{separator}{kvp.Value}");
            }
            
            File.WriteAllLines(filePath, lines);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to save weekly metrics");
        }
    }
    
    private string GetWeeklyMetricsFilePath(DateTime weekStart)
    {
        return Path.Combine(metricsDirectory, $"weekly_{weekStart:yyyy-MM-dd}.csv");
    }
    
    private async Task PeriodicSave()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            
            _lock.EnterReadLock();
            try
            {
                SaveWeeklyMetrics();
                
                var activityFile = Path.Combine(metricsDirectory, $"user_activity_{DateTime.UtcNow:yyyy-MM-dd}.csv");
                var lines = new List<string> { $"UserId{separator}FirstSeen{separator}LastSeen{separator}RequestCount{separator}RequestTypes" };
                
                foreach (var kvp in activeUsers)
                {
                    var types = string.Join(";", kvp.Value.RequestTypes);
                    lines.Add($"{kvp.Key}{separator}{kvp.Value.FirstSeen:O}{separator}{kvp.Value.LastSeen:O}{separator}{kvp.Value.RequestCount}{separator}{ToCsv(types)}");
                }
                
                File.WriteAllLines(activityFile, lines);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
    
    private string ToCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        
        if (value.Contains(separator) || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
    
    public void Dispose()
    {
        SaveWeeklyMetrics();
        _lock.Dispose();
    }
    
}