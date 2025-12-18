using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot.Domain;

public interface IKeyboardService
{
    ReplyKeyboardMarkup MainKeyboard { get; }
}
