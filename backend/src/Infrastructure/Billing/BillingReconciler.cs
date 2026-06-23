using Academy.Application.Abstractions;
using Academy.Application.Billing;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Billing;

/// <summary>
/// Time-based billing reconciliation (TSD §9.3). Advances subscriptions whose period
/// has elapsed: a scheduled downgrade is applied (renew into the cheaper plan, re-locking
/// price to the now-current value — grandfather rollover); a canceled or un-renewed
/// subscription expires (drops the user to Free; watch_progress/certificates are retained,
/// GR-7). With real Xendit this also replays webhooks missed during origin downtime.
/// </summary>
public class BillingReconciler(
    AppDbContext db,
    IEmailSender email,
    BillingOptions options,
    ILogger<BillingReconciler> logger) : IBillingReconciler
{
    public async Task<int> ReconcileAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.Status != SubscriptionStatus.Expired && s.CurrentPeriodEnd <= now)
            .ToListAsync(ct);

        var changed = 0;
        foreach (var sub in due)
        {
            if (sub.Status == SubscriptionStatus.Canceled)
            {
                Transition(sub, SubscriptionStatus.Expired, "Periode berakhir setelah pembatalan.");
                changed++;
            }
            else if (sub.PlannedPlanId is Guid plannedId
                     && await db.Plans.FirstOrDefaultAsync(p => p.Id == plannedId, ct) is { } target)
            {
                var fromName = sub.Plan.Name;
                var days = sub.BillingCycle == BillingCycle.Annual ? options.AnnualDays : options.MonthlyDays;
                sub.PlanId = target.Id;
                sub.PlannedPlanId = null;
                sub.PriceLockedIdr = sub.BillingCycle == BillingCycle.Annual ? target.PriceAnnual : target.PriceMonthly;
                sub.CurrentPeriodStart = now;
                sub.CurrentPeriodEnd = now.AddDays(days);
                sub.Status = SubscriptionStatus.Active;
                db.SubscriptionEvents.Add(new SubscriptionEvent
                {
                    Id = Guid.CreateVersion7(), SubscriptionId = sub.Id,
                    FromStatus = SubscriptionStatus.Active, ToStatus = SubscriptionStatus.Active,
                    Reason = $"Downgrade dari {fromName} ke {target.Name} berlaku pada perpanjangan.",
                });
                changed++;
            }
            else
            {
                // No stored payment method in dev → the subscription lapses to Free.
                Transition(sub, SubscriptionStatus.Expired, "Periode berakhir tanpa perpanjangan.");
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == sub.UserId, ct);
                if (user is not null)
                    await email.SendSubscriptionExpiredAsync(user.Email, user.Name, sub.Plan.Name, ct);
                changed++;
            }
        }

        if (changed > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Billing reconcile advanced {Count} subscription(s).", changed);
        }
        return changed;
    }

    private void Transition(Subscription sub, SubscriptionStatus to, string reason)
    {
        var from = sub.Status;
        sub.Status = to;
        db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            Id = Guid.CreateVersion7(), SubscriptionId = sub.Id,
            FromStatus = from, ToStatus = to, Reason = reason,
        });
    }
}
