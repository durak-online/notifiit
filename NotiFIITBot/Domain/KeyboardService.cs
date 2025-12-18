using Microsoft.Extensions.DependencyInjection;
using NotiFIITBot.Domain.BotCommands;
using System.Reflection;
using Telegram.Bot.Types.ReplyMarkups;

namespace NotiFIITBot.Domain;

public class KeyboardService : IKeyboardService
{
    public ReplyKeyboardMarkup MainKeyboard { get; }

    public KeyboardService()
    {
        var today = GetCommandButtonText<TodayCommand>();
        var tmrw = GetCommandButtonText<TomorrowCommand>();
        var week = GetCommandButtonText<WeekCommand>();
        var twoWeeks = GetCommandButtonText<TwoWeekCommand>();

        MainKeyboard = new ReplyKeyboardMarkup(
        [
            [today, tmrw],
            [week, twoWeeks]
        ])
        {
            ResizeKeyboard = true,
            IsPersistent = true
        };
    }

    private static string GetCommandButtonText<TCommand>() 
        where TCommand : IBotCommand
    {
        var attribute = typeof(TCommand)
            .GetCustomAttribute<KeyboardTextAttribute>() 
            ?? throw new ArgumentException($"Command {typeof(TCommand)} has no attribute {nameof(KeyboardTextAttribute)}");
        return attribute.ButtonName
               ?? throw new Exception($"Command {typeof(TCommand)} not found or has no button text");
    }
}
