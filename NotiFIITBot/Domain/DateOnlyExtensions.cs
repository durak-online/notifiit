using NotiFIITBot.Consts;

namespace NotiFIITBot.Domain;

public static class DateOnlyExtensions
{
    public static Evenness Evenness(this DateOnly date)
    {
        var firstMonday = date.GetFirstStudyDay();
        var indexOfWeek = (date.DayNumber - firstMonday.DayNumber) / 7;
        return indexOfWeek % 2 == 0 ? Consts.Evenness.Odd : Consts.Evenness.Even;
    }

    private static bool IsFirstSem(this DateOnly date)
    {
        return date.Month is >= 9 and <= 12 or 1;
    }

    private static DateOnly GetFirstStudyDay(this DateOnly date)
    {
        //ну теперь получается ищем первый учебный день сентября
        //подумал чуть считерим и будем искать понедельник первой учебной недели, условно если учеба началась во вторник 1 сентября, то я возьму 31 августа
        var studyYear = date.Year;
        if (date.Month == 1) 
            studyYear--;
        var firstStudyDay = new DateOnly(studyYear, 9, 1);
        if (!date.IsFirstSem()) 
            return new DateOnly(studyYear, 2, 9);

        if (firstStudyDay.DayOfWeek != DayOfWeek.Monday)
            firstStudyDay = firstStudyDay.AddDays(1 - (int)firstStudyDay.DayOfWeek);
        return firstStudyDay;
    }
}
