using Serilog;

namespace NotiFIITBot;

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
            var bot = new Bot();
            await bot.Initialize(cts);

            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException)
        {
            Log.Information("Bot stopped gracefully");
        }
        catch (Exception ex)
        {
            Log.Fatal($"Unexpected error: {ex}");
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