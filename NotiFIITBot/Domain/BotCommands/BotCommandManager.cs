using NotiFIITBot.Metrics;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class BotCommandManager(IEnumerable<IBotCommand> commands, MetricsService metricsService)
{
    private readonly IEnumerable<IBotCommand> commands = commands;
    private readonly MetricsService metricsService = metricsService;
    
    public async Task<bool> TryExecuteCommand(Message message)
    {
        foreach (var command in commands)
        {
            if (command.CanRun(message))
            {
                await command.RunCommand(message);
                if (!command.IsAdminCommand)
                    metricsService.RecordRequest(message.Chat.Id, 
                        command.CommandName.Replace("/", ""), command.CommandName);
                return true;
            }
        }
        return false;
    }
}