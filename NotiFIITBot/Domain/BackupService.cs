using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models; 
using Quartz;
using Serilog;

namespace NotiFIITBot.Domain;

//запрещает одновременный запуск двух бэкапов
[DisallowConcurrentExecution] 
public class BackupService : IJob
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger baseLogger;   
    private readonly ILogger backupLogger; 
    
    private static readonly string BackupFolder = "backups";
    private const int MaxBackupsToKeep = 4;

    public BackupService(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        this.scopeFactory = scopeFactory;
        baseLogger = logger;
        backupLogger = logger.ForContext("SourceContext", "BACKUP");
    }

    public async Task Execute(IJobExecutionContext context)
    {
        backupLogger.Information("Starting scheduled backup...");
        try
        {
            await CreateBackupAsync();
        }
        catch (Exception ex)
        {
            backupLogger.Error(ex, "Error executing backup job");
        }
    }

    public async Task CreateBackupAsync()
    {
        if (!Directory.Exists(BackupFolder))
            Directory.CreateDirectory(BackupFolder);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

            var backupData = new FullBackupData
            {
                Timestamp = DateTime.Now,
                Users = await context.Users.AsNoTracking().ToListAsync(),
                Configs = await context.UserNotificationConfigs.AsNoTracking().ToListAsync()
            };

            var fileName = $"full_backup_{timestamp}.json";
            await SaveToFileAsync(backupData, fileName);

            backupLogger.Information($"Backup created successfully: {fileName}");
            
            CleanUpOldBackups();
        }
        catch (Exception ex)
        {
            backupLogger.Error(ex, "Failed to create backup");
            throw; 
        }
    }
    
    /// Удаляет старые бэкапы, оставляет только последние N (решили, что 4) штук.
    private void CleanUpOldBackups()
    {
        try
        {
            var directoryInfo = new DirectoryInfo(BackupFolder);
            
            var files = directoryInfo.GetFiles("full_backup_*.json")
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (files.Count <= MaxBackupsToKeep)
                return;

            var filesToDelete = files.Skip(MaxBackupsToKeep);

            foreach (var file in filesToDelete)
            {
                file.Delete();
                backupLogger.Information($"Deleted old backup: {file.Name}");
            }
        }
        catch (Exception ex)
        {
            backupLogger.Warning(ex, "Failed to clean up old backups");
        }
    }

    public async Task RestoreBackupAsync(string fileName)
    {
        var restoreLog = baseLogger.ForContext("SourceContext", "RESTORE");

        if (string.IsNullOrEmpty(fileName))
        {
            restoreLog.Warning("Filename is empty.");
            return;
        }

        var filePath = Path.Combine(BackupFolder, fileName);
        if (!File.Exists(filePath))
        {
            restoreLog.Warning($"File not found: {filePath}");
            return;
        }

        restoreLog.Information($"Starting restoration from {fileName}...");

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<FullBackupData>(json);

            if (data == null)
            {
                restoreLog.Error("Failed to deserialize backup file.");
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ScheduleDbContext>();

            restoreLog.Information("Clearing database tables...");
            context.UserNotificationConfigs.RemoveRange(context.UserNotificationConfigs);
            await context.SaveChangesAsync();

            context.Users.RemoveRange(context.Users);
            await context.SaveChangesAsync();


            restoreLog.Information($"Restoring {data.Users.Count} users and {data.Configs.Count} configs...");
            if (data.Users.Any())
            {
                await context.Users.AddRangeAsync(data.Users);
                await context.SaveChangesAsync();
            }

            if (data.Configs.Any())
            {
                await context.UserNotificationConfigs.AddRangeAsync(data.Configs);
                await context.SaveChangesAsync();
            }

            restoreLog.Information("Restoration finished successfully.");
        }
        catch (Exception ex)
        {
            restoreLog.Error(ex, "Critical error during restoration. Database might be in inconsistent state.");
            throw;
        }
    }

    private static async Task SaveToFileAsync<T>(T data, string fileName)
    {
        var filePath = Path.Combine(BackupFolder, fileName);
        
        var jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
        };

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, data, jsonOptions);
    }
}

// объединяет бэкапы юзеров и уведомлений в одну джейсонину
public class FullBackupData
{
    public DateTime Timestamp { get; set; }
    public List<User> Users { get; set; } = [];
    public List<UserNotificationConfig> Configs { get; set; } = [];
}