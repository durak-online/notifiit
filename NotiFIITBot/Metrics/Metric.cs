namespace NotiFIITBot.Metrics;

public class UserRequestMetric
{
    public DateTime Timestamp { get; set; }
    public long UserId { get; set; }
    public string RequestType { get; set; }
    public string? Command { get; set; }
}

public class UserActivity
{
    public long UserId { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int RequestCount { get; set; }
    public HashSet<string> RequestTypes { get; set; } = new();
}