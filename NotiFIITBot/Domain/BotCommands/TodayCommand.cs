using NotiFIITBot.Consts;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

[KeyboardText("📅 Сегодня")]
public class TodayCommand(BotMessageService botService, ScheduleService scheduleService) : BaseCommand(botService)
{
    private readonly ScheduleService scheduleService = scheduleService;

    public override string CommandName => "/today";

    public override string Description => "Отправляет расписание на сегодня";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        var todaySched = await scheduleService.GetSchedForPeriodAsync(message.Chat.Id, SchedulePeriod.Today);
        await botService.SendMessage(
            message.Chat.Id,
            todaySched,
            useMainKeyboard: true
        );
    }
}
