using Academy.Domain.Enums;

namespace Academy.Application.Billing;

// ---- payment gateway contract (provider-agnostic; Xendit / dev-sim behind it) ----

public enum CheckoutKind { Cycle, ProrationUpgrade, ProgramPurchase }

/// <summary>
/// A pending charge the gateway turns into a hosted-checkout session.
/// Subscription charges (dormant) carry PlanId/Cycle; an INVERTA one-time program purchase
/// carries ProgramId instead — hence both are nullable.
/// </summary>
public record CheckoutIntent(
    Guid UserId,
    Guid? PlanId,
    BillingCycle? Cycle,
    decimal AmountIdr,
    CheckoutKind Kind,
    Guid? SubscriptionId,
    Guid? ProgramId = null);

public record CheckoutSession(string CheckoutUrl, string ProviderRef);

public enum PaymentWebhookType { Succeeded, Failed }

/// <summary>Normalized inbound payment event parsed from a provider webhook body.</summary>
public record PaymentWebhook(
    string ExternalId,        // provider event id — the idempotency key
    string ProviderRef,       // ties back to the pending payment_transactions row
    PaymentWebhookType Type,
    decimal? AmountIdr,
    string? Method);

public enum WebhookOutcome { Processed, Duplicate, InvalidSignature, Ignored }

public record WebhookResult(WebhookOutcome Outcome);

// ---- API DTOs ----

public record PlanDto(
    Guid Id,
    string Name,
    int TierLevel,
    decimal PriceMonthly,
    decimal PriceAnnual,
    bool IsActive,
    string? Description);

public record MySubscriptionDto(
    Guid Id,
    Guid PlanId,
    string PlanName,
    int TierLevel,
    SubscriptionStatus Status,
    BillingCycle BillingCycle,
    decimal PriceLockedIdr,
    DateTimeOffset CurrentPeriodStart,
    DateTimeOffset CurrentPeriodEnd,
    Guid? PlannedPlanId,
    string? PlannedPlanName);

public record UpgradePreviewDto(
    Guid CurrentPlanId,
    string CurrentPlanName,
    Guid NewPlanId,
    string NewPlanName,
    decimal DeltaIdr,
    int RemainingDays,
    int CycleDays);

public record BillingHistoryItemDto(
    Guid Id,
    decimal AmountIdr,
    PaymentKind Kind,
    PaymentStatus Status,
    string? Method,
    DateTimeOffset CreatedAt);

public record CheckoutRequest(Guid PlanId, BillingCycle BillingCycle);
public record PlanChangeRequest(Guid NewPlanId);
