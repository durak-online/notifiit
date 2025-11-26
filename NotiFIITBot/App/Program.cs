using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Repo;
using Serilog;

namespace NotiFIITBot.App;

public class Program
{
    private static readonly CancellationTokenSource cts = new();

    public static async Task Main(string[] args)
    {

                // 1️⃣ Создаём контекст через фабрику
                var dbContext = new ScheduleDbContextFactory().CreateDbContext(Array.Empty<string>());
        
                // 2️⃣ Создаём репозиторий
                var repo = new ScheduleRepository(dbContext);
        
                // 3️⃣ Задаём параметры запроса
                int groupNumber = 150801;     // номер группы
                int? subGroup = null;         // подгруппа (null = все)
                var period = IScheduleRepository.SchedulePeriod.Week; // период
        
                // 4️⃣ Получаем список уроков
                var lessons = await repo.GetScheduleAsync(groupNumber, subGroup, period);
        
                // 5️⃣ Формируем строку для вывода
                if (!lessons.Any())
                {
                    Console.WriteLine("Сегодня уроков нет.");
                }
                else
                {
                    // Пример вывода: "1. Math (Prof. Ivanov) в 101"
                    foreach (var l in lessons)
                    {
                        string line = $"{l.DayOfWeek} {l.PairNumber} пара. {l.MenGroup} {l.SubGroup} подгруппа, {l.SubjectName} ({l.TeacherName ?? "-"}) в {l.ClassroomNumber ?? "-"}";
                        Console.WriteLine(line);
                    }
                }
            }
        }

//         ConfigureLogging();
//         Log.Information("[START] Program started");
//         try 
//         {
//             EnvReader.Load(".env"); 
//         }
//         catch (Exception ex)
//         {
//             Log.Warning($"[ENV] Could not load .env file: {ex.Message}. Assuming variables are set in system.");
//         }
//
//         Console.CancelKeyPress += OnCancelKeyPress;
//         AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
//
//         try
//         {
//             // КОНФИГУРАЦИЯ ЗАПУСКА
//             var useTable = false;  // Грузить таблицу 
//             var useApi = true;   // Грузить API
//             int[] groupsToParse = [150801];
//             
//             Log.Information("[STEP 1] Starting database seeding...");
//             
//             var seeder = new DbSeeder();
//             await seeder.SeedAsync(useTable, useApi, groupsToParse);
//
//             Log.Information("[STEP 2] Database seeding completed successfully!");
//             /*
//             // ❌ Временно закомментировано, чтобы бот не запускался
//             Log.Information("[STEP 3] Initializing bot...");
//             using var bot = new Bot();
//             var notifier = new Notifier(bot);
//
//             await bot.Initialize(cts);
//             await notifier.Start();
//
//             Log.Information("[STEP 4] Bot started successfully");
//             await Task.Delay(Timeout.Infinite, cts.Token);
//             */
//         }
//         catch (Exception ex)
//         {
//             Log.Fatal("[ERROR] Unexpected error occurred: {ErrorMessage}", ex.Message);
//             Log.Debug("[ERROR] Full exception: {Exception}", ex);
//         }
//         finally
//         {
//             Log.Information("[END] Program finished");
//             Log.CloseAndFlush();
//         }
//     }
//     
//     private static void ConfigureLogging()
//     {
//         if (!Directory.Exists("logs")) Directory.CreateDirectory("logs");
//
//         Log.Logger = new LoggerConfiguration()
//             .WriteTo.Console()
//             .WriteTo.File($"logs/bot-{DateTime.Now:yyyy-MM-dd}.log")
//             .CreateLogger();
//     }
//
//     private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
//     {
//         e.Cancel = true;
//         Log.Information("[CANCEL] Ctrl+C pressed, stopping...");
//         cts.Cancel();
//     }
//
//     private static void OnProcessExit(object? sender, EventArgs e)
//     {
//         Log.Information("[EXIT] Process exit requested, stopping...");
//         cts.Cancel();
//     }
// }
    