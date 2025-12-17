using NotiFIITBot.Consts;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public abstract class BaseCommand(BotMessageService botService) : IBotCommand
{
    protected readonly BotMessageService botService = botService;

    public abstract string CommandName { get; }
    public virtual string? ButtonName => null;

    public abstract string Description { get; }
    public abstract bool IsAdminCommand { get; }

    public abstract Task RunCommand(Message message);

    public virtual bool CanRun(Message message)
    {
        return message.Text != null && (message.Text.Trim() == CommandName || message.Text.Trim() == ButtonName);
    }

    protected static bool IsAdmin(User user)
    {
        return AdminsConfig.AdminIds.Contains(user.Id);
    }
}
