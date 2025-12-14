using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public interface IBotCommand
{
    string Name { get; }
    string Description { get; }
    bool IsAdminCommand { get; }

    Task RunCommand(Message message);
    bool CanRun(Message message);
}
