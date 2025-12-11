using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Repo;
using NotiFIITBot.Consts;
using Serilog;
using NotiFIITBot.Domain;
using NotiFIITBot.Logging;

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

        services.AddTransient<DbSeeder>();

        services.AddScoped<Bot>();
        services.AddScoped<Notifier>();

        services.AddSingleton<CancellationTokenSource>();


        return services.BuildServiceProvider();
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