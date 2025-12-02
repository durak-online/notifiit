using dotenv.net;

namespace NotiFIITBot.Consts;

internal static class EnvReader
{
    public static void Load(string envFilePath)
    {
        DotEnv.Load(new DotEnvOptions(
            envFilePaths: new[] { envFilePath },
            ignoreExceptions: false 
        ));
    }

    public static string BotToken =>
        Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");

    public static long CreatorId =>
        long.Parse(Environment.GetEnvironmentVariable("CREATOR_ID"));

    public static string PostgresUser =>
        Environment.GetEnvironmentVariable("POSTGRES_USER");

    public static string PostgresPassword =>
        Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

    public static string PostgresDbName =>
        Environment.GetEnvironmentVariable("POSTGRES_DB");
    
    
    public static string GoogleApiKey { get; }
    public static string TableId { get; }
    public static string Fiit1Range { get; }
    public static string Fiit2Range { get; }
}

