using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotiFIITBot.Database.Data;
using Serilog;

namespace NotiFIITBot.App;

public class Program
{
    public static async Task Main()
    {
        Log.Information("[APP] Started program");

        try
        {
            var serviceProvider = DiContainer.ConfigureServices();

            var cts = serviceProvider.GetRequiredService<CancellationTokenSource>();

            ConfigureShutdownHandlers(cts);

            await ApplyMigrationsAsync(serviceProvider);

            Log.Information("[SEED] Starting database update...");
            try
            {
                using var seederScope = serviceProvider.CreateScope();
                var seeder = seederScope.ServiceProvider.GetRequiredService<DbSeeder>();

                await seeder.SeedAsync(useTable: true, useApi: false, targetGroups: []);
                Log.Information("[SEED] Database updated successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SEED] Error during database seeding. Continuing with existing data...");
            }

            using var botScope = serviceProvider.CreateScope();
            var bot = botScope.ServiceProvider.GetRequiredService<Bot>();
            var notifier = botScope.ServiceProvider.GetRequiredService<Notifier>();

            await bot.StartPolling();
            await notifier.Start();
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException)
        {
            Log.Information("[BOT] Bot stopped gracefully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unexpected error");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task ApplyMigrationsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

        Log.Information("[MIGRATIONS] Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        Log.Information("[MIGRATIONS] Migrations applied successfully");
    }

    private static void ConfigureShutdownHandlers(CancellationTokenSource cts)
    {
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Log.Information("[APP] Received Ctrl+C, stopping application...");
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            Log.Information("[APP] Process exit requested, stopping application...");
            cts.Cancel();
        };
    }
}