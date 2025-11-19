using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Consts;
using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Database.Data
{
    public class ScheduleDbContext : DbContext
    {
        public ScheduleDbContext(DbContextOptions<ScheduleDbContext> options)
            : base(options)
        {
        }
        //  Наборы всех таблиц
        public DbSet<User> Users { get; set; }
        public DbSet<LessonModel> Lessons { get; set; }
        public DbSet<UserNotificationConfig> UserNotificationConfigs { get; set; }
        public DbSet<WeekParityConfig> WeekParityConfigs { get; set; }

        // Настройка моделей 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

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