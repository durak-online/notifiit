using dotenv.net;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace NotiFIITBot;

public class Bot
{
    private TelegramBotClient bot;
    private readonly string token;
    private readonly long creatorId;
    private CancellationTokenSource cts;

    public Bot()
    {

        DotEnv.Load(options: new DotEnvOptions(ignoreExceptions: false));
        var variables = DotEnv.Read();
        token = variables["TELEGRAM_BOT_TOKEN"];
        creatorId = long.Parse(variables["CREATOR_ID"]);
    }

    public async Task Initialize(CancellationTokenSource cts)
    {
        try
        {
            this.cts = cts;
            bot = new TelegramBotClient(token: token);

            var info = await bot.GetMe();
            Log.Information($"Bot {info.Username} started to work");

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message],
                DropPendingUpdates = true
            };

            bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: this.cts.Token
            );

            Log.Information("Bot is now receiving updates...");
        }
        catch (Exception ex)
        {
            Log.Fatal($"Can't initialize bot: {ex}");
            throw;
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update)
            {
                case { Message: { } message }:
                    Log.Information($"Received message from {message.From?.Username ?? "unknown"}");
                    await HandleMessage(message);
                    break;
                default:
                    Log.Information($"Received {update.Type} update type");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Exception while handling update: {ex}");
        }
    }

    private async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            Log.Error($"Telegram Bot Error: {exception}");

            if (exception is ApiRequestException apiException && apiException.ErrorCode == 401)
            {
                Log.Fatal("Invalid token, stopping bot...");
                cts.Cancel();
            }
        }, cancellationToken);
    }

    private async Task HandleMessage(Message message)
    {
        if (message.Text != null)
            await AnswerOnMessage(message);
        else
            await bot.SendMessage(
                message.Chat.Id,
                "Я не понял о чем ты...\nНапиши /help, если не знаешь с чего начать!");
    }

    private async Task AnswerOnMessage(Message message)
    {
        if (message.Text != null)
            switch (message.Text.Split()[0])
            {
                case "/start":
                    await bot.SendMessage(
                        message.Chat.Id,
                        "Добро пожаловать! Посмотри список доступных команд в <b>Меню</b>",
                        parseMode: ParseMode.Html);
                    break;

                case "/help":
                    await SendHelpMessage(message);
                    break;

                case "/stop" when IsAdmin(message.From):
                    await bot.SendMessage(message.Chat.Id, "Останавливаю бота...");
                    Log.Information($"Stopped by {message.From.Username}");
                    cts.Cancel();
                    break;

                default:
                    await bot.SendMessage(
                        message.Chat.Id,
                        "Я не понял о чем ты...\nНапиши /help, если не знаешь с чего начать!");
                    break;
            }
    }

    private bool IsAdmin(User user)
    {
        // durak online ID
        // more admins later
        return user.Id == creatorId;
    }

    private async Task SendHelpMessage(Message message)
    {
        var answer = "Это бот для отправки расписания. Пока тут немного возможностей, но попробуй что-то из <b>Меню</b>";
        await bot.SendMessage(message.Chat.Id, answer, parseMode: ParseMode.Html);
    }
}