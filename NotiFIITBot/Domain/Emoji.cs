namespace NotiFIITBot.Domain;

public class Emoji(string emojiType, params string[] words)
{
    public string EmojiType { get; } = emojiType;
    private readonly string[] keywords = words;

    public bool IsMatch(string text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}