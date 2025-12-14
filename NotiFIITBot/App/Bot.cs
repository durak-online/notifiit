using Serilog;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using NotiFIITBot.Consts;
using NotiFIITBot.Domain;
using NotiFIITBot.Repo;
using System.Text.RegularExpressions;
using NotiFIITBot.Logging;
using NotiFIITBot.Domain.BotCommands;

namespace NotiFIITBot.App;

public partial class Bot(
    IUserRepository userRepository,
    ScheduleService scheduleService,
    BotMessageService botService,
    RegistrationService registrationService,
    BotCommandManager botCommandManager,
    CancellationTokenSource cts,
    ILoggerFactory loggerFactory
        )
{
    private readonly IUserRepository userRepository = userRepository;
    private readonly ScheduleService scheduleService = scheduleService;
    private readonly BotMessageService botService = botService;
    private readonly RegistrationService registrationService = registrationService;
    private readonly BotCommandManager botCommandManager = botCommandManager;

    private readonly CancellationTokenSource cts = cts;
    private readonly ILogger logger = loggerFactory.CreateLogger("BOT");

    // если навестись, то даже описание есть чего оно ищет
    [GeneratedRegex(@"^(?:МЕН-)?(?<group>\d{6})-(?<subgroup>\d)$", RegexOptions.IgnoreCase)]
    private static partial Regex MENGroupRegex();

    public async Task StartPolling()
    {
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cts.Token);

        try
        {
            var info = await botService.GetBotInfo(linkedCts.Token);
            logger.Information($"Bot {info} started to work");

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
                DropPendingUpdates = true
            };

            botService.GetTelegramBot()
                .StartReceiving(
                HandleUpdate,
                HandlePollingError,
                receiverOptions,
                linkedCts.Token
            );

            logger.Information("Bot is now receiving updates...");
        }
        catch (OperationCanceledException)
        {
            logger.Error("Bot startup timed out after 30 seconds");
            throw;
        }
        catch (Exception ex)
        {
            logger.Fatal($"Can't initialize bot: {ex}");
            throw;
        }
        finally
        {
            timeoutCts.Dispose();
            linkedCts.Dispose();
        }
    }

    public async Task SendNotifitation(string message, params long[] chatIds)
    {
        var successful = 0;
        foreach (var id in chatIds)
        {
            try
            {
                await botService.SendMessage(id, message);
                successful++;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Can't send notification to user with ID {id}");
            }
        }

        logger.Information($"Sent notifitation to {successful} out of {chatIds.Length} users");
    }

    private async Task HandleUpdate(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update)
            {
                case { Message: { } message }:
                    logger.Information($"Received message from {message.From!.ToString() ?? "unknown"}");
                    await HandleMessage(message);
                    break;

                case { CallbackQuery: { } cbQuery }:
                    logger.Information($"Callback query from {cbQuery.From}");
                    await HandleCallbackQuery(cbQuery);
                    break;

                default:
                    logger.Information($"Received {update.Type} update type, no handler for this type");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Exception while handling update: {ex}");
        }
    }


    private async Task HandleCallbackQuery(CallbackQuery cbQuery)
    {
        var sched = "Произошла ошибка при получении расписания";

        try
        {
            sched = await scheduleService.GetSchedForPeriodAsync(
                cbQuery.From.Id, 
                (SchedulePeriod)Enum.Parse(typeof(SchedulePeriod), cbQuery.Data)
            );
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Can't handle CallbackQuery with data: {cbQuery.Data}, text: {cbQuery.Message?.Text}");
        }
        
        await botService.EditMessage(
            cbQuery.Message!.Chat,
            cbQuery.Message.Id,
            sched
        );

        // это надо, чтобы вверху в боте не было плашки "Загрузка"
        await botService.AnswerCallbackQuery(cbQuery);
    }

    private async Task HandlePollingError(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            logger.Error($"Telegram Bot Error: {exception}");

            if (exception is not ApiRequestException apiException || apiException.ErrorCode != 401)
                return;
            logger.Fatal("Invalid token, stopping bot...");
            cts.Cancel();
        }, cancellationToken);
    }

    private async Task HandleMessage(Message message)
    {
        if (message.Text != null)
            await AnswerOnMessage(message);
        else
            await botService.SendMessage(
                message.Chat.Id,
                "Я не понял о чем ты...\nНапиши /help, если не знаешь с чего начать!"
            );
    }

    private async Task AnswerOnMessage(Message message)
    {
        var isRegistering = await IsRegistering(message);
        if (isRegistering)
            return;

        var isExecuted = await botCommandManager.TryExecuteCommand(message);
        if (!isExecuted)
            await botService.SendMessage(
                message.Chat.Id,
                "Неизвестная команда. Напиши /help или посмотри список в <b>Меню</b>"
            );
    }

    private async Task<bool> IsRegistering(Message message)
    {
        if (registrationService.ContainsUser(message.Chat.Id))
        {
            var match = MENGroupRegex().Match(message.Text!);

            if (!match.Success)
            {
                await botService.SendMessage(
                    message.Chat.Id,
                    "Неверный формат группы. Убедись, что прислал что-то похожее на <b>МЕН-240801-1</b> и попробуй еще раз"
                );
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

                registrationService.RemoveUser(message.Chat.Id);
                await botService.SendMessage(
                    message.Chat.Id,
                    "Ты был успешно зарегистрирован! Посмотри список доступных команд в <b>Меню</b>"
                );
            }

            return true;
        }

        return false;
    }
}
