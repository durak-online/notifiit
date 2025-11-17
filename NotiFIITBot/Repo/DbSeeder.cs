﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Repositories;
using NotiFIITBot.Domain;
using Serilog;

namespace NotiFIITBot.Repo
{
    public static class DbSeeder
    {
        private static readonly string ConnectionString =
            "Host=localhost;Port=5433;Database=notifiit_db;Username=notifiit_admin;Password=226381194";

        // Группы и подгруппы для парсинга
        private static readonly int[] Groups = {240801}; // вместо этого будем из парсера вызывать метод для получения групп
        private static readonly int[] SubGroups = {1, 2, 3, 4};//TODO: а если их 3?
        private const int DaysToParse = 14;
        private const int MaxPairsPerDay = 7;
        private const int RequestDelayMs = 150; 

        public static async Task SeedDatabase()
        {
            Log.Information("[SEED] Starting lessons-only database seeding...");

            try
            {
                var options = new DbContextOptionsBuilder<ScheduleDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options;

                await using var context = new ScheduleDbContext(options);
                var repo = new ScheduleRepository(context);

                var allLessons = new List<Lesson>();
                //---------------------
                /*var allLessons = Parser.GetLessons();
                await repo.UpsertLessonsAsync(allLessons);
                await context.SaveChangesAsync();*/
                //-----------------------
                var startDate = DateOnly.FromDateTime(DateTime.Now);

                foreach (var group in Groups)
                {
                    int groupId = -1;
                    try
                    {
                        groupId = await ApiParser.GetGroupId(group);
                        Log.Debug("[SEED] Resolved group {Group} -> groupId={GroupId}", group, groupId);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[SEED] Failed to get groupId for {Group}: {Msg}", group, ex.Message);
                    }

                    for (int dayOffset = 0; dayOffset < DaysToParse; dayOffset++)
                    {
                        var date = startDate.AddDays(dayOffset);

                        foreach (var subGroup in SubGroups)
                        {
                            for (int pair = 1; pair <= MaxPairsPerDay; pair++)
                            {
                                try
                                {
                                    var lesson = await ApiParser.GetLesson(group, date, pair, subGroup);

                                    if (lesson != null)
                                    {
                                        allLessons.Add(lesson);
                                        Log.Information("[PARSE OK] group={Group} subGroup={SubGroup} date={Date:yyyy-MM-dd} pair={Pair} subject=\"{Subject}\"",
                                            group, subGroup, date, pair, lesson.SubjectName);
                                    }
                                    else
                                    {
                                        Log.Warning("[PARSE NULL] group={Group} subGroup={SubGroup} date={Date:yyyy-MM-dd} pair={Pair} lesson=null",
                                            group, subGroup, date, pair);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error("[PARSE ERROR] group={Group} subGroup={SubGroup} date={Date:yyyy-MM-dd} pair={Pair} error={Msg}",
                                        group, subGroup, date, pair, ex.Message);
                                }

                                await Task.Delay(RequestDelayMs);
                            }
                        }
                    }
                }

                Log.Information("[SEED] Total lessons parsed: {Count}", allLessons.Count);

                if (allLessons.Count > 0)
                {
                    await repo.UpsertLessonsAsync(allLessons);
                    await context.SaveChangesAsync();
                    Log.Information("[SEED] Lessons successfully saved to DB!");
                }
                else
                {
                    Log.Warning("[SEED] No lessons parsed — nothing saved.");
                }
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
    }
}