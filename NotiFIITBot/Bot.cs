using dotenv.net;
using Serilog;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot;

public class Bot : IDisposable
{
    private TelegramBotClient bot;
    private readonly string token;
    public readonly long CreatorId;
    private CancellationTokenSource cts;
    private HttpClient httpClient;

    public Bot()
    {
        DotEnv.Load(new DotEnvOptions(false));
        var variables = DotEnv.Read();
        token = variables["TELEGRAM_BOT_TOKEN"];
        CreatorId = long.Parse(variables["CREATOR_ID"]);
    }

    public async Task Initialize(CancellationTokenSource cts)
    {
        try
        {
            this.cts = cts;
            var proxy = new WebProxy("http://147.75.101.247:9443");

            var httpClient = new HttpClient(new HttpClientHandler()
            {
                Proxy = proxy,
                UseProxy = true
            });
            this.httpClient = httpClient;

            bot = new TelegramBotClient(token: token, httpClient: httpClient);
            //bot = new TelegramBotClient(token: token);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var info = await bot.GetMe(timeoutCts.Token);
            Log.Information($"Bot {info.Username} started to work");

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
                DropPendingUpdates = true
            };

            bot.StartReceiving(
                HandleUpdate,
                HandlePollingError,
                receiverOptions,
                this.cts.Token
            );

            Log.Information("Bot is now receiving updates...");
        }
        catch (Exception ex)
        {
            Log.Fatal($"Can't initialize bot: {ex}");
            throw;
        }
    }

    public async Task SendNotifitation(string message, params long[] chatIds)
    {
        var successful = 0;
        foreach (var id in chatIds)
        {
            try
            {
                await bot.SendMessage(id, message, ParseMode.Html);
                successful++;
            }
            catch
            {
                continue;
            }
        }
            
        Log.Information($"Sent notifitation to {successful} out of {chatIds.Length} users");
    }

    private async Task HandleUpdate(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update)
            {
                case { Message: { } message }:
                    Log.Information($"Received message from {message.From?.Username ?? "unknown"}");
                    await HandleMessage(message);
                    break;

                case { CallbackQuery: { } cbQuery }:
                    Log.Information($"Callback query from {cbQuery.From.Username}");
                    await HandleCallbackQuery(cbQuery);
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

    private async Task HandleCallbackQuery(CallbackQuery cbQuery)
    {
        var sched = cbQuery.Data switch
        {
            "today" => "Тест: расписание на сегодня",
            "tomorrow" => "Тест: расписание на завтра",
            "week" => "Тест: расписание на неделю",
            "2week" => "Тест: расписание на 2 недели",
            _ => null
        };

        if (sched == null)
        {
            Log.Error($"Can't handle CallbackQuery with message {cbQuery.Message}");
            sched = "Произошла ошибка";
        }


        await bot.EditMessageText(
            cbQuery.Message!.Chat,
            cbQuery.Message.Id,
            sched
        );
        await bot.AnswerCallbackQuery(cbQuery.Id);
    }

    private async Task HandlePollingError(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
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
                        ParseMode.Html);
                    break;

                case "/sched":
                    await AskSchedule(message);
                    break;

                case "/slots":
                    await bot.SendMessage(
                        message.Chat.Id,
                        "Додепчик пошел"
                    );
                    await bot.SendDice(
                        message.Chat.Id,
                        "🎰"
                    );
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

    private async Task AskSchedule(Message message)
    {
        var schedInlineMarkup = new InlineKeyboardMarkup()
            .AddButton("Сегодня", "today")
            .AddButton("Завтра", "tomorrow")
            .AddNewRow()
            .AddButton("Неделя", "week")
            .AddButton("2 недели", "2week");
        await bot.SendMessage(message.Chat, "Выбери какое расписание тебе нужно:", replyMarkup: schedInlineMarkup);
    }

    private bool IsAdmin(User user)
    {
        // durak online ID
        // more admins later
        return user.Id == CreatorId;
    }

    private async Task SendHelpMessage(Message message)
    {
        var answer =
            "Это бот для отправки расписания. Пока тут немного возможностей, но попробуй что-то из <b>Меню</b>";
        await bot.SendMessage(message.Chat.Id, answer, ParseMode.Html);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}