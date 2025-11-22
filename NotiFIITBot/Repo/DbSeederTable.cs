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
    public static class TableDbSeeder
    {
        private static readonly string ConnectionString =
            "Host=localhost;Port=5433;Database=notifiit_db;Username=notifiit_admin;Password=226381194";

        public static async Task SeedDatabaseFromTable(bool stopAfterFirstGroup = false)
        {
            Log.Information("[SEED-TABLE] Starting table-based seeding...");

            try
            {
                var options = new DbContextOptionsBuilder<ScheduleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options;

                await using var context = new ScheduleDbContext(options);
                var repo = new ScheduleRepository(context);

                // Получаем все уроки из таблицы
                var apiKey = "ВАШ_НОВЫЙ_API_KEY";
                var spreadsheetId = "1pj8fzVqrZVkNssSJiInxy_Cm54ddC8tm8eluMdV-XvM";
                var range = "ФИИТ-1, с 15.09!A1:J84"; // пример для 1 курса

                var lessonsFromTable = TableParser.GetTableData(apiKey, spreadsheetId, range);

                if (!lessonsFromTable.Any())
                {
                    Log.Warning("[SEED-TABLE] No lessons found in table.");
                    return;
                }

                Log.Information("[SEED-TABLE] Total lessons found in table: {Count}", lessonsFromTable.Count);

                // Сначала объединяем уроки, чтобы правильно выставить Evenness
                var mergedLessons = ChangeParity(lessonsFromTable).ToList();

                // Формируем LessonId
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

                // Сохраняем в базу
                try
                {
                    Log.Information("[SEED-TABLE] Saving {Count} lessons to DB...", mergedLessons.Count);
                    await repo.UpsertLessonsAsync(mergedLessons);
                    await context.SaveChangesAsync();
                    Log.Information("[SEED-TABLE] Saved lessons to DB.");
                }
                catch (Exception ex)
                {
                    Log.Error("[SEED-TABLE] Failed to save lessons to DB: {Msg}", ex.Message);
                }

                Log.Information("[SEED-TABLE] Table-based seeding finished.");
            }
            catch (Exception ex)
            {
                Log.Fatal("[SEED-TABLE] Fatal error: {Msg}", ex.Message);
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
