using System.Text.Json;
using Academy.Application.Billing;
using Academy.Domain;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Billing;

/// <summary>Subscription use-cases. Checkout/upgrade hand off to the gateway and persist a
/// PENDING payment intent — the actual subscription/entitlement change happens only when the
/// webhook clears (GR-2). Downgrade/cancel are entitlement-preserving state changes.</summary>
public class SubscriptionService(AppDbContext db, IPaymentGateway gateway) : ISubscriptionService
{
    public async Task<IReadOnlyList<PlanDto>> GetPlansAsync(CancellationToken ct = default)
        => await db.Plans.Where(p => p.IsActive).OrderBy(p => p.TierLevel)
            .Select(p => new PlanDto(p.Id, p.Name, p.TierLevel, p.PriceMonthly, p.PriceAnnual, p.IsActive, p.Description))
            .ToListAsync(ct);

    public async Task<MySubscriptionDto?> GetMyAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await CurrentAsync(userId, ct);
        if (sub is null) return null;
        string? plannedName = sub.PlannedPlanId is Guid pid
            ? await db.Plans.Where(p => p.Id == pid).Select(p => p.Name).FirstOrDefaultAsync(ct)
            : null;
        return new MySubscriptionDto(
            sub.Id, sub.PlanId, sub.Plan.Name, sub.Plan.TierLevel, sub.Status, sub.BillingCycle,
            sub.PriceLockedIdr, sub.CurrentPeriodStart, sub.CurrentPeriodEnd, sub.PlannedPlanId, plannedName);
    }

    public async Task<IReadOnlyList<BillingHistoryItemDto>> GetBillingHistoryAsync(Guid userId, CancellationToken ct = default)
        => await db.PaymentTransactions.Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new BillingHistoryItemDto(p.Id, p.AmountIdr, p.Kind, p.Status, p.Method, p.CreatedAt))
            .ToListAsync(ct);

    public async Task<CheckoutSession> CheckoutAsync(Guid userId, Guid planId, BillingCycle cycle, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new BillingException("Pengguna tidak ditemukan.");
        if (!user.EmailVerified) throw new BillingException("Verifikasi email Anda sebelum berlangganan.");

        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new BillingException("Paket tidak ditemukan.");
        if (!plan.IsActive) throw new BillingException("Paket tidak aktif.");
        if (plan.TierLevel <= 0) throw new BillingException("Paket gratis tidak memerlukan pembayaran.");

        if (await CurrentAsync(userId, ct) is not null)
            throw new BillingException("Anda sudah memiliki langganan aktif. Gunakan ubah paket (upgrade/downgrade).");

        var amount = cycle == BillingCycle.Annual ? plan.PriceAnnual : plan.PriceMonthly;
        var session = await gateway.CreateCheckoutAsync(
            new CheckoutIntent(userId, planId, cycle, amount, CheckoutKind.Cycle, null), ct);

        AddPendingPayment(userId, null, amount, PaymentKind.Cycle, session.ProviderRef,
            new CheckoutIntentSnapshot(planId, cycle, CheckoutKind.Cycle, null));
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<UpgradePreviewDto> PreviewUpgradeAsync(Guid userId, Guid newPlanId, CancellationToken ct = default)
    {
        var (_, current, target, delta, remaining, cycleDays) = await ComputeUpgradeAsync(userId, newPlanId, ct);
        return new UpgradePreviewDto(current.Id, current.Name, target.Id, target.Name, delta, remaining, cycleDays);
    }

    public async Task<CheckoutSession> UpgradeAsync(Guid userId, Guid newPlanId, CancellationToken ct = default)
    {
        var (sub, _, target, delta, _, _) = await ComputeUpgradeAsync(userId, newPlanId, ct);
        var session = await gateway.CreateCheckoutAsync(
            new CheckoutIntent(userId, target.Id, sub.BillingCycle, delta, CheckoutKind.ProrationUpgrade, sub.Id), ct);

        AddPendingPayment(userId, sub.Id, delta, PaymentKind.ProrationUpgrade, session.ProviderRef,
            new CheckoutIntentSnapshot(target.Id, sub.BillingCycle, CheckoutKind.ProrationUpgrade, sub.Id));
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task DowngradeAsync(Guid userId, Guid newPlanId, CancellationToken ct = default)
    {
        var sub = await CurrentAsync(userId, ct)
            ?? throw new BillingException("Tidak ada langganan aktif.");
        var target = await db.Plans.FirstOrDefaultAsync(p => p.Id == newPlanId, ct)
            ?? throw new BillingException("Paket tujuan tidak ditemukan.");
        if (target.TierLevel < 1) throw new BillingException("Untuk berhenti, gunakan Batalkan langganan.");
        if (target.TierLevel >= sub.Plan.TierLevel) throw new BillingException("Downgrade harus ke tier yang lebih rendah.");

        sub.PlannedPlanId = newPlanId; // applied at next renewal (TSD §9.4)
        db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            Id = Guid.CreateVersion7(), SubscriptionId = sub.Id,
            FromStatus = sub.Status, ToStatus = sub.Status,
            Reason = $"Downgrade ke {target.Name} dijadwalkan pada perpanjangan berikutnya.",
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid userId, CancellationToken ct = default)
    {
        var sub = await CurrentAsync(userId, ct)
            ?? throw new BillingException("Tidak ada langganan aktif untuk dibatalkan.");
        if (!SubscriptionStateMachine.CanTransition(sub.Status, SubscriptionStatus.Canceled))
            throw new BillingException("Langganan tidak dapat dibatalkan.");

        var from = sub.Status;
        sub.Status = SubscriptionStatus.Canceled;
        sub.PlannedPlanId = null;
        db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            Id = Guid.CreateVersion7(), SubscriptionId = sub.Id,
            FromStatus = from, ToStatus = SubscriptionStatus.Canceled,
            Reason = "Dibatalkan oleh pengguna; akses berlanjut sampai akhir periode.",
        });
        await db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    /// <summary>The user's current entitling subscription (non-expired, still in period).</summary>
    private async Task<Subscription?> CurrentAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.Status != SubscriptionStatus.Expired && s.CurrentPeriodEnd > now)
            .OrderByDescending(s => s.Plan.TierLevel).ThenByDescending(s => s.CurrentPeriodEnd)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<(Subscription sub, Plan current, Plan target, decimal delta, int remaining, int cycleDays)>
        ComputeUpgradeAsync(Guid userId, Guid newPlanId, CancellationToken ct)
    {
        var sub = await CurrentAsync(userId, ct)
            ?? throw new BillingException("Tidak ada langganan aktif untuk di-upgrade.");
        var target = await db.Plans.FirstOrDefaultAsync(p => p.Id == newPlanId, ct)
            ?? throw new BillingException("Paket tujuan tidak ditemukan.");
        if (!target.IsActive) throw new BillingException("Paket tujuan tidak aktif.");
        if (target.TierLevel <= sub.Plan.TierLevel) throw new BillingException("Upgrade harus ke tier yang lebih tinggi.");

        var now = DateTimeOffset.UtcNow;
        var currentCyclePrice = sub.PriceLockedIdr;
        var newCyclePrice = sub.BillingCycle == BillingCycle.Annual ? target.PriceAnnual : target.PriceMonthly;
        var cycleDays = Math.Max(1, (int)Math.Round((sub.CurrentPeriodEnd - sub.CurrentPeriodStart).TotalDays));
        var remaining = Math.Max(0, (int)Math.Ceiling((sub.CurrentPeriodEnd - now).TotalDays));
        var delta = Proration.UpgradeDelta(currentCyclePrice, newCyclePrice, remaining, cycleDays);
        return (sub, sub.Plan, target, delta, remaining, cycleDays);
    }

    private void AddPendingPayment(Guid userId, Guid? subscriptionId, decimal amount, PaymentKind kind,
        string providerRef, CheckoutIntentSnapshot intent)
    {
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SubscriptionId = subscriptionId,
            AmountIdr = amount,
            Kind = kind,
            Status = PaymentStatus.Pending,
            ProviderRef = providerRef,
            XenditIds = JsonSerializer.Serialize(new { provider = gateway.Source, providerRef }),
            RawPayload = JsonSerializer.Serialize(intent),
        });
    }
}
