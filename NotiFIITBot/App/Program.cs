using NotiFIITBot.Consts; // Нужно для EnvReader, если вы захотите проверить загрузку переменных явно
using Serilog;

namespace NotiFIITBot.App;

public class Program
{
    private static readonly CancellationTokenSource cts = new();

    public static async Task Main()
    {
        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File($"logs/bot-{DateTime.Now:yyyy-MM-dd}.log")
            .CreateLogger();

        Log.Information("Started program");

        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        try
        {
            Log.Information("[SEED] Starting database update...");
            try
            {
                var seeder = new DbSeeder();
                
                // Настройки запуска:
                // useTable: true (грузить из Google Sheets)
                // useApi: true (грузить из API УрФУ)
                // targetGroups: null (обновить все группы) или new[] { 63804 } для конкретной
                await seeder.SeedAsync(useTable: false, useApi: true, targetGroups: [240207]);
                
                Log.Information("[SEED] Database updated successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SEED] Error during database seeding. Continuing with existing data...");
            }

            using var bot = new Bot();
            var notifier = new Notifier(bot);

            await bot.Initialize(cts);
            await notifier.Start();

            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException)
        {
            Log.Information("Bot stopped gracefully");
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

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Log.Information("Received Ctrl+C, stopping bot...");
        cts.Cancel();
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        Log.Information("Process exit requested, stopping bot...");
        cts.Cancel();
    }
}