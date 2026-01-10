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
        var twoWeeks = GetCommandButtonText<NextWeekCommand>();

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
    
    public ReplyKeyboardMarkup CreateGridKeyboard(List<string> items, int columns)
    {
        var buttons = new List<KeyboardButton[]>();
        for (int i = 0; i < items.Count; i += columns)
        {
            var row = items.Skip(i).Take(columns)
                .Select(text => new KeyboardButton(text))
                .ToArray();
            buttons.Add(row);
        }
        return new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true, OneTimeKeyboard = true };
    }
}
