using NotiFIITBot.Consts;
using System.Collections.Concurrent;
using System.Reflection;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public abstract class BaseCommand(BotMessageService botService) : IBotCommand
{
    protected readonly BotMessageService botService = botService;
    private static readonly ConcurrentDictionary<Type, string?> buttonNameCache = new();

    public abstract string CommandName { get; }
    public abstract string Description { get; }
    public abstract bool IsAdminCommand { get; }

    public abstract Task RunCommand(Message message);

    public virtual bool CanRun(Message message)
    {
        if (message.Text == null)
            return false;
        var text = message.Text.Trim();
        return text == CommandName || text == GetButtonName();
    }

    protected static bool IsAdmin(User user)
    {
        return AdminsConfig.AdminIds.Contains(user.Id);
    }

    protected string? GetButtonName()
    {
        return buttonNameCache.GetOrAdd(GetType(), type =>
        {
            return type.GetCustomAttribute<KeyboardTextAttribute>()
            ?.ButtonName;
        });
    }
}
