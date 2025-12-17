using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class StartCommand(BotMessageService botService, RegistrationService registrationService) : BaseCommand(botService)
{
    private readonly RegistrationService registrationService = registrationService;

    public override string CommandName => "/start";

    public override string Description => "Стартовая команда, отправляет сообщение о регистрации";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        await botService.SendMessage(
            message.Chat.Id,
            "Добро пожаловать! Напиши свою группу в формате МЕН-группа-подгруппа для регистрации. " +
            "Например: <b>МЕН-240801-1</b>"
        );

        registrationService.AddUser(message.Chat.Id);
    }
}
