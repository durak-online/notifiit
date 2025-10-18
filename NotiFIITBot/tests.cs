using NUnit.Framework;

namespace NotiFIITBot;

[TestFixture]
public class ParserTests
{
    [Test]
    public async Task GetLesson_ReturnsLesson_WhenEventMatches()
    {
        var r = await Parser.GetLesson("150206", new DateOnly(2025, 10, 13), 2, 2);
        var lesson = new Lesson(2, "Языки и технологии программирования", "Субботин И.М.", "528", new TimeOnly(10, 40),
            new TimeOnly(12, 10), "Тургенева, 4", 2, "", new DateOnly(2025, 10, 13));

        Assert.That(r.Date == lesson.Date);
        Assert.That(r.SubjectName == lesson.SubjectName);
    }

    [Test]
    public async Task GetGroups_ReturnsGroups_WhenEventMatches()
    {
        var groups = await Parser.GetGroups(2);
        foreach (var group in groups) Console.WriteLine(group.title);
        Assert.That(groups.Count > 1);
    }

    [Test]
    public async Task GetGroupId()
    {
        var id = Parser.GetGroupId("240801");
        Console.WriteLine(id);
        Assert.That(id.Result == 63804);
    }

    [Test]
    public async Task GetShow()
    {
        TableParser.ShowTables();
    }
}