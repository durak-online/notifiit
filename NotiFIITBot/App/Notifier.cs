using System.Linq;
using NotiFIITBot.Consts;
using Quartz;

namespace NotiFIITBot.App;
public class Notifier
{
    private readonly IScheduler scheduler;
    private readonly Bot botClient;
    private readonly CancellationTokenSource cts;

    public Notifier(Bot botClient, CancellationTokenSource cts)
    {
        this.botClient = botClient;
        this.cts = cts;
        scheduler = SchedulerBuilder
            .Create()
            .UseDefaultThreadPool(maxConcurrency: 5)
            .BuildScheduler()
            .Result;
    }

    public async Task Start()
    {
        await scheduler.Start(cts.Token);

        var jobData = new JobDataMap();
        jobData.Put("botClient", botClient);

        var job = JobBuilder.Create<NotificationJob>()
            .SetJobData(jobData)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity("daily-trigger")
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(8, 00))
            .StartNow()
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }
}

public class NotificationJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var botClient = (Bot)context.MergedJobDataMap["botClient"];

        var userIds = await GetUserIds();
        var message = "Тест уведомлений:\nМатан 9:00 - 10:30\nАуд. 513";

        await botClient.SendNotifitation(message, userIds);
    }

    private Task<long[]> GetUserIds()
    {
        return Task.FromResult(AdminsConfig.AdminIds.ToArray());
    }
}