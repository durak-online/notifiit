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
using NotiFIITBot.Repo;
using System.Text.RegularExpressions;

namespace NotiFIITBot.App;

public partial class Bot : IDisposable
{
    private readonly TelegramBotClient bot;
    private readonly CancellationTokenSource cts;
    private readonly HttpClient httpClient;

    private readonly HashSet<long> registeringUserIds = new();

    // если навестись, то даже описание есть чего оно ищет
    [GeneratedRegex(@"^(?:МЕН-)?(?<group>\d{6})-(?<subgroup>\d)$", RegexOptions.IgnoreCase)]
    private static partial Regex MENGroupRegex();

    private readonly IUserRepository userRepository;
    private readonly IScheduleRepository scheduleRepository;
    private readonly ScheduleService scheduleService;

    public Bot(IUserRepository userRepository, IScheduleRepository scheduleRepository,
        ScheduleService scheduleService, CancellationTokenSource cts)
    {
        this.userRepository = userRepository;
        this.scheduleRepository = scheduleRepository;
        this.scheduleService = scheduleService;
        this.cts = cts;
        var proxy = new WebProxy("http://75.56.141.249:8000");

        var httpClient = new HttpClient(new HttpClientHandler()
        {
            Proxy = proxy,
            UseProxy = false
        });
        this.httpClient = httpClient;

        bot = new TelegramBotClient(token: EnvReader.BotToken, httpClient: httpClient);
    }

