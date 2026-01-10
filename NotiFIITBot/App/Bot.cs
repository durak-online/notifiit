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
    IKeyboardService keyboardService,
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
        var chatId = message.Chat.Id;
        
        var session = registrationService.GetRegSession(chatId);
        if (session == null) 
            return false; 

        var text = message.Text?.Trim();
        if (string.IsNullOrEmpty(text)) 
            return true;

        if (!int.TryParse(text, out var selectedValue))
        {
            await botService.SendMessage(chatId, "Пожалуйста, выбери вариант кнопкой.");
            return true;
        }

        switch (session.Step)
        {
            case RegistrationStep.SelectCourse:
                // Пользователь выбрал КУРС
                var groups = await scheduleRepository.GetGroupsByCourseAsync(selectedValue);
                if (groups.Count == 0)
                {
                    await botService.SendMessage(chatId, "Для этого курса нет групп. Выбери другой.");
                    return true;
                }

                session.SelectedCourse = selectedValue;
                session.Step = RegistrationStep.SelectGroup;
                registrationService.UpdateRegSession(chatId, session);

                var groupKeyboard = keyboardService.CreateGridKeyboard(groups.Select(g => g.ToString()).ToList(), 2);
                await botService.SendMessage(chatId, $"Курс {selectedValue}. Теперь выбери группу:", groupKeyboard);
                break;

            case RegistrationStep.SelectGroup:
                // Пользователь выбрал ГРУППУ
                var subgroups = await scheduleRepository.GetSubgroupsByGroupAsync(selectedValue);
                if (subgroups.Count == 0) 
                    subgroups = [1]; // Если в базе пусто, предлагаем стандартную

                session.SelectedGroup = selectedValue;
                session.Step = RegistrationStep.SelectSubgroup;
                registrationService.UpdateRegSession(chatId, session);

                var subGroupKeyboard = keyboardService.CreateGridKeyboard(subgroups.Select(s => s.ToString()).ToList(), 2);
                await botService.SendMessage(chatId, $"Группа {selectedValue}. Выбери подгруппу:", subGroupKeyboard);
                break;

            case RegistrationStep.SelectSubgroup:
                // Пользователь выбрал ПОДГРУППУ 
                
                var user = await userRepository.FindUserAsync(chatId);
                if (user != null)
                {
                    user.MenGroup = session.SelectedGroup;
                    user.SubGroup = selectedValue;
                    await userRepository.UpdateUserAsync(user);
                }
                else
                {
                    await userRepository.AddUserAsync(chatId, session.SelectedGroup, selectedValue);
                }

                registrationService.RemoveUser(chatId);

                await botService.SendMessage(
                    chatId,
                    $"Готово! Ты записан в группу <b>МЕН-{session.SelectedGroup}-{selectedValue}</b>.",
                    useMainKeyboard: true
                );
                break;
        }

        return true;
    }
}
