using dotenv.net;

namespace NotiFIITBot.Consts;

internal static class EnvReader
{
    static EnvReader()
    {
        DotEnv.Load(new DotEnvOptions(false));
        var variables = DotEnv.Read();

        BotToken = variables["TELEGRAM_BOT_TOKEN"];

        PostgresUser = variables["POSTGRES_USER"];
        PostgresPassword = variables["POSTGRES_PASSWORD"];
        PostgresDbName = variables["POSTGRES_DB"];

        GoogleApiKey = variables["GOOGLE_API_KEY"];
        TableId = variables["TABLE_ID"];
        Fiit1Range = variables["FIIT_1_RANGE"];
        Fiit2Range = variables["FIIT_2_RANGE"];
    }

    public static string BotToken { get; }

    public static string PostgresUser { get; }
    public static string PostgresPassword { get; }
    public static string PostgresDbName { get; }

    public static string GoogleApiKey { get; }
    public static string TableId { get; }
    public static string Fiit1Range { get; }
    public static string Fiit2Range { get; }
}
