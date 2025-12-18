namespace NotiFIITBot.Domain.BotCommands;

[AttributeUsage(AttributeTargets.Class)]
public class KeyboardTextAttribute(string buttonName) : Attribute
{
    public string ButtonName { get; } = buttonName;
}
