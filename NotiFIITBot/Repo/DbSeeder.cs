using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Repo;
using NotiFIITBot.Domain;
using Serilog;
using NotiFIITBot.Consts;

namespace NotiFIITBot.Repo
{
    public static class ApiDbSeeder
    {
        private static readonly string ConnectionString =
            "Host=localhost;Port=5433;Database=notifiit_db;Username=notifiit_admin;Password=226381194";

        public static async Task SeedDatabaseApi(bool stopAfterFirstGroup = false, int? singleGroupNumber = null)
        {
            Log.Information("[SEED] Starting lessons-only database seeding...");

            try
            {
                var options = new DbContextOptionsBuilder<ScheduleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options;

                await using var context = new ScheduleDbContext(options);
                var repo = new ScheduleRepository(context);

                var allGroups = await ApiParser.GetGroups(1); // пример: курс 1
                Log.Information("[SEED] Total groups found: {Count}", allGroups.Count);

                if (singleGroupNumber.HasValue)
                {
                    allGroups = allGroups
                        .Where(g =>
                        {
                            if (string.IsNullOrWhiteSpace(g.title)) return false;
                            if (!g.title.StartsWith("МЕН-")) return false;
                            if (!int.TryParse(g.title.Replace("МЕН-", ""), out var num)) return false;
                            return num == singleGroupNumber.Value;
                        })
                        .ToList();
                    Log.Information("[SEED] singleGroupNumber specified: {Group}. Groups to process: {Count}",
                        singleGroupNumber, allGroups.Count);
                }

                foreach (var group in allGroups)
                {
                    Log.Information("[SEED] Processing group {Title}", group.title);

                    var groupLessons = new List<Lesson>();

                    foreach (var subGroup in new[] { 1, 2 })
                    {
                        try
                        {
                            if (!int.TryParse(group.title.Replace("МЕН-", ""), out var groupNumber))
                            {
                                Log.Warning("[SEED] Cannot parse numeric group from title {Title}, skipping subgroup {SubGroup}",
                                    group.title, subGroup);
                                continue;
                            }

                            var lessons = (await ApiParser.GetLessons(groupNumber, subGroup)).ToList();
                            groupLessons.AddRange(lessons);

                            foreach (var lesson in lessons)
                            {
                                Log.Information("[SEED] Got lesson: Group={Group} SubGroup={SubGroup} Subject={Subject} Day={Day} Pair={Pair} Evenness={Evenness}",
                                    group.title, subGroup, lesson.SubjectName, lesson.DayOfWeek, lesson.PairNumber, lesson.EvennessOfWeek);
                            }

                            Log.Information("[SEED] Group {Group} subGroup {SubGroup} parsed {Count} lessons",
                                group.title, subGroup, lessons.Count);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[SEED] Failed parsing group {Group} subGroup {SubGroup}: {Msg}", group.title,
                                subGroup, ex.Message);
                        }
                    }

                    Log.Information("[SEED] Total lessons fetched for group {Group}: {Count}", group.title,
                        groupLessons.Count);

                    if (!groupLessons.Any())
                    {
                        Log.Warning("[SEED] No lessons for group {Group}, skipping save", group.title);
                        if (stopAfterFirstGroup) break;
                        continue;
                    }

                    // Применяем новый метод ChangeParity для корректного определения parity
                    var mergedLessons = ChangeParity(groupLessons).ToList();

                    // Формируем LessonId для каждой записи
                    foreach (var lesson in mergedLessons)
                    {
                        int gNum = lesson.MenGroup ?? 0;
                        int sg = lesson.SubGroup ?? 0;
                        int parityInt = lesson.EvennessOfWeek switch
                        {
                            Evenness.Even => 0,
                            Evenness.Odd => 1,
                            Evenness.Always => 2,
                            _ => 0
                        };
                        int day = (int)(lesson.DayOfWeek ?? DayOfWeek.Monday);
                        int pair = lesson.PairNumber ?? 0;

                        lesson.LessonId = gNum * 10000 + sg * 1000 + parityInt * 100 + day * 10 + pair;
                    }

                    if (mergedLessons.Any())
                    {
                        try
                        {
                            Log.Information("[SEED] Saving {Count} merged lessons for group {Group} to DB...",
                                mergedLessons.Count, group.title);
                            await repo.UpsertLessonsAsync(mergedLessons);
                            await context.SaveChangesAsync();
                            Log.Information("[SEED] Saved group {Group} to DB.", group.title);
                        }
                        catch (Exception ex)
                        {
                            Log.Error("[SEED] Failed to save group {Group} to DB: {Msg}", group.title, ex.Message);
                        }
                    }

                    if (stopAfterFirstGroup) break;
                }

                Log.Information("[SEED] Seeding loop finished.");
            }
            catch (Exception ex)
            {
                Log.Fatal("[SEED] Fatal error: {Msg}", ex.Message);
                Log.Debug("[SEED] Exception detail: {Ex}", ex);
            }
            finally
            {
                Log.Information("[SEED] Lessons-only seeding finished.");
            }
        }

        private static IEnumerable<Lesson> ChangeParity(IEnumerable<Lesson> lessons)
        {
            var groups = lessons.GroupBy(l =>
                $"{l.SubjectName}-{l.TeacherName}-{l.ClassRoom}-{l.PairNumber}-{l.SubGroup}-{l.MenGroup}");
            var result = new List<Lesson>();

            foreach (var group in groups)
            {
                var list = group.ToList();
                if (list.Count == 1)
                {
                    result.Add(list[0]);
                    continue;
                }
                var hasOdd = list.Any(x => x.EvennessOfWeek == Evenness.Odd);
                var hasEven = list.Any(x => x.EvennessOfWeek == Evenness.Even);
                var merged = list[0];
                if (hasOdd && hasEven)
                    merged.EvennessOfWeek = Evenness.Always;
                result.Add(merged);
            }
            return result;
        }
    }
}
