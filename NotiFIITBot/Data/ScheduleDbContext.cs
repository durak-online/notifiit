using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Models; 
using Microsoft.Extensions.Configuration; 
using NotiFIITBot;

namespace NotiFIITBot.Data 
{
    public class ScheduleDbContext : DbContext
    {
        //  Наборы всех таблиц
        public DbSet<User> Users { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<UserNotificationConfig> UserNotificationConfigs { get; set; }
        public DbSet<WeekParityConfig> WeekParityConfigs { get; set; }

        //  Конфигурация подключения 
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<ScheduleDbContext>()
                .Build();

            var dbPassword = configuration["DbPassword"];
            var connectionString = $"Host=localhost;Port=5433;Database=notifiit_db;Username=notifiit_admin;Password={dbPassword}";


            optionsBuilder.UseNpgsql(connectionString);
        }

        // Настройка моделей 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresEnum<Evenness>();
            
            modelBuilder.Entity<WeekParityConfig>()
                .HasKey(e => e.Parity);
            
            // Так как у NotificationConfig.cs нет [Key], мы обязаны определить
            // "составной" ключ здесь:
            modelBuilder.Entity<UserNotificationConfig>()
                .HasKey(e => new { e.TelegramId, e.LessonId });
            

            // Связь "Config -> User"
            // (У одного Config есть один User)
            modelBuilder.Entity<UserNotificationConfig>()
                .HasOne(c => c.User) 
                .WithMany() // (У User много Configs, но без списка)
                .HasForeignKey(c => c.TelegramId); // (Связь по этому ключу)
                
            // Связь "Config -> Lesson"
            // (У одного Config есть один Lesson)
            modelBuilder.Entity<UserNotificationConfig>()
                .HasOne(c => c.Lesson) 
                .WithMany() // (У Lesson много Configs, но без списка)
                .HasForeignKey(c => c.LessonId); // (Связь по этому ключу)
        }
    }
}