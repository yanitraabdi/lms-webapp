using System.Text.Json;
using Academy.Application.Admin;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Admin;

public class UserAdminService(AppDbContext db) : IUserAdminService
{
    public async Task<AdminUserListDto> ListAsync(string? search, string? status, int? tier, int skip, int take, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var q = db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var p = $"%{search.Trim()}%";
            q = q.Where(u => EF.Functions.ILike(u.Email, p) || EF.Functions.ILike(u.Name, p));
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UserStatus>(status, true, out var st))
            q = q.Where(u => u.Status == st);
        if (tier is int t)
            q = q.Where(u => db.Subscriptions.Any(s => s.UserId == u.Id
                && s.Status != SubscriptionStatus.Expired && s.CurrentPeriodEnd > now && s.Plan.TierLevel == t));

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(u => u.CreatedAt)
            .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100))
            .Select(u => new
            {
                u.Id, u.Email, u.Name, u.Role, u.Status, u.CreatedAt,
                Tier = db.Subscriptions
                    .Where(s => s.UserId == u.Id && s.Status != SubscriptionStatus.Expired && s.CurrentPeriodEnd > now)
                    .Select(s => (int?)s.Plan.TierLevel).Max(),
            })
            .ToListAsync(ct);

        var users = rows
            .Select(r => new AdminUserListItemDto(r.Id, r.Email, r.Name, r.Role.ToString(), r.Status.ToString(), r.Tier, r.CreatedAt))
            .ToList();
        return new AdminUserListDto(users, total);
    }

    public async Task<AdminUserDetailDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct) ?? throw NotFound();
        var now = DateTimeOffset.UtcNow;

        var sub = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.Status != SubscriptionStatus.Expired && s.CurrentPeriodEnd > now)
            .OrderByDescending(s => s.Plan.TierLevel).FirstOrDefaultAsync(ct);

        var completed = await db.WatchProgress.CountAsync(w => w.UserId == userId && w.Completed, ct);
        var certs = await db.Certificates.CountAsync(c => c.UserId == userId, ct);
        var payments = await db.PaymentTransactions.Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt).Take(5)
            .Select(p => new { p.AmountIdr, p.Kind, p.Status, p.CreatedAt }).ToListAsync(ct);

        return new AdminUserDetailDto(
            u.Id, u.Email, u.Name, u.Role.ToString(), u.Status.ToString(), u.EmailVerified, u.CreatedAt,
            sub?.Plan.TierLevel, sub?.Plan.Name, sub?.Status.ToString(), sub?.CurrentPeriodEnd,
            completed, certs,
            payments.Select(p => new AdminPaymentDto(p.AmountIdr, p.Kind.ToString(), p.Status.ToString(), p.CreatedAt)).ToList());
    }

    public async Task SetStatusAsync(Guid actor, Guid userId, UserStatus status, CancellationToken ct = default)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct) ?? throw NotFound();
        u.Status = status;
        Audit(actor, status == UserStatus.Suspended ? "user_suspended" : "user_reactivated", userId, new { u.Email });
        await db.SaveChangesAsync(ct);
    }

    public async Task SetRoleAsync(Guid actor, Guid userId, UserRole role, CancellationToken ct = default)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct) ?? throw NotFound();
        var from = u.Role;
        u.Role = role;
        Audit(actor, "user_role_changed", userId, new { u.Email, from = from.ToString(), to = role.ToString() });
        await db.SaveChangesAsync(ct);
    }

    public async Task GrantPlanAsync(Guid actor, Guid userId, Guid planId, int days, CancellationToken ct = default)
    {
        _ = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct) ?? throw NotFound();
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct)
            ?? throw new AdminException("Paket tidak ditemukan.", 404);

        var now = DateTimeOffset.UtcNow;
        var sub = new Subscription
        {
            Id = Guid.CreateVersion7(), UserId = userId, PlanId = planId,
            PriceLockedIdr = 0m, BillingCycle = BillingCycle.Monthly, Status = SubscriptionStatus.Active,
            CurrentPeriodStart = now, CurrentPeriodEnd = now.AddDays(Math.Clamp(days, 1, 3650)),
        };
        db.Subscriptions.Add(sub);
        db.SubscriptionEvents.Add(new SubscriptionEvent
        {
            Id = Guid.CreateVersion7(), SubscriptionId = sub.Id, FromStatus = null,
            ToStatus = SubscriptionStatus.Active, Reason = "Diberikan manual oleh admin (komplimen).",
        });
        Audit(actor, "plan_granted", userId, new { plan.Name, days });
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(Guid actor, Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var active = await db.Subscriptions
            .Where(s => s.UserId == userId && s.Status != SubscriptionStatus.Expired && s.CurrentPeriodEnd > now)
            .ToListAsync(ct);
        foreach (var s in active)
        {
            var from = s.Status;
            s.Status = SubscriptionStatus.Expired;
            s.CurrentPeriodEnd = now;
            db.SubscriptionEvents.Add(new SubscriptionEvent
            {
                Id = Guid.CreateVersion7(), SubscriptionId = s.Id, FromStatus = from,
                ToStatus = SubscriptionStatus.Expired, Reason = "Dicabut manual oleh admin.",
            });
        }
        Audit(actor, "plan_revoked", userId, new { count = active.Count });
        await db.SaveChangesAsync(ct);
    }

    private void Audit(Guid actor, string action, Guid target, object metadata) =>
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(), ActorUserId = actor, Action = action,
            Target = target.ToString(), Metadata = JsonSerializer.Serialize(metadata),
        });

    private static AdminException NotFound() => new("Pengguna tidak ditemukan.", 404);
}
