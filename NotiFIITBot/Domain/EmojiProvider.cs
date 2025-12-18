namespace NotiFIITBot.Domain;

public static class EmojiProvider
{
    public const string Clock = "🕒";
    public const string Monkey = "🙊";
    public const string Calendar = "📅";
    
    private const string DefaultSubject = "🎓";
    private const string DefaultLocation = "🏛";

    private static readonly List<Emoji> Subjects =
    [
        // Математический блок
        new("🧠 ", "Математический анализ кружок"),
        new("🧩", "Математический анализ", "Матан"),
        new("📚", "ДМ", "Алгебра и геометрия", "Дискретка", "Дискретная математика", "Алгем"),
        new("💀", "Теория вероятностей", "Тервер"),
        
        // Программирование
        new("🐍", "Язык Python"),
        new("#️⃣", "ЯТП", "ООП"),
        new("📟", "АрхЭВМ"),
        new("🌐", "Сети"),

        // Гуманитарные и общие
        new("🔧", "ОПД", "Проектный практикум 2"),
        new("👥", "Практическая психология на работе"),
        new("🏃", "Физкультура"),
        new("👋‍", "Практика эффективной коммуникации"),
        new("🇷🇺", "Основы российской государственности"),
        new("🇬🇧", "Иностранный язык")
    ];

    private static readonly List<Emoji> Locations =
    [
        new("💻", "Онлайн"),
        new("🏛", "тургенева"),
        new("🌿", "куйбышева"),
    ];

    public static string GetSubjectEmoji(string? subjectName)
    {
        if (string.IsNullOrWhiteSpace(subjectName)) 
            return DefaultSubject;

        var rule = Subjects.FirstOrDefault(r => r.IsMatch(subjectName));
        return rule?.EmojiType ?? DefaultSubject;
    }
    
    public static string GetLocationEmoji(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) 
            return DefaultLocation;

        var rule = Locations.FirstOrDefault(r => r.IsMatch(location));
        return rule?.EmojiType ?? DefaultLocation;
    }
}