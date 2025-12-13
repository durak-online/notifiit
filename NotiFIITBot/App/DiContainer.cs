using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Domain;
using NotiFIITBot.Domain.BotCommands;
using NotiFIITBot.Logging;
using NotiFIITBot.Repo;
using Serilog;
using System.Net;
using System.Reflection;
using Telegram.Bot;

namespace NotiFIITBot.App;

public static class DiContainer
{
    public static IServiceProvider ConfigureServices()
    {
        ConfigureLogger();

        var services = new ServiceCollection();

        var connectionString = $"Host=localhost;" +
                              $"Port=5434;" +
                              $"Database={EnvReader.PostgresDbName};" +
                              $"Username={EnvReader.PostgresUser};" +
                              $"Password={EnvReader.PostgresPassword}";

        services.AddDbContext<ScheduleDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        }, ServiceLifetime.Transient);

        services.AddSingleton(Log.Logger);
        services.AddSingleton<ILoggerFactory, LoggerFactory>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();

        services.AddScoped<ScheduleService>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<BotMessageService>();

        services.AddTransient<DbSeeder>();

        services.AddScoped<Bot>();
        services.AddScoped<Notifier>();

        services.AddSingleton<CancellationTokenSource>();

        services.AddScoped<ITelegramBotClient>(sp =>
            {
                var proxy = new WebProxy("http://75.56.141.249:8000");
                var httpClient = new HttpClient(new HttpClientHandler()
                {
                    Proxy = proxy,
                    UseProxy = false
                });

                return new TelegramBotClient(token: EnvReader.BotToken, httpClient: httpClient);
            }
        );

        RegisterBotCommands(services);

        return services.BuildServiceProvider();
    }

    private static void RegisterBotCommands(ServiceCollection services)
    {
        // регистрируем все IBotCommand, кроме абстрактного BaseCommand
        var commandTypes = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(IBotCommand).IsAssignableFrom(t) 
                    && !t.IsAbstract 
                    && t != typeof(HelpCommand));
        foreach (var type in commandTypes)
            services.AddSingleton(typeof(IBotCommand), type);

        // иначе там рекурсия
        services.AddSingleton<HelpCommand>();
        services.AddScoped<BotCommandManager>();
    }

    private static void ConfigureLogger()
    {
        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                $"logs/bot-{DateTime.Now:yyyy-MM-dd}.log",
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
    }
}