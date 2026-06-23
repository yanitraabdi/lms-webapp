using Academy.Domain.Enums;

namespace Academy.Application.Billing;

/// <summary>Payment gateway port. Implemented by DevPaymentGateway (sim) now and
/// XenditGateway when credentials are supplied — selected by config.</summary>
public interface IPaymentGateway
{
    /// <summary>Recorded on webhook_events.source (e.g. "xendit", "dev").</summary>
    string Source { get; }

    /// <summary>Turn a pending charge into a hosted-checkout session the user is sent to.</summary>
    Task<CheckoutSession> CreateCheckoutAsync(CheckoutIntent intent, CancellationToken ct = default);

    /// <summary>Verify an inbound webhook against the configured callback token / secret.</summary>
    bool VerifyWebhookSignature(string rawBody, string? callbackToken);

    /// <summary>Parse a (verified) webhook body into the normalized event.</summary>
    bool TryParseWebhook(string rawBody, out PaymentWebhook webhook);
}

/// <summary>Subscription lifecycle use-cases (checkout, upgrade/downgrade, cancel, reads).
/// Entitlement is NEVER granted here — only by the webhook processor (GR-2).</summary>
public interface ISubscriptionService
{
    Task<IReadOnlyList<PlanDto>> GetPlansAsync(CancellationToken ct = default);
    Task<MySubscriptionDto?> GetMyAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<BillingHistoryItemDto>> GetBillingHistoryAsync(Guid userId, CancellationToken ct = default);

    Task<CheckoutSession> CheckoutAsync(Guid userId, Guid planId, BillingCycle cycle, CancellationToken ct = default);

    Task<UpgradePreviewDto> PreviewUpgradeAsync(Guid userId, Guid newPlanId, CancellationToken ct = default);
    Task<CheckoutSession> UpgradeAsync(Guid userId, Guid newPlanId, CancellationToken ct = default);

    Task DowngradeAsync(Guid userId, Guid newPlanId, CancellationToken ct = default);
    Task CancelAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Processes inbound payment webhooks idempotently and drives the subscription
/// state machine + entitlement grant. The ONLY place entitlement changes (GR-2).</summary>
public interface IPaymentWebhookProcessor
{
    Task<WebhookResult> ProcessAsync(string rawBody, string? callbackToken, CancellationToken ct = default);
}

/// <summary>Time-based + missed-webhook reconciliation (TSD §9.3): advances overdue
/// subscriptions (grace→expired, canceled→expired) and, with real Xendit, replays
/// missed events. Run periodically by a background service.</summary>
public interface IBillingReconciler
{
    Task<int> ReconcileAsync(CancellationToken ct = default);
}

public class BillingException(string message) : Exception(message);
