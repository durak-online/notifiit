using System.Text.RegularExpressions; 
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;
using NotiFIITBot.Logging;
using NotiFIITBot.Repo;
using Serilog;

namespace NotiFIITBot.App;

public class DbSeeder
{
    private readonly ScheduleDbContext _context;
    private readonly IScheduleRepository _scheduleRepository;
    private readonly ILogger logger;

    public DbSeeder(ScheduleDbContext context, IScheduleRepository scheduleRepository, ILoggerFactory loggerFactory)
    {
        _context = context;
        _scheduleRepository = scheduleRepository;
        logger = loggerFactory.CreateLogger("SEED");
    }

    public async Task SeedAsync(bool useTable, bool useApi, int[]? targetGroups = null)
    {
        logger.Information("Starting Unified Seeding...");
        
        if (string.IsNullOrEmpty(EnvReader.PostgresPassword))
        {
            logger.Fatal("CRITICAL: Env variables not loaded");
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
                    logger.Warning("Range is empty in EnvReader, skipping...");
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
                        logger.Information($"Loaded {tableData.Count} lessons from range {currentRange}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"Error parsing range {currentRange}: {ex.Message}");
                }
            }
        }

        // 2. ГРУЗИМ API
        if (useApi)
        {
            logger.Information("Fetching API group list...");
            
            var apiGroups = new List<ApiParser.Group>();
            apiGroups.AddRange(await ApiParser.GetGroups(1));
            apiGroups.AddRange(await ApiParser.GetGroups(2));
            

            logger.Information($"Found {apiGroups.Count} groups in API");

            foreach (var group in apiGroups)
            {
                if (!TryParseGroupNumberRegex(group.title, out int gNum)) continue;
                
                if (targetGroups != null && targetGroups.Length > 0 && !targetGroups.Contains(gNum)) continue; 
                if (tableGroupNumbers.Contains(gNum)) continue; 

                logger.Information($"Loading API for {gNum} (Internal ID: {group.id})...");
                
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
            logger.Warning("No lessons found. Exiting");
            return;
        }

        logger.Information($"Processing {allLessons.Count} raw lessons...");

        var lessonsSubNormalized = LessonProcessor.NormalizeSubgroups(allLessons);
        var finalLessons = LessonProcessor.MergeByEvenness(lessonsSubNormalized).ToList();
        LessonProcessor.AssignStableIds(finalLessons);
        
        var uniqueLessons = finalLessons.DistinctBy(l => l.LessonId).ToList();

        logger.Information($"Final count to save: {uniqueLessons.Count}");

        await SaveBatchedSafeAsync(uniqueLessons);
    }
    
    private async Task ClearExistingLessonsAsync(int[]? targetGroups)
    {
        if (targetGroups == null || targetGroups.Length == 0)
        {
            logger.Warning("[SEED-CLEAN] Cleaning ALL lessons table...");
            await _context.Set<LessonModel>().ExecuteDeleteAsync();
            logger.Information("[SEED-CLEAN] All lessons deleted");
        }
        else
        {
            logger.Information($"[SEED-CLEAN] Cleaning lessons for groups: {string.Join(", ", targetGroups)}...");
            await _context.Set<LessonModel>()
                .Where(l => l.MenGroup == 0 && targetGroups.Contains(l.MenGroup))
                .ExecuteDeleteAsync();
            logger.Information("[SEED-CLEAN] Targeted cleanup finished");
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

                await _scheduleRepository.UpsertLessonsAsync(dbModels);
            }
            catch (Exception ex)
            {
                logger.Error($"Batch save error: {ex.Message}");
            }
        }
        logger.Information("Seeding finished successfully");
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
