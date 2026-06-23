using Academy.Application.Billing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Jobs;

/// <summary>Runs billing reconciliation hourly (expire lapsed subs, apply scheduled
/// downgrades, replay missed webhooks). <see cref="IBillingReconciler"/> holds the logic.</summary>
public class BillingReconcileService(IServiceProvider services, ILogger<BillingReconcileService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = services.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<IBillingReconciler>();
                await reconciler.ReconcileAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Billing reconcile sweep failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
