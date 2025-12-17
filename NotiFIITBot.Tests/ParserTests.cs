using NotiFIITBot.Consts;
using NotiFIITBot.Domain;

namespace NotiFIITBot.Tests;

[TestFixture]
public class ParserTests
{
    private bool printInformation = false;
    
    [Test]
    public async Task GetLessons()
    {
        var lessons = await ApiParser.GetLessons(63804, 1);
        foreach (var lesson in lessons)
        {
            if(printInformation)
                Console.WriteLine($"День: {lesson.DayOfWeek}, " +
                              $"Группа: {lesson.MenGroup}, " + $"Подгруппа: {lesson.SubGroup}, " +
                              $"Предмет: {lesson.SubjectName}, " +
                              $"Преподаватель: {lesson.TeacherName}, " +
                              $"Локация: {lesson.AuditoryLocation}, " +
                              $"Аудитория: {lesson.ClassRoom}, " +
                              $"Пара №: {lesson.PairNumber}, " +
                              $"Начало: {lesson.Begin} " +
                              $"Четность: {lesson.EvennessOfWeek} ");
        }
        Assert.That(lessons.Count() > 1);
    }
    
    [Test]
    public async Task GetGroups_ReturnsGroups_WhenEventMatches()
    {
        var groups = await ApiParser.GetGroups(4);
        foreach (var group in groups) Console.WriteLine(group.title);
        Assert.That(groups.Count > 1);
    }

    [Test]
    public async Task GetGroupId()
    {
        var id = await ApiParser.GetGroupId(240801);
        Console.WriteLine(id);
        Assert.That(id == 63804);
    }

    [Test]
    public void GetShow()
    {
        TableParser.ShowTables();
    }

    [Test]
    public void GetLessonsTable()
    {
        var scheduleData = TableParser.GetTableData(EnvReader.GoogleApiKey, EnvReader.TableId, EnvReader.Fiit2Range);
        
        Assert.That(scheduleData.Count > 0);
        
        scheduleData = scheduleData.Where(les => les.MenGroup == 240810).ToList();
        
        Assert.That(scheduleData.Where(les => les.SubjectName == "Физкультура").Count()>0);
    }

    [Test]
    public void GetEvenness_Monday()
    {
        var date = new DateOnly(2025,11,10);
        Assert.That(date.GetEvenness() == Evenness.Odd);
    }
    
    [Test]
    public void GetEvenness_NotMonday()
    {
        var date = new DateOnly(2025,9,2);
        Assert.That(date.GetEvenness() == Evenness.Odd);
    }
    
    [Test]
    public void GetEvenness_AnotherYear()
    {
        var date = new DateOnly(2026,1,3);
        Assert.That(date.GetEvenness() == Evenness.Even);
    }
}