using System.Text;

namespace NotiFIITBot.Domain;

public static class ScheduleFormatter
{
    public static string BuildDailySchedule(DateOnly date, List<Lesson> lessons)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📅 {DayOfWeekInRus(lessons[0].DayOfWeek)} ({date:dd.MM.yyyy})");
        sb.AppendLine();

        if (lessons.Count == 0)
        {
            sb.AppendLine($"Занятий нет {EmojiProvider.Monkey}");
            sb.AppendLine();
            
            return sb.ToString();
        }
        foreach (var lesson in lessons)
        {
            var lessonEnd = lesson.End ?? lesson.Begin!.Value.AddMinutes(90);
            sb.AppendLine($"{EmojiProvider.Clock} <b>{lesson.Begin:HH:mm} - {lessonEnd:HH:mm}</b>");

            var subEmoji = EmojiProvider.GetSubjectEmoji(lesson.SubjectName);
            var teacher = string.IsNullOrEmpty(lesson.TeacherName) ? "" : $" - {lesson.TeacherName}";
            sb.AppendLine($"{subEmoji} {lesson.SubjectName}<i>{teacher}</i>");

            sb.AppendLine(FormatFullLocation(lesson));
            
            sb.AppendLine("------------------------------------");
            sb.AppendLine();
        }

        return sb.ToString();
    }
    
    public static string BuildWeeklySchedule(Dictionary<DateOnly, List<Lesson>> schedule)
    {
        var sb = new StringBuilder();

        foreach (var (date, lessons) in schedule.OrderBy(x => x.Key))
        {
            // Заголовок дня: 📅 Понедельник (15.12.2025)
            sb.AppendLine($"📅 {DayOfWeekInRus(date.DayOfWeek)} ({date:dd.MM.yyyy})");
            sb.AppendLine();

            if (lessons.Count == 0)
            {
                sb.AppendLine($"Выходной {EmojiProvider.Monkey}");
                sb.AppendLine("==============================");
                sb.AppendLine();
                continue;
            }

            // Перебираем уроки по порядку
            foreach (var lesson in lessons.OrderBy(l => l.Begin))
            {
                var lessonEnd = lesson.End ?? lesson.Begin!.Value.AddMinutes(90);
                var subEmoji = EmojiProvider.GetSubjectEmoji(lesson.SubjectName);

                sb.AppendLine($"{subEmoji} <b>{lesson.Begin:HH:mm} - {lessonEnd:HH:mm}</b> {lesson.SubjectName}");

                sb.AppendLine(FormatFullLocation(lesson));

                sb.AppendLine("------------------------------------");
            }

            // Разделитель между днями (двойная черта)
            sb.AppendLine();
            sb.AppendLine("==============================");
            sb.AppendLine();
        }

        return sb.ToString();
    }
    
    private static string FormatFullLocation(Lesson lesson)
    {
        var loc = lesson.AuditoryLocation ?? "";
        var room = lesson.ClassRoom ?? "-";
        if (loc.Contains("онлайн", StringComparison.OrdinalIgnoreCase) || 
            room.Contains("онлайн", StringComparison.OrdinalIgnoreCase))
        {
            return $"{EmojiProvider.GetLocationEmoji("Онлайн")} Онлайн";
        }
        var locEmoji = EmojiProvider.GetLocationEmoji(loc);
        var locPart = string.IsNullOrEmpty(loc) ? "" : $"{loc}; ";
        return $"{locEmoji} {locPart}<i>Ауд. {room}</i>";
    }

    private static string DayOfWeekInRus(DayOfWeek? dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "Понедельник",
            DayOfWeek.Tuesday => "Вторник",
            DayOfWeek.Wednesday => "Среда",
            DayOfWeek.Thursday => "Четверг",
            DayOfWeek.Friday => "Пятница",
            DayOfWeek.Saturday => "Суббота",
            DayOfWeek.Sunday => "Воскресенье",
            _ => "День"
        };
    }
}