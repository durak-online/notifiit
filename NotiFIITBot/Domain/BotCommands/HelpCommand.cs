using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class HelpCommand(BotMessageService botService, Lazy<IEnumerable<IBotCommand>> commands) : BaseCommand(botService)
{
    private readonly Lazy<IEnumerable<IBotCommand>> commands = commands;

    public override string Name => "/help";

    public override string Description => "Пишет справку по всем командам";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        var helpText = "<b>Доступные команды:</b>\n\n";

        var isAdmin = IsAdmin(message.From!);

        foreach (var command in commands.Value)
        {
            if (command.IsAdminCommand && !isAdmin)
                continue;
            if (command.Name != "" && command.Description != "")
                helpText += $"• {command.Name} - {command.Description}\n";
        }

        await botService.SendMessage(message.Chat.Id, helpText);
    }
}
