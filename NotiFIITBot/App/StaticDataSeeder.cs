using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using Serilog;

namespace NotiFIITBot.App;

// класс для первичного наполнения БД статичными данными при старте
public class StaticDataSeeder
{
    private static string ConnectionString => 
        $"Host=localhost;Port=5433;Database={EnvReader.PostgresDbName};Username={EnvReader.PostgresUser};Password={EnvReader.PostgresPassword}";
    
    public async Task SeedAsync()
    {
        Log.Information("[SEED-STATIC] Starting static data seeding...");
        
        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new ScheduleDbContext(options);

        await SeedParityConfig(context);
        
        await context.SaveChangesAsync(); 
        
        Log.Information("[SEED-STATIC] Static data seeded successfully.");
    }

    /// <summary>
    /// Настраивает точку отсчета для четности недель.
    /// 1. Смотрит на текущую дату.
    /// 2. Если месяц >= августа, считаем, что это осенний семестр (начало 1 сентября).
    /// 3. Иначе — весенний (начало 10 февраля).
    /// 4. Находит первый понедельник семестра, чтобы от него считать недели.
    /// </summary>
    private async Task SeedParityConfig(ScheduleDbContext context)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var currentYear = today.Year;
        var startOfSemester = today.Month >= 8 
            ? new DateOnly(currentYear, 9, 1) 
            : new DateOnly(currentYear, 2, 10);

        var diff = (7 + (startOfSemester.DayOfWeek - DayOfWeek.Monday)) % 7;
        var firstMonday = startOfSemester.AddDays(-1 * diff);

        var existingConfig = await context.WeekParityConfigs.FindAsync(Evenness.Odd); 

        if (existingConfig == null)
        {
            context.WeekParityConfigs.Add(new WeekParityConfig { Parity = Evenness.Odd, FirstMonday = firstMonday });
        }
        else if (existingConfig.FirstMonday != firstMonday)
        {
            existingConfig.FirstMonday = firstMonday;
        }
    }
    
}