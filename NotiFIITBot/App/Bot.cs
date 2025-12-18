using NotiFIITBot.Consts;
using NotiFIITBot.Domain;
using NotiFIITBot.Domain.BotCommands;
using NotiFIITBot.Logging;
using NotiFIITBot.Metrics;
using NotiFIITBot.Repo;
using Serilog;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot.App;

public partial class Bot(
    IUserRepository userRepository,
    BotMessageService botService,
    RegistrationService registrationService,
    BotCommandManager botCommandManager,
    IScheduleRepository scheduleRepository,
    CancellationTokenSource cts,
    ILoggerFactory loggerFactory
        )
{
    private readonly IUserRepository userRepository = userRepository;
    private readonly BotMessageService botService = botService;
    private readonly RegistrationService registrationService = registrationService;
    private readonly BotCommandManager botCommandManager = botCommandManager;
    private readonly IScheduleRepository scheduleRepository = scheduleRepository;

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
                AllowedUpdates = [UpdateType.Message],
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
                "Я не понял о чем ты...\nНапиши /help, если не знаешь с чего начать"
            );
    }

    private async Task AnswerOnMessage(Message message)
    {
        var isRegistering = await TryRegisterUser(message);
        if (isRegistering)
            return;

        var isExecuted = await botCommandManager.TryExecuteCommand(message);
        if (!isExecuted)
            await botService.SendMessage(
                message.Chat.Id,
                "Неизвестная команда. Напиши /help или посмотри список в <b>Меню</b>"
            );
    }

    private async Task<bool> TryRegisterUser(Message message)
    {
        if (!registrationService.ContainsUser(message.Chat.Id))
            return false;
        
        var match = MENGroupRegex().Match(message.Text!);

        if (!match.Success)
        {
            await botService.SendMessage(
                message.Chat.Id,
                "Ты ввел(а) неверный формат группы. Пришли мне МЕН-номер своей группы в формате МЕН-группа-подгруппа ещё раз"
            );
            return true;
        }

        if (int.TryParse(match.Groups["group"].Value, out var groupNum) &&
            int.TryParse(match.Groups["subgroup"].Value, out var subGroupNum))
        {
            var isGroupValid = await scheduleRepository.GroupExistsAsync(groupNum, subGroupNum);
            if (!isGroupValid)
            {
                await botService.SendMessage(
                    message.Chat.Id,
                    $"Группа <b>МЕН-{groupNum}-{subGroupNum}</b> не найдена.\n" +
                    "Возможно, ты ввел(а) неверный номер группы и подгруппы или расписание для неё еще не загружено."
                );
                return true;
            }

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
                "Ты был(а) успешно зарегистрирован(а)! Посмотри список доступных команд в <b>Меню</b>",
                useMainKeyboard: true
            );
        }

        return true;
    }
}
