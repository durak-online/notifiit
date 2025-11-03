using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NotiFIITBot.Models;

namespace NotiFIITBot;

public class ApplicationDbContext : DbContext
{
    public DbSet<Group> Groups { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<Subjects.Subject> Subjects { get; set; }
    public DbSet<Classroom> Classrooms { get; set; }
    public DbSet<TimeSlot> TimeSlots { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<ApplicationDbContext>()
            .Build();

        var dbPassword = config["DbPassword"];
        var connectionString = $"Host=localhost;Database=schedule_bot_db;Username=postgres;Password={dbPassword}";

        optionsBuilder.UseNpgsql(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<WeekPority>();
        
        /* я хз оставить это или нет, это короче время пар, ну оно же никогда не меняется, можно и так закинуть
        modelBuilder.Entity<TimeSlot>().HasData(
            new TimeSlot { Id = 1, PairNumber = 1, StartTime = new TimeOnly(9, 0, 0), EndTime = new TimeOnly(10, 30, 0) },
            new TimeSlot { Id = 2, PairNumber = 2, StartTime = new TimeOnly(10, 40, 0), EndTime = new TimeOnly(12, 10, 0) },
            new TimeSlot { Id = 3, PairNumber = 3, StartTime = new TimeOnly(12, 50, 0), EndTime = new TimeOnly(14, 20, 0) },
            new TimeSlot { Id = 4, PairNumber = 4, StartTime = new TimeOnly(14, 30, 0), EndTime = new TimeOnly(16, 0, 0) },
            new TimeSlot { Id = 5, PairNumber = 5, StartTime = new TimeOnly(16, 10, 0), EndTime = new TimeOnly(17, 40, 0) },
            new TimeSlot { Id = 6, PairNumber = 6, StartTime = new TimeOnly(17, 50, 0), EndTime = new TimeOnly(19, 20, 0) },
            new TimeSlot { Id = 7, PairNumber = 7, StartTime = new TimeOnly(19, 30, 0), EndTime = new TimeOnly(21, 0, 0) }
        );
        */
    }
}