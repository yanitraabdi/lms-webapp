// Plans, subscriptions, payments, webhook idempotency (TSD §6.3)
using Academy.Domain.Common;
using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public class Plan : Entity
{
    public string Name { get; set; } = default!;
    public int TierLevel { get; set; }                     // 0..3
    public decimal PriceMonthly { get; set; }              // IDR (decimal(18,2), whole rupiah)
    public decimal PriceAnnual { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string IncludedContentMapping { get; set; } = "{}"; // jsonb — tier→content rule
}

public class Subscription : Entity
{
    public Guid UserId { get; set; }
    public Guid? OrgId { get; set; }                       // null for B2C; per-seat later
    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = default!;

    // Scheduled downgrade target: applied at the next renewal, then cleared (GR-8 / TSD §9.4).
    public Guid? PlannedPlanId { get; set; }

    // Grandfathering: bill this snapshot, NOT live Plan.Price (GR-8).
    public decimal PriceLockedIdr { get; set; }
    public BillingCycle BillingCycle { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTimeOffset CurrentPeriodStart { get; set; }
    public DateTimeOffset CurrentPeriodEnd { get; set; }
    public string? XenditPlanRef { get; set; }

    public ICollection<SubscriptionEvent> Events { get; set; } = new List<SubscriptionEvent>();
}

public class SubscriptionEvent : Entity
{
    public Guid SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = default!;
    public SubscriptionStatus? FromStatus { get; set; }
    public SubscriptionStatus ToStatus { get; set; }
    public string? Reason { get; set; }
}

public class PaymentTransaction : Entity
{
    public Guid UserId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public decimal AmountIdr { get; set; }
    public PaymentKind Kind { get; set; }                  // cycle | proration_upgrade
    public string? Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ProviderRef { get; set; }               // gateway checkout/charge ref — webhook lookup key
    public string XenditIds { get; set; } = "{}";          // jsonb
    public string RawPayload { get; set; } = "{}";         // jsonb — checkout intent / event snapshot
}

/// <summary>Idempotency ledger for ALL inbound webhooks (Xendit + Bunny). GR-2.</summary>
public class WebhookEvent : Entity
{
    public string Source { get; set; } = default!;         // xendit | bunny
    public string ExternalId { get; set; } = default!;     // UNIQUE — dedupe key
    public bool SignatureVerified { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string Payload { get; set; } = "{}";            // jsonb
}
