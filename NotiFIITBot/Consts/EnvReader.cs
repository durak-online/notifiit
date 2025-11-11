using dotenv.net;

namespace NotiFIITBot.Consts;

internal static class EnvReader
{
    static EnvReader()
    {
        DotEnv.Load(new DotEnvOptions(false));
        var variables = DotEnv.Read();

        BotToken = variables["TELEGRAM_BOT_TOKEN"];
        CreatorId = long.Parse(variables["CREATOR_ID"]);

        PostgresUser = variables["POSTGRES_USER"];
        PostgresPassword = variables["POSTGRES_PASSWORD"];
        PostgresDbName = variables["POSTGRES_DB"];
    }

    public static string BotToken { get; }
    public static long CreatorId { get; }

    public static string PostgresUser { get; }
    public static string PostgresPassword { get; }
    public static string PostgresDbName { get; }
}
