using NotiFIITBot.Consts;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

[KeyboardText("🔜 Завтра")]
public class TomorrowCommand(BotMessageService botService, ScheduleService scheduleService) : BaseCommand(botService)
{
    private readonly ScheduleService scheduleService = scheduleService;

    public override string CommandName => "/tmrw";

    public override string Description => "Отправляет расписание на завтра";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        var tomorrowSched = await scheduleService.GetSchedForPeriodAsync(message.Chat.Id, SchedulePeriod.Tomorrow);
        await botService.SendMessage(
            message.Chat.Id,
            tomorrowSched,
            useMainKeyboard: true
        );
    }
}
