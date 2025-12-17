using NotiFIITBot.Repo;
using Telegram.Bot.Types;

namespace NotiFIITBot.Domain.BotCommands;

public class DeleteCommand(BotMessageService botService, IUserRepository userRepository) : BaseCommand(botService)
{
    private readonly IUserRepository userRepository = userRepository;

    public override string CommandName => "/delete";
    
    public override string Description => "Удалить юзера с данным tg_id из базы данных";

    public override bool IsAdminCommand => true;

    public override async Task RunCommand(Message message)
    {
        if (long.TryParse(message.Text!.Split()[1], out var userToDeleteId))
        {
            await userRepository.DeleteUserAsync(userToDeleteId);
            await botService.SendMessage(
                message.Chat.Id,
                "Юзер был успешно удален из базы данных"
            );
        }
        else
        {
            await botService.SendMessage(
                message.Chat.Id,
                "Юзер не был удален из базы данных, неверно введен ID.\n" +
                "Допускаются только цифры, нужен tg ID юзера"
            );
        }
    }

    public override bool CanRun(Message message)
    {
        if (IsAdmin(message.From!) && message.Text != null)
        {
            if (message.Text!.StartsWith(CommandName) && message.Text!.Split().Length == 2)
                return true;
            if (message.Text!.StartsWith(CommandName)) // подсказка
                Task.Run(() => botService.SendMessage(message.Chat.Id, "Команда используется как /delete *tg ID*"));
        }
        return false;
    }
}
