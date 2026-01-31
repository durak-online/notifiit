namespace NotiFIITBot.Domain;

public static class EmojiProvider
{
    public static readonly string Clock = "🕒";
    public static readonly string Monkey = "🙊";
    public static readonly string Calendar = "📅";
    
    private static readonly string DefaultSubject = "🎓";
    private static readonly string DefaultLocation = "🏛";

    private static readonly List<Emoji> Subjects =
    [
        // Математический блок
        new("🧠 ", "Математический анализ кружок"),
        new("🧩", "Математический анализ", "Матан"),
        new("📚", "ДМ", "Алгебра и геометрия", "Дискретка", "Дискретная математика", "Алгем"),
        new("🎲", "Теория вероятностей", "Тервер"),
        new("📈", "Мат.стат", "Математическая статистика"),
        
        // Программирование
        new("🐍", "Язык Python"),
        new("#️⃣", "ЯТП", "ООП"),
        new("📟", "АрхЭВМ"),
        new("🌐", "Сети", "Веб-разработка"),
        new("🚗", "МЛ", "Машинное обучение"),
        new("💾", "Базы данных"),
        new("🛡", "Основы компьютерной безопасности"),
        new("⌛", "Алгоритмы"),

        // Гуманитарные и общие
        new("🔧", "ОПД", "Проектный практикум", "Создание IT-продукта"),
        new("👥", "Практическая психология на работе", "Публичные выступления"),
        new("🏃", "Физкультура"),
        new("👋‍", "Практика эффективной коммуникации"),
        new("🇷🇺", "Основы российской государственности", "История"),
        new("🇬🇧", "Иностранный язык")
    ];

    private static readonly List<Emoji> Locations =
    [
        new("💻", "Онлайн"),
        new("🏛", "Тургенева"),
        new("🌿", "Куйбышева"),
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