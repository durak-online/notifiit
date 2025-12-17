using NotiFIITBot.Consts;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class WeekCommand(BotMessageService botService, ScheduleService scheduleService) : BaseCommand(botService)
{
    private readonly ScheduleService scheduleService = scheduleService;

    public override string CommandName => "/week";
    
    public override string ButtonName => "🗓️ Текущая неделя";

    public override string Description => "Отправляет расписание на текущую неделю";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        var weekSched = await scheduleService.GetSchedForPeriodAsync(message.Chat.Id, SchedulePeriod.Week);
        await botService.SendMessage(
            message.Chat.Id,
            weekSched
        );
    }
}
