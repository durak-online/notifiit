using NotiFIITBot.Metrics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot.Domain.BotCommands;

public class BotCommandManager(IEnumerable<IBotCommand> commands, MetricsService metricsService)
{
    private readonly IEnumerable<IBotCommand> commands = commands;
    private readonly MetricsService metricsService = metricsService;
    
    
    public ReplyKeyboardMarkup GetMainKeyboard()
    {
        var today = GetButtonText("/today");
        var tmrw = GetButtonText("/tmrw");
        var week = GetButtonText("/week");
        var twoWeeks = GetButtonText("/2week");

        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { today, tmrw },      // 1 ряд
            new KeyboardButton[] { week, twoWeeks }    // 2 ряд
        })
        {
            ResizeKeyboard = true,
            IsPersistent = true
        };
    }

    private string GetButtonText(string commandName)
    {
        return commands.First(c => c.CommandName == commandName).ButtonName
               ?? throw new Exception($"Command {commandName} not found or has no button text");
    }
    
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