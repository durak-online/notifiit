using NotiFIITBot.Consts;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot.Domain.BotCommands;

public class SchedCommand(BotMessageService botService) : BaseCommand(botService)
{
    public override string Name => "/sched";

    public override string Description => "Отправляет сообщение с выбором периода расписания";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        var schedInlineMarkup = new InlineKeyboardMarkup()
            .AddButton("Сегодня", nameof(SchedulePeriod.Today))
            .AddButton("Завтра", nameof(SchedulePeriod.Tomorrow))
            .AddNewRow()
            .AddButton("Неделя", nameof(SchedulePeriod.Week))
            .AddButton("2 недели", nameof(SchedulePeriod.TwoWeeks));

        await botService.SendMessage(
            message.Chat.Id, 
            "Выбери какое расписание тебе нужно:", 
            replyMarkup: schedInlineMarkup
        );
    }
}
