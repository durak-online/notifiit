using NotiFIITBot.Logging;
using Serilog;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class StopCommand(BotMessageService botService, CancellationTokenSource cts, ILoggerFactory loggerFactory) : BaseCommand(botService)
{
    private readonly CancellationTokenSource cts = cts;
    private readonly ILogger logger = loggerFactory.CreateLogger(nameof(StopCommand));

    public override string CommandName => "/stop";

    public override string Description => "Останавливает бота и всё приложение в целом";

    public override bool IsAdminCommand => true;

    public override async Task RunCommand(Message message)
    {
        await botService.SendMessage(message.Chat.Id, "Останавливаю бота...");
        logger.Information($"Stopped by {message.From!}");
        cts.Cancel();
    }

    public override bool CanRun(Message message)
    {
        return IsAdmin(message.From!) && base.CanRun(message);
    }
}