    public async Task StartPolling()
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var info = bot.GetMe(timeoutCts.Token).GetAwaiter().GetResult();
            Log.Information($"[BOT] Bot {info} started to work");

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
                DropPendingUpdates = true
            };

            bot.StartReceiving(
                HandleUpdate,
                HandlePollingError,
                receiverOptions,
                cts.Token
            );

            Log.Information("[BOT] Bot is now receiving updates...");
        }
        catch (Exception ex)
        {
            Log.Fatal($"[BOT] Can't initialize bot: {ex}");
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
            catch (Exception ex)
            {
                Log.Error(ex, $"[BOT] Can't send notification to user with ID {id}");
            }
        }

        Log.Information($"[BOT] Sent notifitation to {successful} out of {chatIds.Length} users");
    }

    private async Task HandleUpdate(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update)
            {
                case { Message: { } message }:
                    Log.Information($"[BOT] Received message from {message.From!.ToString() ?? "unknown"}");
                    await HandleMessage(message);
                    break;

                case { CallbackQuery: { } cbQuery }:
                    Log.Information($"[BOT] Callback query from {cbQuery.From}");
                    await HandleCallbackQuery(cbQuery);
                    break;

                default:
                    Log.Information($"[BOT] Received {update.Type} update type, no handler for this type");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[BOT] Exception while handling update: {ex}");
        }
    }


    private async Task HandleCallbackQuery(CallbackQuery cbQuery)
    {
        string? sched = null;

        switch (cbQuery.Data)
        {
            case nameof(SchedulePeriod.Today):
                sched = await GetSchedForPeriodAsync(cbQuery.From.Id, SchedulePeriod.Today);
                break;
            case nameof(SchedulePeriod.Tomorrow):
                sched = await GetSchedForPeriodAsync(cbQuery.From.Id, SchedulePeriod.Tomorrow);
                break;
            case nameof(SchedulePeriod.Week):
                sched = await GetSchedForPeriodAsync(cbQuery.From.Id, SchedulePeriod.Week);
                break;
            case nameof(SchedulePeriod.TwoWeeks):
                sched = await GetSchedForPeriodAsync(cbQuery.From.Id, SchedulePeriod.TwoWeeks);
                break;
        }

        if (sched == null)
        {
            Log.Error($"[BOT] Can't handle CallbackQuery with data: {cbQuery.Data}, text: {cbQuery.Message?.Text}");
            sched = "Произошла ошибка при получении расписания";
        }

        await bot.EditMessageText(
            cbQuery.Message!.Chat,
            cbQuery.Message.Id,
            sched,
            ParseMode.Html
        );
        await bot.AnswerCallbackQuery(cbQuery.Id);
    }

    private async Task HandlePollingError(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            Log.Error($"[BOT] Telegram Bot Error: {exception}");

            if (exception is not ApiRequestException apiException || apiException.ErrorCode != 401)
                return;
            Log.Fatal("[BOT] Invalid token, stopping bot...");
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
        var isRegistering = await IsRegistering(message);
        if (isRegistering)
            return;

        switch (message.Text!.Split()[0])
        {
            #region base commands
            case "/start":
                await bot.SendMessage(
                    message.Chat.Id,
                    "Добро пожаловать! Напиши свою группу в формате МЕН-группа-подгруппа для регистрации. " +
                    "Например <b>МЕН-240801-1</b>",
                    ParseMode.Html);

                registeringUserIds.Add(message.Chat.Id);
                break;

            case "/rereg":
                await bot.SendMessage(
                    message.Chat.Id,
                    "Меняем твою группу! Напиши свою группу в формате МЕН-группа-подгруппа для регистрации. " +
                    "Например <b>МЕН-240801-1</b>",
                    ParseMode.Html);

                registeringUserIds.Add(message.Chat.Id);
                break;

            case "/sched":
                await AskSchedule(message);
                break;

            case "/today":
                var todaySched = await GetSchedForPeriodAsync(message.Chat.Id, SchedulePeriod.Today);
                await bot.SendMessage(
                    message.Chat.Id,
                    todaySched,
                    ParseMode.Html
                );
                break;

            case "/tmrw":
                var tomorrowSched = await GetSchedForPeriodAsync(message.Chat.Id, SchedulePeriod.Tomorrow);
                await bot.SendMessage(
                    message.Chat.Id,
                    tomorrowSched,
                    ParseMode.Html
                );
                break;

            case "/week":
                var weekSched = await GetSchedForPeriodAsync(message.Chat.Id, SchedulePeriod.Week);
                await bot.SendMessage(
                    message.Chat.Id,
                    weekSched,
                    ParseMode.Html
                );
                break;

            case "/2week":
                var twoWeeksSched = await GetSchedForPeriodAsync(message.Chat.Id, SchedulePeriod.TwoWeeks);
                await bot.SendMessage(
                    message.Chat.Id,
                    twoWeeksSched,
                    ParseMode.Html
                );
                break;
            #endregion

            #region side commands
            case "/slots":
                await bot.SendMessage(message.Chat.Id, "Додепчик пошел");
                await bot.SendDice(
                    message.Chat.Id,
                    DiceEmoji.Dice
                );
                break;

            case "/help":
                await SendHelpMessage(message);
                break;
            #endregion

            #region admin commands
            case "/stop" when IsAdmin(message.From!):
                await bot.SendMessage(message.Chat.Id, "Останавливаю бота...");
                Log.Information($"Stopped by {message.From!}");
                cts.Cancel();
                break;

            case "/delete" when IsAdmin(message.From!):
                await DeleteUser(message);
                break;

            #endregion

            default:
                await bot.SendMessage(
                    message.Chat.Id,
                    "Я не понял о чем ты...\nНапиши /help, если не знаешь с чего начать!");
                break;
        }
    }

    private async Task DeleteUser(Message message)
    {
        if (long.TryParse(message.Text!.Split()[1], out var userToDeleteId))
        {
            await userRepository.DeleteUserAsync(userToDeleteId);
            await bot.SendMessage(
                    message.Chat.Id,
                    "Юзер был успешно удален из базы данных",
                    ParseMode.Html);
        }
        else
        {
            await bot.SendMessage(
                    message.Chat.Id,
                    "Юзер не был удален из базы данных, неверно введен ID.\n" +
                    "Допускаются только цифры, нужен tg ID юзера",
                    ParseMode.Html);
        }
    }

    private async Task<bool> IsRegistering(Message message)
    {
        if (registeringUserIds.Contains(message.Chat.Id))
        {
            var match = MENGroupRegex().Match(message.Text!);

            if (!match.Success)
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    "Неверный формат группы. Убедись, что прислал что-то похожее на <b>МЕН-240801-1</b> и попробуй еще раз",
                    ParseMode.Html);
                return true;
            }

            if (int.TryParse(match.Groups["group"].Value, out var groupNum) &&
                int.TryParse(match.Groups["subgroup"].Value, out var subGroupNum))
            {
                var user = await userRepository.FindUserAsync(message.Chat.Id);
                if (user != null)
                {
                    user.MenGroup = groupNum;
                    user.SubGroup = subGroupNum;
                    await userRepository.UpdateUserAsync(user);
                }
                else
                    await userRepository.AddUserAsync(message.Chat.Id, groupNum, subGroupNum);

                registeringUserIds.Remove(message.Chat.Id);
                await bot.SendMessage(
                    message.Chat.Id,
                    "Ты был успешно зарегистрирован! Посмотри список доступных команд в <b>Меню</b>",
                    ParseMode.Html);
            }

            return true;
        }

        return false;
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

    private async Task<string> GetSchedForPeriodAsync(long userId, SchedulePeriod period)
    {
        try
        {
            var user = await userRepository.FindUserAsync(userId);
            if (user == null)
                return "Ты еще не зарегестрирован в базе данных";

            var scheduleDays = await scheduleService.GetFormattedScheduleAsync(user.MenGroup, user.SubGroup, period);
            if (scheduleDays == null || scheduleDays.Count == 0)
            {
                return $"Пар для группы МЕН-{user.MenGroup}-{user.SubGroup} нет 🎉";
            }

            var result = new System.Text.StringBuilder();

            foreach (var (date, lessons) in scheduleDays)
            {
                result.Append(Formatter.FormatLessons(date, lessons));
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while getting schedule from DB");
            return "Произошла ошибка при получении данных из базы";
        }
    }

    private static bool IsAdmin(User user)
    {
        return user.Id == EnvReader.CreatorId;
    }

    private async Task SendHelpMessage(Message message)
    {
        var answer = "Это бот для отправки расписания. Все возможные команды есть в <b>Меню</b>\n\n" +
            "/sched - Отправляет сообщение с выбором периода расписания\n" +
            "/today - Отправляет расписание на сегодня\n" +
            "/tmrw - Отправляет расписание на завтра\n" +
            "/week - Отправляет расписание на текущую неделю\n" +
            "/2week - Отправляет расписание на текущую и следующую неделю\n" +
            "/rereg - Изменяет твою МЕН группу и подгруппу";

        if (IsAdmin(message.From!))
            answer += "\n\n<b>ДЛЯ АДМИНОВ</b>\n\n" +
                "/stop - Остановить бота, то есть <b>остановить программу на сервере</b>\n" +
                "/delete *tg_id* - Удалить юзера с данным tg_id из базы данных\n";

        await bot.SendMessage(message.Chat.Id, answer, ParseMode.Html);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
