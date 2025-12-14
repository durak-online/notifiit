using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NotiFIITBot.Domain.BotCommands;

public class SlotsCommand(BotMessageService botService) : BaseCommand(botService)
{
    // делаем так, чтобы команда была скрытой и
    // не высвечивалась в help
    public override string Name => "";

    public override string Description => "";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        await botService.SendMessage(message.Chat.Id, "Додепчик пошел");
        await botService.SendEmoji(
            message.Chat.Id,
            DiceEmoji.SlotMachine
        );
    }

    public override bool CanRun(Message message)
    {
        return message.Text != null && message.Text.Contains("/slots");
    }
}
