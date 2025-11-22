using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Repo;
using NotiFIITBot.Domain;

namespace NotiFIITBot.Tests;

public class ScheduleRepositoryIntegrationTests : IDisposable
{
    private readonly ScheduleDbContext _context;
    private readonly ScheduleRepository _repo;

    public ScheduleRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ScheduleDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=notifiit_db;Username=notifiit_admin;Password=226381194")
            .Options;

        _context = new ScheduleDbContext(options);
        _repo = new ScheduleRepository(_context);
    }

    [Test]
    public async Task UpsertLesson_ShouldAddAndRemoveLessonInDatabase()
    {
        // Arrange
        var lesson = new Lesson(
            pairNumber: 1,
            subjectName: "Тестовая математика",
            teacherName: "Тестов И.И.",
            classRoom: "999",
            begin: new TimeOnly(9, 0),
            end: new TimeOnly(10, 30),
            auditoryLocation: "Тестовая аудитория",
            subGroup: 1,
            menGroup: 101,
            evennessOfWeek: NotiFIITBot.Consts.Evenness.Even,
            dayOfWeek: DayOfWeek.Monday
        );

        // Act
        var savedLesson = await _repo.UpsertLessonAsync(lesson);
        await _context.SaveChangesAsync();

        // Assert
        Assert.NotNull(savedLesson);
        Assert.True(savedLesson.LessonId > 0);

        var fromDb = await _context.Lessons.FirstOrDefaultAsync(l => l.LessonId == savedLesson.LessonId);
        Assert.NotNull(fromDb);
        Assert.That("Тестовая математика" == fromDb.SubjectName);
        
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
