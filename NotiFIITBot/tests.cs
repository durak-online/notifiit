using NUnit.Framework;

namespace NotiFIITBot;

[TestFixture]
public class ParserTests
{
    [Test]
    public async Task GetLesson_ReturnsLesson_WhenEventMatches()
    {
        var r = await Parser.GetLesson("150206", new DateOnly(2025, 10, 13), 2, 2);
        var lesson = new Lesson(2, "Языки и технологии программирования", "Субботин И.М.", "528", new TimeOnly(10, 40), new TimeOnly(12,10), "Тургенева, 4", 2, new DateOnly(2025, 10, 13));
        
        Assert.That(r.Date ==  lesson.Date);
        Assert.That(r.SubjectName ==  lesson.SubjectName);
    }

}

