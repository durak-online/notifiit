using NotiFIITBot.Consts;
using NotiFIITBot.Domain;

namespace NotiFIITBot.Tests;

[TestFixture]
public class ParserTests
{
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
    public void GetEvenness_Monday()
    {
        var date = new DateOnly(2025,11,10);
        Assert.That(date.Evenness() == Evenness.Odd);
    }
    
    [Test]
    public void GetEvenness_NotMonday()
    {
        var date = new DateOnly(2025,9,2);
        Assert.That(date.Evenness() == Evenness.Odd);
    }
    
    [Test]
    public void GetEvenness_AnotherYear()
    {
        var date = new DateOnly(2026,1,3);
        Assert.That(date.Evenness() == Evenness.Even);
    }
}