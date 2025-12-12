using Quartz;
using Quartz.Impl;
using Serilog;

namespace NotiFIITBot.Metrics;

public class MetricsReportScheduler : IDisposable
{
    private IScheduler? _scheduler;
    
    public async Task StartAsync()
    {
        try
        {
            Log.Information("Starting metrics report scheduler...");
            
            var schedulerFactory = new StdSchedulerFactory();
            _scheduler = await schedulerFactory.GetScheduler();
            await _scheduler.Start();
            
            var weeklyJob = JobBuilder.Create<WeeklyMetricsJob>()
                .WithIdentity("weekly-metrics-job")
                .Build();
            
            var weeklyTrigger = TriggerBuilder.Create()
                .WithIdentity("weekly-metrics-trigger")
                .WithSchedule(CronScheduleBuilder.WeeklyOnDayAndHourAndMinute(DayOfWeek.Monday, 0, 5))
                .StartNow()
                .Build();
            
            await _scheduler.ScheduleJob(weeklyJob, weeklyTrigger);
            
            Log.Information($"Weekly report job scheduled. Next run: {weeklyTrigger.GetNextFireTimeUtc()?.LocalDateTime}");
            
            await TriggerInitialReportsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start metrics report scheduler");
            throw;
        }
    }
    
    private async Task TriggerInitialReportsAsync()
    {
        try
        {
            Log.Information("Scheduling initial reports generation...");
            
            var initialJob = JobBuilder.Create<InitialMetricsJob>()
                .WithIdentity("initial-metrics-job")
                .Build();
            
            var initialTrigger = TriggerBuilder.Create()
                .WithIdentity("initial-metrics-trigger")
                .StartNow()
                .Build();
            
            await _scheduler?.ScheduleJob(initialJob, initialTrigger);
            
            Log.Information("Initial reports job scheduled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to schedule initial reports job");
        }
    }
    
    public async Task StopAsync()
    {
        try
        {
            if (_scheduler != null)
            {
                await _scheduler.Shutdown(true);
                Log.Information("Metrics report scheduler stopped");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping metrics report scheduler");
        }
    }
    
    public void Dispose()
    {
        StopAsync().Wait();
    }
}

public class WeeklyMetricsJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        try
        {
            Log.Information("Starting weekly metrics report generation...");
            var reporter = new MetricsReporter();
            reporter.GenerateWeeklyReport();
            Log.Information("Weekly metrics report generated successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing weekly metrics report job");
        }
        
        return Task.CompletedTask;
    }
}

public class InitialMetricsJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        try
        {
            Log.Information("Starting initial generation of all metrics reports...");
            var reporter = new MetricsReporter();
            reporter.GenerateAllReports();
            Log.Information("All metrics reports generated successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing initial metrics reports job");
        }
        
        return Task.CompletedTask;
    }
}