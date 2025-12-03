using System.Net;
using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using NotiFIITBot.Consts;
using NotiFIITBot.Domain;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Repo;

namespace NotiFIITBot.App;

public class Bot : IDisposable
{
    private TelegramBotClient bot;
    private CancellationTokenSource cts;
    private HttpClient httpClient;

    public async Task Initialize(CancellationTokenSource cts)
    {
        try
        {
            this.cts = cts;
            var proxy = new WebProxy("http://168.81.64.204:8000");

            var httpClient = new HttpClient(new HttpClientHandler()
            {
                Proxy = proxy,
                UseProxy = true
            });
            this.httpClient = httpClient;

            bot = new TelegramBotClient(token: EnvReader.BotToken, httpClient: httpClient);

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
                // ignored
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
                    Log.Information($"Received message from {message.From!.ToString() ?? "unknown"}");
                    await HandleMessage(message);
                    break;

                case { CallbackQuery: { } cbQuery }:
                    Log.Information($"Callback query from {cbQuery.From}");
                    await HandleCallbackQuery(cbQuery);
                    break;

                default:
                    Log.Information($"Received {update.Type} update type, no handler for this type");
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
        string? sched = null;
        
        switch (cbQuery.Data)
        {
            case nameof(SchedulePeriod.Today):
                sched = await GetSchedForPeriodAsync(SchedulePeriod.Today);
                break;
            case nameof(SchedulePeriod.Tomorrow):
                sched = await GetSchedForPeriodAsync(SchedulePeriod.Tomorrow);
                break;
            case nameof(SchedulePeriod.Week):
                sched = await GetSchedForPeriodAsync(SchedulePeriod.Week);
                break;
            case nameof(SchedulePeriod.TwoWeeks):
                sched = await GetSchedForPeriodAsync(SchedulePeriod.TwoWeeks);
                break;
        }

        if (sched == null)
        {
            Log.Error($"Can't handle CallbackQuery with data: {cbQuery.Data}, text: {cbQuery.Message?.Text}");
            sched = "Произошла ошибка при получении расписания.";
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

            if (exception is not ApiRequestException apiException || apiException.ErrorCode != 401)
                return;
            Log.Fatal("Invalid token, stopping bot...");
            cts.Cancel();
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
        switch (message.Text!.Split()[0])
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

            case "/today":
                var todaySched = await GetSchedForPeriodAsync(SchedulePeriod.Today);
                await bot.SendMessage(
                    message.Chat.Id,
                    todaySched
                );
                break;

            case "/tmrw":
                var tomorrowSched = await GetSchedForPeriodAsync(SchedulePeriod.Tomorrow);
                await bot.SendMessage(
                    message.Chat.Id,
                    tomorrowSched
                );
                break;

            case "/week":
                var weekSched = await GetSchedForPeriodAsync(SchedulePeriod.Week);
                await bot.SendMessage(
                    message.Chat.Id,
                    weekSched
                );
                break;

            case "/2week":
                var twoWeeksSched = await GetSchedForPeriodAsync(SchedulePeriod.TwoWeeks);
                await bot.SendMessage(
                    message.Chat.Id,
                    twoWeeksSched
                );
                break;

            case "/slots":
                await bot.SendMessage(message.Chat.Id, "Додепчик пошел");
                await bot.SendDice(
                    message.Chat.Id,
                    "🎰"
                );
                break;

            case "/help":
                await SendHelpMessage(message);
                break;

            case "/stop" when IsAdmin(message.From!):
                await bot.SendMessage(message.Chat.Id, "Останавливаю бота...");
                Log.Information($"Stopped by {message.From!}");
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
            .AddButton("Сегодня", nameof(SchedulePeriod.Today))
            .AddButton("Завтра", nameof(SchedulePeriod.Tomorrow))
            .AddNewRow()
            .AddButton("Неделя", nameof(SchedulePeriod.Week))
            .AddButton("2 недели", nameof(SchedulePeriod.TwoWeeks));
        await bot.SendMessage(message.Chat, "Выбери какое расписание тебе нужно:", replyMarkup: schedInlineMarkup);
    }

    private async Task<string> GetSchedForPeriodAsync(SchedulePeriod period)
    {
        try 
        {
            var factory = new ScheduleDbContextFactory();
            using var context = factory.CreateDbContext(null);
            
            var repo = new ScheduleRepository(context);
            var service = new ScheduleService(repo);

            // Заглушка пользователя
            var userGroup = 240801; 
            var userSubGroup = 1;

            var lessons = await service.GetFormattedScheduleAsync(userGroup, userSubGroup, period);

            if (lessons == null || !lessons.Any())
            {
                return "Пар нет 🎉 (или база пуста)";
            }

            var result = "";
            
            foreach (var group in lessons.GroupBy(l => l.DayOfWeek))
                result += Formatter.FormatLessons(group.ToList());

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error getting schedule from DB");
            return "Произошла ошибка при получении данных из базы.";
        }
    }

    private bool IsAdmin(User user)
    {
        return user.Id == EnvReader.CreatorId;
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