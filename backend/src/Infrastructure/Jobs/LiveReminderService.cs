using Academy.Application.Programs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Jobs;

/// <summary>Hourly sweep that sends H-1 live-session reminders.
/// <see cref="ILiveSessionReminder"/> holds the logic and its own idempotency.</summary>
public class LiveReminderService(IServiceProvider services, ILogger<LiveReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var reminder = scope.ServiceProvider.GetRequiredService<ILiveSessionReminder>();
                await reminder.SendDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Live-session reminder sweep failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
