using System.Text;

namespace NotiFIITBot.Domain;

public static class ScheduleFormatter
{
    private static class Emoji
    {
        public static string Clock = "🕒";
        public static string Subject = "🎓";
        public static string Teacher = "👤";
        public static string Calendar = "📅";
        public static string Location = "🏛";
        public static string Monkey = "🙊";
    }

    public static string BuildDailySchedule(DateOnly date, List<Lesson> lessons)
    {
        var strBuilder = new StringBuilder();

        strBuilder.Append($"{Emoji.Calendar} {DayOfWeekInRus(lessons[0].DayOfWeek)} ({date}):\n\n");

        if (lessons.Count == 0)
        {
            strBuilder.AppendLine($"Занятий нет {Emoji.Monkey}");
            return strBuilder.ToString();
        }
        foreach (var lesson in lessons)
        {
            strBuilder.Append(FormatLesson(lesson));
            strBuilder.AppendLine();
        }

        return strBuilder.ToString();
    }

    public static string FormatLesson(Lesson lesson)
    {
        var lessonEnd = lesson.End == null
            ? lesson.Begin!.Value.AddHours(1).AddMinutes(30)
            : lesson.End;

        var time = $"{Emoji.Clock} №{lesson.PairNumber} {lesson.Begin:HH:mm}-{lessonEnd:HH:mm}";
        var subject = $"{Emoji.Subject} {lesson.SubjectName}";
        var teacher = $"{Emoji.Teacher} Преподаватель: " +
            $"{(string.IsNullOrEmpty(lesson.TeacherName) ? "-" : lesson.TeacherName)}";

        // some shit
        var room = $"{Emoji.Location} " +
            $"{(string.IsNullOrEmpty(lesson.ClassRoom) 
            ? "Ауд. -" 
            : lesson.ClassRoom.ToLower() == "онлайн" ? lesson.ClassRoom : $"Ауд. {lesson.ClassRoom}")}";
        var location = $"{(string.IsNullOrEmpty(lesson.AuditoryLocation) 
            || lesson.AuditoryLocation.ToLower() == "онлайн" ? "" : $"; {lesson.AuditoryLocation}")}";

        return $"{time}\n{subject}\n{teacher}\n{room}{location}\n";
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
            _ => "Понедельник"
        };
    }
}
