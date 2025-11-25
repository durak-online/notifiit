using NotiFIITBot.Consts;
using NotiFIITBot.Repo;
using Serilog;

namespace NotiFIITBot.App;

public class Program
{
    private static readonly CancellationTokenSource cts = new();

    public static async Task Main(string[] args)
    {
        ConfigureLogging();
        Log.Information("[START] Program started");
        try 
        {
            EnvReader.Load(".env"); 
        }
        catch (Exception ex)
        {
            Log.Warning($"[ENV] Could not load .env file: {ex.Message}. Assuming variables are set in system.");
        }

        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        try
        {
            // КОНФИГУРАЦИЯ ЗАПУСКА
            var useTable = true;  // Грузить таблицу 
            var useApi = false;   // Грузить API
            int[] groupsToParse = [];
            
            Log.Information("[STEP 1] Starting database seeding...");
            
            var seeder = new DbSeeder();
            await seeder.SeedAsync(useTable, useApi, groupsToParse);

            Log.Information("[STEP 2] Database seeding completed successfully!");
            /*
            // ❌ Временно закомментировано, чтобы бот не запускался
            Log.Information("[STEP 3] Initializing bot...");
            using var bot = new Bot();
            var notifier = new Notifier(bot);

            await bot.Initialize(cts);
            await notifier.Start();

            Log.Information("[STEP 4] Bot started successfully");
            await Task.Delay(Timeout.Infinite, cts.Token);
            */
        }
        catch (Exception ex)
        {
            Log.Fatal("[ERROR] Unexpected error occurred: {ErrorMessage}", ex.Message);
            Log.Debug("[ERROR] Full exception: {Exception}", ex);
        }
        finally
        {
            Log.Information("[END] Program finished");
            Log.CloseAndFlush();
        }
    }
    
    private static void ConfigureLogging()
    {
        if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File($"logs/bot-{DateTime.Now:yyyy-MM-dd}.log")
            .CreateLogger();
    }

    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Log.Information("[CANCEL] Ctrl+C pressed, stopping...");
        cts.Cancel();
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        Log.Information("[EXIT] Process exit requested, stopping...");
        cts.Cancel();
    }
}