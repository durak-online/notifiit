using System.Text.RegularExpressions; 
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;
using NotiFIITBot.Repo;
using Serilog;

namespace NotiFIITBot.App;

public class DbSeeder
{
    private readonly ScheduleDbContextFactory _contextFactory = new();

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
            var ranges = new[] { EnvReader.Fiit1Range, EnvReader.Fiit2Range };

            foreach (var currentRange in ranges)
            {
                if (string.IsNullOrWhiteSpace(currentRange))
                {
                    Log.Warning("[SEED] Range is empty in EnvReader, skipping...");
                    continue;
                }

                try 
                {
                    var tableData = TableParser.GetTableData(
                        EnvReader.GoogleApiKey, 
                        EnvReader.TableId, 
                        currentRange
                    ).Where(l => l != null).ToList();

                    if (tableData.Any())
                    {
                        foreach (var l in tableData) 
                            if (l.MenGroup.HasValue) tableGroupNumbers.Add(l.MenGroup.Value);
                        
                        allLessons.AddRange(tableData);
                        Log.Information($"[SEED] Loaded {tableData.Count} lessons from range {currentRange}.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[SEED] Error parsing range {currentRange}: {ex.Message}");
                }
            }
        }

        // 2. ГРУЗИМ API
        if (useApi)
        {
            Log.Information("[SEED] Fetching API group list...");
            
            var apiGroups = new List<ApiParser.Group>();
            apiGroups.AddRange(await ApiParser.GetGroups(1));
            apiGroups.AddRange(await ApiParser.GetGroups(2));
            

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
        
        var uniqueLessons = finalLessons.DistinctBy(l => l.LessonId).ToList();

        Log.Information($"[SEED] Final count to save: {uniqueLessons.Count}");

        await SaveBatchedSafeAsync(uniqueLessons);
    }
    
    private async Task ClearExistingLessonsAsync(int[]? targetGroups)
    {
        await using var context = _contextFactory.CreateDbContext(null);

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
                .Where(l => l.MenGroup == 0 && targetGroups.Contains(l.MenGroup))
                .ExecuteDeleteAsync();
            Log.Information("[SEED-CLEAN] Targeted cleanup finished.");
        }
    }

    private async Task SaveBatchedSafeAsync(List<Lesson> lessons)
    {
        foreach (var batch in lessons.Chunk(100))
        {
            try
            {
                var dbModels = batch.Select(l => new LessonModel
                {
                    LessonId = l.LessonId ?? Guid.NewGuid(),
                    MenGroup = l.MenGroup ?? 0,
                    SubGroup = l.SubGroup,
                    SubjectName = l.SubjectName,
                    TeacherName = l.TeacherName,
                    ClassroomNumber = l.ClassRoom,
                    AuditoryLocation = l.AuditoryLocation,
                    PairNumber = l.PairNumber ?? 0,
                    StartTime = l.Begin ?? TimeOnly.MinValue,
                    DayOfWeek = l.DayOfWeek ?? DayOfWeek.Monday,
                    Evenness = l.EvennessOfWeek
                }).ToList();

                await using var context = _contextFactory.CreateDbContext(null);
                var repo = new ScheduleRepository();
            
                await repo.UpsertLessonsAsync(dbModels);
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
        return match.Success && int.TryParse(match.Value, out number);
    }
}
