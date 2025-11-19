﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Repositories;
using NotiFIITBot.Domain;
using Serilog;
using NotiFIITBot.Consts;

namespace NotiFIITBot.Repo
{
    public static class DbSeeder
    {
        private static readonly string ConnectionString =
            "Host=localhost;Port=5433;Database=notifiit_db;Username=notifiit_admin;Password=226381194";

        // Группы и подгруппы для парсинга
        private static readonly int[] Groups = {240801}; // вместо этого будем из парсера вызывать метод для получения групп
        private static readonly int[] SubGroups = {1, 2};//TODO: а если их 3?
        private const int DaysToParse = 14;
        private const int MaxPairsPerDay = 3;
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
        var startDate = DateOnly.FromDateTime(DateTime.Now);

        // -----------------------
        // Парсинг расписания (как раньше)
        // -----------------------
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

        Log.Information("[SEED] Total lessons parsed raw: {Count}", allLessons.Count);

        // -----------------------
        // MergeLessons: объединяем уроки с одинаковой (день, пара, предмет)
        // чтобы сформировать ParityList = [0,1] если есть both
        // -----------------------
        List<Lesson> MergeLessons(List<Lesson> lessons)
        {
            static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

            var merged = lessons
                .GroupBy(l => new
                {
                    Day = l.DayOfWeek,
                    Pair = l.PairNumber,
                    Subject = Norm(l.SubjectName),
                    // при желании можно добавить Teacher/ClassRoom/AuditoryLocation в ключ
                })
                .Select(g =>
                {
                    var first = g.First();

                    // Собирам все parity из группы (включая fallback по EvennessOfWeek)
                    var parityInts = g
                        .SelectMany(x =>
                        {
                            if (x.ParityList != null && x.ParityList.Any()) return x.ParityList;
                            return x.EvennessOfWeek switch
                            {
                                Evenness.Even => new List<int> { 0 },
                                Evenness.Odd => new List<int> { 1 },
                                Evenness.Always => new List<int> { 0, 1 },
                                _ => new List<int> { 0 }
                            };
                        })
                        .Distinct()
                        .OrderBy(i => i)
                        .ToList();

                    // Выбираем лучшие значения полей (первый непустой)
                    string? pickFirstNonEmptyString(IEnumerable<string?> seq) => seq.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                    TimeOnly? pickFirstTime(IEnumerable<TimeOnly?> seq) => seq.FirstOrDefault(t => t.HasValue);
                    int? pickFirstInt(IEnumerable<int?> seq) => seq.FirstOrDefault(v => v.HasValue);

                    var mergedLesson = new Lesson(
                        first.PairNumber,
                        first.SubjectName,
                        pickFirstNonEmptyString(g.Select(x => x.TeacherName)),
                        pickFirstNonEmptyString(g.Select(x => x.ClassRoom)),
                        pickFirstTime(g.Select(x => x.Begin)),
                        pickFirstTime(g.Select(x => x.End)),
                        pickFirstNonEmptyString(g.Select(x => x.AuditoryLocation)),
                        pickFirstInt(g.Select(x => x.SubGroup)) ?? first.SubGroup ?? 0,
                        pickFirstInt(g.Select(x => x.MenGroup)) ?? first.MenGroup ?? 0,
                        parityInts.Contains(0) && parityInts.Contains(1) ? Evenness.Always :
                            (parityInts.Contains(0) ? Evenness.Even : Evenness.Odd),
                        first.DayOfWeek
                    );

                    mergedLesson.ParityList = parityInts;

                    return mergedLesson;
                })
                .ToList();

            return merged;
        }

        var mergedLessons = MergeLessons(allLessons);

        Log.Information("[SEED] Lessons after merge: {Before} -> {After}", allLessons.Count, mergedLessons.Count);

        if (mergedLessons.Count > 0)
        {
            await repo.UpsertLessonsAsync(mergedLessons);
            // UpsertLessonsAsync делает SaveChanges внутри; дополнительный SaveChanges не обязателен,
            // но оставим для совместимости (можно убрать при желании)
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