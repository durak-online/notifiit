using System.Text.RegularExpressions; 
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;
using Serilog;

namespace NotiFIITBot.Repo;

public class DbSeeder
{
    private static string ConnectionString => 
        $"Host=localhost;Port=5433;Database={EnvReader.PostgresDbName};Username={EnvReader.PostgresUser};Password={EnvReader.PostgresPassword}";
    
    private const string ApiKey = "AIzaSyDSC8k2yVH-OZvJE7ksssWeUxem04c2kPM";
    private const string SpreadsheetId = "1pj8fzVqrZVkNssSJiInxy_Cm54ddC8tm8eluMdV-XvM";
    private const string Range = "ФИИТ-1, с 15.09!A1:J84";

    public async Task SeedAsync(bool useTable, bool useApi, int[]? targetGroups = null)
    {
        Log.Information("[SEED] Starting Unified Seeding...");
        
        if (string.IsNullOrEmpty(EnvReader.PostgresPassword))
        {
            Log.Fatal("[SEED] CRITICAL: Env variables not loaded.");
            return;
        }

        await ClearExistingLessonsAsync(targetGroups);
        var allLessons = new List<Lesson>();
        var tableGroupNumbers = new HashSet<int>();

        // 1. ГРУЗИМ ТАБЛИЦУ
        if (useTable)
        {
            var tableData = TableParser.GetTableData(ApiKey, SpreadsheetId, Range, targetGroups)
                .Where(l => l != null).ToList();

            if (tableData.Any())
            {
                foreach (var l in tableData) 
                    if (l.MenGroup.HasValue) tableGroupNumbers.Add(l.MenGroup.Value);
                
                allLessons.AddRange(tableData);
                Log.Information($"[SEED] Loaded {tableData.Count} lessons from Table.");
            }
        }

        // 2. ГРУЗИМ API
        if (useApi)
        {
            Log.Information("[SEED] Fetching API group list...");
            
            var apiGroups = new List<NotiFIITBot.Domain.ApiParser.Group>();
            apiGroups.AddRange(await ApiParser.GetGroups(1));

            Log.Information($"[SEED] Found {apiGroups.Count} groups in API.");

            foreach (var group in apiGroups)
            {
                if (!TryParseGroupNumberRegex(group.title, out int gNum)) continue;
                
                if (targetGroups != null && targetGroups.Length > 0 && !targetGroups.Contains(gNum)) continue; 
                if (tableGroupNumbers.Contains(gNum)) continue; 

                Log.Information($"[SEED] Loading API for {gNum} (Internal ID: {group.id})...");
                
                var sub1 = (await ApiParser.GetLessons(group.id, 1)).ToList();
                var sub2 = (await ApiParser.GetLessons(group.id, 2)).ToList();

                sub1.ForEach(l => l.MenGroup = gNum);
                sub2.ForEach(l => l.MenGroup = gNum);

                allLessons.AddRange(sub1);
                allLessons.AddRange(sub2);
            }
        }

        if (!allLessons.Any())
        {
            Log.Warning("[SEED] No lessons found. Exiting.");
            return;
        }

        Log.Information($"[SEED] Processing {allLessons.Count} raw lessons...");

        var lessonsSubNormalized = LessonProcessor.NormalizeSubgroups(allLessons);
        var finalLessons = LessonProcessor.MergeByParity(lessonsSubNormalized).ToList();
        LessonProcessor.AssignStableIds(finalLessons);
        
        // Убираем полные дубликаты
        var uniqueLessons = finalLessons.DistinctBy(l => l.LessonId).ToList();

        Log.Information($"[SEED] Final count to save: {uniqueLessons.Count}");

        await SaveBatchedSafeAsync(uniqueLessons);
    }
    
    private async Task ClearExistingLessonsAsync(int[]? targetGroups)
    {
        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var context = new ScheduleDbContext(options);

        if (targetGroups == null || targetGroups.Length == 0)
        {
            Log.Warning("[SEED-CLEAN] Cleaning ALL lessons table...");
            await context.Set<LessonModel>().ExecuteDeleteAsync();
            Log.Information("[SEED-CLEAN] All lessons deleted.");
        }
        else
        {
            Log.Information($"[SEED-CLEAN] Cleaning lessons for groups: {string.Join(", ", targetGroups)}...");
            await context.Set<LessonModel>()
                .Where(l => l.MenGroup.HasValue && targetGroups.Contains(l.MenGroup.Value))
                .ExecuteDeleteAsync();
            Log.Information("[SEED-CLEAN] Targeted cleanup finished.");
        }
    }

    private async Task SaveBatchedSafeAsync(List<Lesson> lessons)
    {
        var options = new DbContextOptionsBuilder<ScheduleDbContext>().UseNpgsql(ConnectionString).Options;

        foreach (var batch in lessons.Chunk(100))
        {
            try
            {
                await using var context = new ScheduleDbContext(options);
                var repo = new ScheduleRepository(context);
                await repo.UpsertLessonsAsync(batch);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error($"[SEED] Batch save error: {ex.Message}");
            }
        }
        Log.Information("[SEED] Seeding finished successfully.");
    }

    private static bool TryParseGroupNumberRegex(string title, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(title)) return false;

        // Ищем любые 6 цифр подряд 
        var match = Regex.Match(title, @"\d{6}");
        if (match.Success)
        {
            return int.TryParse(match.Value, out number);
        }
        return false;
    }
}