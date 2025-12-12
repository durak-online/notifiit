using Quartz;
using Serilog;

namespace NotiFIITBot.Domain;

[DisallowConcurrentExecution]
public class ScheduledBackupJob : IJob
{
    private readonly BackupService backupService;
    private readonly ILogger logger;

    public ScheduledBackupJob(BackupService backupService, ILogger logger)
    {
        this.backupService = backupService;
        this.logger = logger.ForContext("SourceContext", "BACKUP");
    }

    public async Task Execute(IJobExecutionContext context)
    {
        logger.Information("Starting scheduled backup...");
        try
        {
            await backupService.CreateBackupAsync();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error executing backup job");
        }
    }
}