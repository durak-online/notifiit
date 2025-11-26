using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Consts;
using NotiFIITBot.Repo;
using Xunit;

namespace NotiFIITBot.Tests
{
    public class ScheduleRepositoryTests
    {
        // --- Вспомогательный метод для создания In-Memory контекста ---
        private ScheduleDbContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ScheduleDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;

            return new ScheduleDbContext(options);
        }

        // --- Вспомогательные тестовые данные ---
        private List<LessonModel> GetSampleLessons() => new()
        {
            new LessonModel
            {
                LessonId = Guid.NewGuid(),
                MenGroup = 353001,
                SubGroup = null,
                DayOfWeek = DayOfWeek.Monday,
                PairNumber = 1,
                Evenness = Evenness.Even,
                SubjectName = "Math"
            },
            new LessonModel
            {
                LessonId = Guid.NewGuid(),
                MenGroup = 353001,
                SubGroup = 1,
                DayOfWeek = DayOfWeek.Monday,
                PairNumber = 2,
                Evenness = Evenness.Odd,
                SubjectName = "Physics"
            },
            new LessonModel
            {
                LessonId = Guid.NewGuid(),
                MenGroup = 353002,
                SubGroup = null,
                DayOfWeek = DayOfWeek.Tuesday,
                PairNumber = 1,
                Evenness = Evenness.Even,
                SubjectName = "English"
            }
        };

        // --- Тест чтения уроков ---
        [Fact]
        public async Task GetSchedule_ReturnsCorrectLessons_ForGivenGroup()
        {
            // Arrange
            var ctx = CreateInMemoryContext(nameof(GetSchedule_ReturnsCorrectLessons_ForGivenGroup));
            ctx.Lessons.AddRange(GetSampleLessons());
            await ctx.SaveChangesAsync();

            var repo = new ScheduleRepository(ctx);

            // Дата: понедельник, 10 февраля 2025 (ISO неделя 7, нечётная)
            var monday = new DateTime(2025, 2, 10);

            // Act
            var lessons = await repo.GetScheduleAsync(
                groupNumber: 353001,
                subGroup: null,
                IScheduleRepository.SchedulePeriod.Today,
                now: monday
            );

            // Assert
            // Урок с Evenness = Odd (Physics) должен быть возвращён
            Xunit.Assert.Single(lessons);
            Xunit.Assert.Equal("Physics", lessons.First().SubjectName);
        }

        // --- Тест добавления урока через UpsertLessonsAsync ---
        [Fact]
        public async Task UpsertLessons_AddsNewLesson()
        {
            // Arrange
            var ctx = CreateInMemoryContext(nameof(UpsertLessons_AddsNewLesson));
            var repo = new ScheduleRepository(ctx);

            var newLesson = new NotiFIITBot.Domain.Lesson(
                pairNumber: 1,
                subjectName: "Biology",
                teacherName: null,
                classRoom: null,
                begin: null,
                end: null,
                auditoryLocation: null,
                subGroup: 0,
                menGroup: 353003,
                evennessOfWeek: Evenness.Even,
                dayOfWeek: DayOfWeek.Wednesday
            )
            {
                LessonId = Guid.NewGuid() // Обязательно для UpsertLessonsAsync!
            };

            // Act
            var result = await repo.UpsertLessonsAsync(new[] { newLesson });

            // Assert
            Xunit.Assert.Single(result);
            Xunit.Assert.Equal("Biology", result.First().SubjectName);
            Xunit.Assert.Equal(353003, result.First().MenGroup);

            // Проверим, что урок реально добавился в контекст
            var savedLesson = await ctx.Lessons.FindAsync(newLesson.LessonId);
            Xunit.Assert.NotNull(savedLesson);
            Xunit.Assert.Equal("Biology", savedLesson.SubjectName);
        }
    }
}
