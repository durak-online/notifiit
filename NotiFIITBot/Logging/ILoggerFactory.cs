using Serilog;

namespace NotiFIITBot.Logging;

public interface ILoggerFactory
{
    ILogger CreateLogger(string categoryName);
}
