using NUnit.Framework;

namespace NotiFIITBot;

[TestFixture]
public class ParserTests
{
    [Test]
    public async Task GetGroups_ReturnsGroups_WhenEventMatches()
    {
        var groups = await Parser.GetGroups(4);
        foreach (var group in groups) Console.WriteLine(group.title);
        Assert.That(groups.Count > 1);
    }

    [Test]
    public async Task GetGroupId()
    {
        var id = await Parser.GetGroupId(240801);
        Console.WriteLine(id);
        Assert.That(id == 63804);
    }

    [Test]
    public void GetShow()
    {
        TableParser.ShowTables();
    }
}