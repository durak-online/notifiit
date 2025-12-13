using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot.Domain;

public class BotMessageService(ITelegramBotClient botClient, CancellationTokenSource cts)
{
    private readonly ITelegramBotClient botClient = botClient;
    private readonly CancellationTokenSource cts = cts;

    public async Task SendMessage(long id, string message, ReplyMarkup? replyMarkup = null)
    {
        await botClient.SendMessage(id, message, ParseMode.Html,
            replyMarkup: replyMarkup, cancellationToken: cts.Token);
    }

    public async Task SendEmoji(long id, string emoji)
    {
        await botClient.SendDice(id, emoji);
    }

    public async Task EditMessage(Chat chat, int messageId, string newMessage)
    {
        await botClient.EditMessageText(
            chat,
            messageId,
            newMessage,
            ParseMode.Html,
            cancellationToken: cts.Token
        );
    }

    public async Task AnswerCallbackQuery(CallbackQuery callbackQuery)
    {
        await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cts.Token);
    }

    public async Task<User> GetBotInfo()
    {
        return await botClient.GetMe();
    }

    public ITelegramBotClient GetTelegramBot()
    {
        return botClient;
    }
}
