using NotiFIITBot.Repo;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class StartCommand(
    BotMessageService botService, 
    RegistrationService registrationService,
    IKeyboardService keyboardService,
    IScheduleRepository scheduleRepo) : BaseCommand(botService)
{
    private readonly RegistrationService registrationService = registrationService;

    public override string CommandName => "/start";

    public override string Description => "Стартовая команда, отправляет сообщение о регистрации";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        var courses = await scheduleRepo.GetAvailableCoursesAsync();

        if (courses.Count == 0)
        {
            await botService.SendMessage(message.Chat.Id, "В базе пока нет расписания, регистрация невозможна.");
            return;
        }

        registrationService.StartRegSession(message.Chat.Id);

        var keyboard = keyboardService.CreateGridKeyboard(courses.Select(c => c.ToString()).ToList(), 2);
        
        await botService.SendMessage(
            message.Chat.Id, 
            "Добро пожаловать! Выбери свой курс:", 
            keyboard
        );
    }
}
