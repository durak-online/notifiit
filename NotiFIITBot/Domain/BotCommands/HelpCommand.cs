using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class HelpCommand(BotMessageService botService, IServiceProvider serviceProvider) : BaseCommand(botService)
{
    private string commonHelpMessage;
    private string adminHelpMessage;

    private bool isComputed = false;

    public override string CommandName => "/help";

    public override string Description => "Пишет справку по всем командам";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        if (!isComputed)
            ComputeHelpMessages();

        var isAdmin = IsAdmin(message.From!);

        if (isAdmin)
            await botService.SendMessage(message.Chat.Id, adminHelpMessage);
        else
            await botService.SendMessage(message.Chat.Id, commonHelpMessage);
    }

    private void ComputeHelpMessages()
    {
        var commands = serviceProvider.GetRequiredService<IEnumerable<IBotCommand>>();

        var commonCommands = commands
            .Where(c => !c.IsAdminCommand)
            .OrderBy(c => c.CommandName);
        var adminCommands = commands
            .Where(c => c.IsAdminCommand)
            .OrderBy(c => c.CommandName);

        var strBuilder = new StringBuilder();
        strBuilder.Append("<b>Доступные команды:</b>\n\n");


        foreach (var command in commonCommands)
        {
            if (command.CommandName != "" && command.Description != "")
                strBuilder.Append($"• {command.CommandName} - {command.Description}\n");
        }
        commonHelpMessage = strBuilder.ToString();

        strBuilder.Append("\n<b>АДМИНСКИЕ КОМАНДЫ</b>\n\n");
        foreach (var command in adminCommands)
        {
            if (command.CommandName != "" && command.Description != "")
                strBuilder.Append($"• {command.CommandName} - {command.Description}\n");
        }
        adminHelpMessage = strBuilder.ToString();

        isComputed = true;
    }
}
