namespace NotiFIITBot.Metrics;

public class UserRequestMetric
{
    public DateTime Timestamp { get; set; }
    public long UserId { get; set; }
    public string RequestType { get; set; }
    public string? Command { get; set; }
}