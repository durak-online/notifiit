using System.Globalization;
using System.Text;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;

namespace NotiFIITBot.App;

public class ScheduleFormatter
{
    public static class Emoji
    {
        public static string Clock = "🕒";
        public static string Subject = "🎓";
        public static string Teacher = "👤";
        public static string Calendar = "📅";
        public static string Location = "📍";
        public static string Monkey = "🙊";
    }
    
    public static string BuildDailySchedule(List<Lesson> lessons)
    {
        var strBuilder = new StringBuilder();
        
        var day = lessons[0].DayOfWeek;
        strBuilder.Append($" {Emoji.Calendar} {DayOfWeekInRus(day)}:\n\n");
        
        if (!lessons.Any())
        {
            strBuilder.AppendLine("Занятий нет {Emoji.Monkey}");
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

        var time = $"{Emoji.Clock} {lesson.Begin:HH:mm}-{lessonEnd:HH:mm}";
        var subject = $"{Emoji.Subject} {lesson.SubjectName}";
        var teacher = string.IsNullOrWhiteSpace(lesson.TeacherName)
            ? "{Emoji.Teacher} Преподаватель: —"
            : $"{Emoji.Teacher} Преподаватель: {lesson.TeacherName}";
        var room = string.IsNullOrWhiteSpace(lesson.ClassRoom)
            ? "{Emoji.Location} Аудитория: —"
            : $"{Emoji.Location} Аудитория: {lesson.ClassRoom}";

        return $"{time}\n{subject}\n{teacher}\n{room}\n";
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