using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class BotCommandManager(IEnumerable<IBotCommand> commands)
{
    private readonly IEnumerable<IBotCommand> commands = commands;

    public async Task<bool> TryExecuteCommand(Message message)
    {
        foreach (var command in commands)
        {
            if (command.CanRun(message))
            {
                await command.RunCommand(message);
                return true;
            }
        }
        return false;
    }
}