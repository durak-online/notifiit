using NotiFIITBot.Consts;
using Serilog;

namespace NotiFIITBot.App;

public class Program
{
    private static readonly CancellationTokenSource Cts = new();

    public static async Task Main(string[] args)
    {
        ConfigureLogging();
        Log.Information("[APP] Starting NotiFIITBot Application...");

        try
        {
            // Загружаем переменные окружения (.env)
            try
            {
                EnvReader.Load(".env");
            }
            catch (Exception ex)
            {
                Log.Warning($"[ENV] .env file issue: {ex.Message}. Relying on system env vars.");
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Log.Information("[STOP] Stop signal received...");
                Cts.Cancel();
            };

            // Запуск обновления базы данных (Seeder)
            Log.Information("[SEED] Starting database update...");
            try
            {
                var seeder = new DbSeeder();
                // Настройки можно вынести в конфиг или аргументы запуска
                bool useTable = false; 
                bool useApi = true;
                int[] groupsToUpdate = [150801]; // Можно сделать null для всех групп

                await seeder.SeedAsync(useTable, useApi, groupsToUpdate);
                Log.Information("[SEED] Database updated successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SEED] Error during database seeding. Continuing with existing data...");
            }

            //  Инициализация и запуск Бота
            Log.Information("[BOT] Initializing Telegram Bot...");
            using var bot = new Bot();
            await bot.Initialize(Cts);

            //  Запуск планировщика уведомлений
            Log.Information("[NOTIFIER] Starting notification scheduler...");
            var notifier = new Notifier(bot);
            await notifier.Start();

            Log.Information("[APP] Bot is running. Press Ctrl+C to stop.");

            // Держим приложение запущенным, пока не отменят (Ctrl+C)
            await Task.Delay(Timeout.Infinite, Cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Information("[APP] Application stopped gracefully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[APP] Critical error. Application terminated.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureLogging()
    {
        if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/log-{DateTime.Now:yyyy-MM-dd}.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }
}