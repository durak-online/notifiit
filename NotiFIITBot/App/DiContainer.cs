using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Repo;
using NotiFIITBot.Consts;
using Serilog;
using NotiFIITBot.Domain;
using NotiFIITBot.Logging;
using Quartz;

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
        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog());
        services.AddSingleton<ILoggerFactory, LoggerFactory>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<ScheduleService>();

        services.AddTransient<DbSeeder>();

        services.AddScoped<Bot>();
        services.AddScoped<Notifier>();

        services.AddSingleton<CancellationTokenSource>();

        services.AddSingleton<BackupService>();

        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("WeeklyBackupJob");

            q.AddJob<BackupService>(opts => opts.WithIdentity(jobKey));

            q.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("WeeklyBackupTrigger")
                // Cron: Секунды Минуты Часы День Месяц ДеньНедели
                // 0 0 1 ? * MON  -> Каждый понедельник в 01:00:00
                .WithCronSchedule("0 0 1 ? * MON")); 
            //.WithCronSchedule("0 * * ? * *"));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        
        return services.BuildServiceProvider();
    }

    private static void ConfigureLogger()
    {
        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                $"logs/bot-{DateTime.Now:yyyy-MM-dd}.log",
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
    }
}