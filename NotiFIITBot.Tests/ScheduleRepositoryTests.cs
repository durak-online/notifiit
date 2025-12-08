using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Repo;
using NUnit.Framework;

namespace NotiFIITBot.Tests
{
    [TestFixture]
    public class ScheduleRepositoryUnitTests
    {
        private ScheduleDbContext _context = null!;
        private ScheduleRepository _repo = null!;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ScheduleDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            _context = new ScheduleDbContext(options);
            _repo = new ScheduleRepository(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
        }

        [Test]
        public async Task UpsertLessonsAsync_ShouldAddLesson_WhenNotExists()
        {
            var guid = Guid.NewGuid();

            var model = new LessonModel
            {
                LessonId = guid,
                Evenness = Evenness.Even,
                PairNumber = 2,
                SubjectName = "Тестовая математика",
                TeacherName = "Тестов И.И.",
                ClassroomNumber = "101",
                MenGroup = 240801,
                SubGroup = 1,
                DayOfWeek = DayOfWeek.Monday
            };

            var result = await _repo.UpsertLessonsAsync(new[] { model });

            Assert.That(result, Is.Not.Empty);

            var fromDb = await _context.Lessons
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.LessonId == guid);

            Assert.NotNull(fromDb);
            Assert.AreEqual("Тестовая математика", fromDb.SubjectName);
        }

        [Test]
        public async Task UpsertLessonsAsync_ShouldUpdateLesson_WhenExists()
        {
            var guid = Guid.NewGuid();

            var original = new LessonModel
            {
                LessonId = guid,
                SubjectName = "Старый предмет",
                TeacherName = "Иванов",
                PairNumber = 1,
                MenGroup = 240801
            };

            _context.Lessons.Add(original);
            await _context.SaveChangesAsync();

            var updated = new LessonModel
            {
                LessonId = guid,
                SubjectName = "Новый предмет",
                TeacherName = "Петров",
                PairNumber = 3,
                MenGroup = 240801
            };

            var result = await _repo.UpsertLessonsAsync(new[] { updated });

            var fromDb = await _context.Lessons
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.LessonId == guid);

            Assert.NotNull(fromDb);
            Assert.AreEqual("Новый предмет", fromDb.SubjectName);
            Assert.AreEqual("Петров", fromDb.TeacherName);
        }
    }
}
