using Serilog;

namespace NotiFIITBot.Logging;

public class LoggerFactory(ILogger baseLogger) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName)
    {
        return baseLogger.ForContext("SourceContext", categoryName);
    }
}