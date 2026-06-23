using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Billing;
using Academy.Application.Engagement;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Academy.Infrastructure.Billing;

/// <summary>
/// The ONLY place entitlement changes (GR-2). Verifies the signature, dedupes on
/// webhook_events.external_id, then applies the payment outcome to the subscription
/// state machine. Safe to call repeatedly — duplicates are no-ops.
/// </summary>
public class PaymentWebhookProcessor(
    AppDbContext db,
    IPaymentGateway gateway,
    IEmailSender email,
    INotificationSender notifier,
    BillingOptions options,
    ILogger<PaymentWebhookProcessor> logger) : IPaymentWebhookProcessor
{
    public async Task<WebhookResult> ProcessAsync(string rawBody, string? callbackToken, CancellationToken ct = default)
    {
        if (!gateway.VerifyWebhookSignature(rawBody, callbackToken))
            return new WebhookResult(WebhookOutcome.InvalidSignature);

        if (!gateway.TryParseWebhook(rawBody, out var evt))
            return new WebhookResult(WebhookOutcome.Ignored);

        // Idempotency ledger — external_id is UNIQUE. Already seen → no-op.
        if (await db.WebhookEvents.AnyAsync(w => w.ExternalId == evt.ExternalId, ct))
            return new WebhookResult(WebhookOutcome.Duplicate);

        var record = new WebhookEvent
        {
            Id = Guid.CreateVersion7(),
            Source = gateway.Source,
            ExternalId = evt.ExternalId,
            SignatureVerified = true,
            Payload = rawBody,
        };
        db.WebhookEvents.Add(record);
        try
        {
            await db.SaveChangesAsync(ct); // unique index closes the concurrent-delivery race
        }
        catch (DbUpdateException)
        {
            return new WebhookResult(WebhookOutcome.Duplicate);
        }

        await ApplyAsync(evt, ct);

        record.ProcessedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return new WebhookResult(WebhookOutcome.Processed);
    }

    private async Task ApplyAsync(PaymentWebhook evt, CancellationToken ct)
    {
        var tx = await db.PaymentTransactions
            .FirstOrDefaultAsync(p => p.ProviderRef == evt.ProviderRef && p.Status == PaymentStatus.Pending, ct);
        if (tx is null)
        {
            logger.LogWarning("Webhook {Ref} has no matching pending payment intent; ignoring.", evt.ProviderRef);
            return;
        }

        var intent = JsonSerializer.Deserialize<CheckoutIntentSnapshot>(tx.RawPayload)!;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == tx.UserId, ct);
        var plan = await db.Plans.FirstAsync(p => p.Id == intent.PlanId, ct);

        tx.Method = evt.Method;
        tx.XenditIds = MergeEventId(tx.XenditIds, evt.ExternalId);

        if (evt.Type == PaymentWebhookType.Failed)
        {
            tx.Status = PaymentStatus.Failed;
            if (user is not null) await email.SendPaymentFailedAsync(user.Email, user.Name, plan.Name, ct);
            await notifier.DispatchAsync(tx.UserId, NotificationCategories.Billing, "payment_failed",
                "Pembayaran gagal", $"Pembayaran untuk {plan.Name} gagal. Silakan coba lagi.", ct);
            return;
        }

        tx.Status = PaymentStatus.Paid;
        var now = DateTimeOffset.UtcNow;

        if (intent.Kind == CheckoutKind.Cycle)
        {
            var days = intent.Cycle == BillingCycle.Annual ? options.AnnualDays : options.MonthlyDays;
            var sub = new Subscription
            {
                Id = Guid.CreateVersion7(),
                UserId = tx.UserId,
                PlanId = plan.Id,
                PriceLockedIdr = tx.AmountIdr,           // grandfathered at signup price (GR-8)
                BillingCycle = intent.Cycle,
                Status = SubscriptionStatus.Active,
                CurrentPeriodStart = now,
                CurrentPeriodEnd = now.AddDays(days),
            };
            db.Subscriptions.Add(sub);
            tx.SubscriptionId = sub.Id;
            db.SubscriptionEvents.Add(new SubscriptionEvent
            {
                Id = Guid.CreateVersion7(), SubscriptionId = sub.Id,
                FromStatus = null, ToStatus = SubscriptionStatus.Active,
                Reason = "Pembayaran checkout berhasil — langganan aktif.",
            });
            if (user is not null)
                await email.SendSubscriptionConfirmationAsync(user.Email, user.Name, plan.Name, sub.PriceLockedIdr, sub.CurrentPeriodEnd, ct);
            await notifier.DispatchAsync(tx.UserId, NotificationCategories.Billing, "subscription_activated",
                "Langganan aktif", $"Langganan {plan.Name} Anda telah aktif. Selamat belajar!", ct);
        }
        else // ProrationUpgrade — immediate plan change, period unchanged
        {
            var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.Id == intent.SubscriptionId, ct);
            if (sub is null)
            {
                logger.LogWarning("Upgrade webhook for missing subscription {Id}.", intent.SubscriptionId);
                return;
            }
            sub.PlanId = plan.Id;
            sub.PriceLockedIdr = intent.Cycle == BillingCycle.Annual ? plan.PriceAnnual : plan.PriceMonthly;
            db.SubscriptionEvents.Add(new SubscriptionEvent
            {
                Id = Guid.CreateVersion7(), SubscriptionId = sub.Id,
                FromStatus = sub.Status, ToStatus = sub.Status,
                Reason = $"Upgrade ke {plan.Name} (prorata) berhasil.",
            });
            if (user is not null)
                await email.SendSubscriptionConfirmationAsync(user.Email, user.Name, plan.Name, tx.AmountIdr, sub.CurrentPeriodEnd, ct);
        }
    }

    private static string MergeEventId(string xenditIds, string externalId)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(xenditIds) ?? [];
            var merged = new Dictionary<string, object?>();
            foreach (var (k, v) in dict) merged[k] = v.ToString();
            merged["event"] = externalId;
            return JsonSerializer.Serialize(merged);
        }
        catch
        {
            return JsonSerializer.Serialize(new { @event = externalId });
        }
    }
}
