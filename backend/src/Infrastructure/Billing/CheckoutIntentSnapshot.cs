using Academy.Application.Billing;
using Academy.Domain.Enums;

namespace Academy.Infrastructure.Billing;

/// <summary>Checkout intent persisted on the pending payment_transactions row so the
/// webhook can resolve what to grant once payment clears (entitlement only via webhook).
/// Subscription charges (dormant) carry PlanId/Cycle; an INVERTA program purchase carries
/// ProgramId + EnrollmentId.</summary>
internal record CheckoutIntentSnapshot(
    Guid? PlanId,
    BillingCycle Cycle,
    CheckoutKind Kind,
    Guid? SubscriptionId,
    Guid? ProgramId = null,
    Guid? EnrollmentId = null);
