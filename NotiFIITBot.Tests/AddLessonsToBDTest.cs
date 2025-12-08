using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Repo;
using NUnit.Framework;

namespace NotiFIITBot.IntegrationTests
{
    [TestFixture]
    public class ScheduleRepositoryIntegrationTests
    {
        private ScheduleDbContext _context = null!;
        private ScheduleRepository _repo = null!;
        private IDbContextTransaction? _tx;

        private const string ConnectionString =
            "Host=localhost;Port=5433;Database=notifiit_db;Username=notifiit_admin;Password=226381194";

        [SetUp]
        public async Task SetUp()
        {
            var options = new DbContextOptionsBuilder<ScheduleDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            _context = new ScheduleDbContext(options);
            _repo = new ScheduleRepository(_context);

            _tx = await _context.Database.BeginTransactionAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            try
            {
                if (_tx != null)
                {
                    await _tx.RollbackAsync();
                    await _tx.DisposeAsync();
                    _tx = null;
                }
            }
            finally
            {
                if (_context != null)
                {
                    await _context.DisposeAsync();
                    _context = null!;
                }
            }
        }

        [Test]
        public async Task UpsertLessonsAsync_ShouldAddAndRetrieveLesson()
        {
            var model = new LessonModel
            {
                MenGroup = 999999,
                SubGroup = 1,
                DayOfWeek = DayOfWeek.Monday,
                PairNumber = 1,
                Evenness = Evenness.Even,
                SubjectName = "Тестовый урок",
                TeacherName = "Ненастоящий преподаватель",
                ClassroomNumber = "777",
            };
            
            var saved = await _repo.UpsertLessonsAsync(new[] { model });
            
            Assert.IsNotNull(saved);
            Assert.IsNotEmpty(saved);

            var returned = saved.First();
            
            Assert.AreEqual("Integration Test Subject", returned.SubjectName);
            
            var fromDb = await _context.Lessons.FindAsync(returned.LessonId);
            Assert.IsNotNull(fromDb, "Запись не найдена в БД в рамках транзакции.");
            Assert.AreEqual("Integration Test Subject", fromDb!.SubjectName);
        }

        [Test]
        public async Task GetScheduleAsync_ShouldReturnLessonsForGroupAndDay()
        {
            var gid = 111111; // menGroup для теста
            var lessonEven = new LessonModel
            {
                LessonId = Guid.NewGuid(),
                MenGroup = gid,
                SubGroup = null,
                DayOfWeek = DayOfWeek.Tuesday,
                PairNumber = 1,
                Evenness = Evenness.Even,
                SubjectName = "IT Test Even"
            };
            var lessonOdd = new LessonModel
            {
                LessonId = Guid.NewGuid(),
                MenGroup = gid,
                SubGroup = null,
                DayOfWeek = DayOfWeek.Tuesday,
                PairNumber = 2,
                Evenness = Evenness.Odd,
                SubjectName = "IT Test Odd"
            };

            await _context.Lessons.AddAsync(lessonEven);
            await _context.Lessons.AddAsync(lessonOdd);
            await _context.SaveChangesAsync();
            
            var results = await _repo.GetScheduleAsync(groupNumber: gid, subGroup: null,
                SchedulePeriod.Week, now: new DateTime(2025, 2, 11));

            Assert.IsTrue(results.Any(r => r.SubjectName == "IT Test Even"));
            Assert.IsTrue(results.Any(r => r.SubjectName == "IT Test Odd"));
        }
    }
}
