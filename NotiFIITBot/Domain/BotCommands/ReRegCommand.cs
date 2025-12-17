using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot.Domain.BotCommands;

public class ReRegCommand(BotMessageService botService, RegistrationService registrationService) : BaseCommand(botService)
{
    private readonly RegistrationService registrationService = registrationService;

    public override string CommandName => "/rereg";

    public override string Description => "Изменяет твою МЕН группу и подгруппу";

    public override bool IsAdminCommand => false;

    public override async Task RunCommand(Message message)
    {
        await botService.SendMessage(
            message.Chat.Id,
            "Меняем твою группу! Напиши свою группу в формате МЕН-группа-подгруппа для регистрации. " +
            "Например <b>МЕН-240801-1</b>",
            replyMarkup: new ReplyKeyboardRemove()
        );

        registrationService.AddUser(message.Chat.Id);
    }
}
