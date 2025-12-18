using NotiFIITBot.Consts;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

[KeyboardText("🔭 2 Недели")]
public class TwoWeekCommand(BotMessageService botService, ScheduleService scheduleService) : BaseCommand(botService)
{
    private readonly ScheduleService scheduleService = scheduleService;

    public override string CommandName => "/2week";

    public override string Description => "Отправляет расписание на текущую и следующую неделю";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        var twoWeekSched = await scheduleService.GetSchedForPeriodAsync(message.Chat.Id, SchedulePeriod.NextWeek);
        await botService.SendMessage(
            message.Chat.Id,
            twoWeekSched,
            useMainKeyboard: true
        );
    }
}
