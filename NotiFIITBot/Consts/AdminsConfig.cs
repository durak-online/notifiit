using System.Text.Json;

namespace NotiFIITBot.Consts;

public static class AdminsConfig
{
    public static List<long> AdminIds { get; }

    static AdminsConfig()
    {
        var adminJson = File.ReadAllText("admins.json");
        var adminData = JsonSerializer.Deserialize<AdminData>(adminJson);
        AdminIds = adminData.AdminIds;
    }
}

public class AdminData
{
    public List<long> AdminIds { get; set; }
}
