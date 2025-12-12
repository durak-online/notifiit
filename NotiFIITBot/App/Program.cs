using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Logging;
using NotiFIITBot.Metrics;
using Serilog;
using Quartz;
using Quartz.Impl;

namespace NotiFIITBot.App;

public class Program
{
    private static ILogger logger;
    private static MetricsReporter? metricsReporter;

    public static async Task Main()
    {
        var serviceProvider = DiContainer.ConfigureServices();

        var cts = serviceProvider.GetRequiredService<CancellationTokenSource>();
        logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("APP");

        logger.Information("Started program");

        try
        {
            ConfigureShutdownHandlers(cts);

            await ApplyMigrationsAsync(serviceProvider);
            await UpdateDatabase(serviceProvider);

            using var botScope = serviceProvider.CreateScope();
            var bot = botScope.ServiceProvider.GetRequiredService<Bot>();
            var notifier = botScope.ServiceProvider.GetRequiredService<Notifier>();

            // для отчетов
            var schedulerFactory = new StdSchedulerFactory();
            var scheduler = await schedulerFactory.GetScheduler();
            await scheduler.Start();
            
            // job для генерации отчетов (сам SimpleMetricsJob в reporter)
            var job = JobBuilder.Create<SimpleMetricsJob>()
                .WithIdentity("weekly-report-job")
                .Build();
            
            var trigger = TriggerBuilder.Create()
                .WithIdentity("weekly-report-trigger")
                .WithSchedule(CronScheduleBuilder.WeeklyOnDayAndHourAndMinute(DayOfWeek.Monday, 0, 5))
                .StartNow()
                .Build();
            
            await scheduler.ScheduleJob(job, trigger);
            logger.Information($"Weekly report scheduled. Next run: {trigger.GetNextFireTimeUtc()?.LocalDateTime}");
            
            metricsReporter = new MetricsReporter();
            metricsReporter.GenerateAllReports();
            
            await bot.StartPolling();
            await notifier.Start();
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException)
        {
            logger.Information("Bot stopped gracefully");
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "Unexpected error");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    private static async Task UpdateDatabase(IServiceProvider serviceProvider)
    {
        logger.Information("Starting database update...");
        try
        {
            using var seederScope = serviceProvider.CreateScope();
            var seeder = seederScope.ServiceProvider.GetRequiredService<DbSeeder>();

            await seeder.SeedAsync(useTable: true, useApi: false, targetGroups: []);
            logger.Information("Database updated successfully.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error during database seeding. Continuing with existing data...");
        }
    }

    private static async Task ApplyMigrationsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        logger.Information("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        logger.Information("Migrations applied successfully");
    }

    private static void ConfigureShutdownHandlers(CancellationTokenSource cts)
    {
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            logger.Information("Received Ctrl+C, stopping application...");
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            logger.Information("Process exit requested, stopping application...");
            cts.Cancel();
        };
    }
}