using NotiFIITBot.Consts;
using System.Text;

namespace NotiFIITBot.Domain;

public static class Formatter
{
    public static string FormatLessons(DateOnly date, List<Lesson> lessons)
    {
        var strBuilder = new StringBuilder();
        strBuilder.Append($"<b>{DayOfWeekInRus(date.DayOfWeek)} ({date:dd.MM}):</b>\n\n");
        
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
        return $"№ {lesson.PairNumber} {lesson.Begin.Value:HH:mm}-{lessonEnd:HH:mm}\n" +
               $"Предмет: {lesson.SubjectName}\n" +
               $"Аудитория: {lesson.ClassRoom}\n" +
               $"Преподаватель: {lesson.TeacherName}\n";
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

    private static string EvennessInRus(Evenness evenness)
    {
        return evenness switch
        { 
            Evenness.Even => "Чёт",
            Evenness.Odd => "Нечёт",
            _ => "Всегда"
        };
    }
}
