using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot.Domain;

public class BotMessageService(ITelegramBotClient botClient, CancellationTokenSource cts,
    IKeyboardService keyboardService)
{
    private readonly ITelegramBotClient botClient = botClient;
    private readonly CancellationTokenSource cts = cts;
    private readonly IKeyboardService keyboardService = keyboardService;

    private readonly ReplyKeyboardRemove keyboardRemove = new();

    public async Task SendMessage(long id, string message, ReplyMarkup? replyMarkup)
    {
        replyMarkup ??= keyboardRemove;
        await botClient.SendMessage(id, message, ParseMode.Html,
            replyMarkup: replyMarkup, cancellationToken: cts.Token);
    }

    public async Task SendMessage(long id, string message, bool useMainKeyboard = false)
    {
        var replyMarkup = useMainKeyboard ? (ReplyMarkup)keyboardService.MainKeyboard : keyboardRemove;
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

    public async Task<User> GetBotInfo(CancellationToken cancellationToken)
    {
        return await botClient.GetMe(cancellationToken: cancellationToken);
    }

    public ITelegramBotClient GetTelegramBot()
    {
        return botClient;
    }
}
