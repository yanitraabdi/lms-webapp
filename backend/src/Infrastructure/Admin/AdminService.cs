using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Admin;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Admin;

public class AdminService(AppDbContext db, IContentRevalidator revalidator) : IAdminService
{
    public async Task<IReadOnlyList<AdminModuleDto>> GetModulesAsync(string? search, CancellationToken ct = default)
    {
        var q = db.Modules.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(m => EF.Functions.ILike(m.Title, $"%{search.Trim()}%"));

        return await q
            .OrderBy(m => m.Track.Level.OrderIndex).ThenBy(m => m.Track.OrderIndex).ThenBy(m => m.OrderIndex)
            .Select(m => new AdminModuleDto(
                m.Id, m.Title, m.Slug, m.Track.Level.Name, m.Track.Level.RequiredPlanTier,
                m.Status == ModuleStatus.Published, m.IsPreview, m.RequiredPlanTier))
            .ToListAsync(ct);
    }

    public async Task SetModulePublishedAsync(Guid actorUserId, Guid moduleId, bool published, CancellationToken ct = default)
    {
        var m = await db.Modules.Include(x => x.Track).ThenInclude(t => t.Level)
            .FirstOrDefaultAsync(x => x.Id == moduleId, ct)
            ?? throw new AdminException("Modul tidak ditemukan.", 404);

        m.Status = published ? ModuleStatus.Published : ModuleStatus.Draft;
        if (published && m.PublishedAt is null) m.PublishedAt = DateTimeOffset.UtcNow;

        Audit(actorUserId, published ? "module_published" : "module_unpublished", m.Id.ToString(),
            new { m.Title, m.Slug });
        await db.SaveChangesAsync(ct);

        await revalidator.RevalidateAsync(
            new[] { "/catalog", $"/catalog/{m.Track.Level.Slug}", $"/modules/{m.Slug}" }, ct);
    }

    public async Task<IReadOnlyList<AdminPlanDto>> GetPlansAsync(CancellationToken ct = default)
        => await db.Plans.OrderBy(p => p.TierLevel)
            .Select(p => new AdminPlanDto(p.Id, p.Name, p.TierLevel, p.PriceMonthly, p.PriceAnnual, p.IsActive))
            .ToListAsync(ct);

    public async Task UpdatePlanPricesAsync(Guid actorUserId, IReadOnlyList<PlanPriceUpdate> items, CancellationToken ct = default)
    {
        if (items.Count == 0) return;

        var ids = items.Select(i => i.PlanId).ToList();
        var plans = await db.Plans.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

        foreach (var item in items)
        {
            var plan = plans.FirstOrDefault(p => p.Id == item.PlanId);
            if (plan is null) continue;
            if (item.PriceMonthly < 0 || item.PriceAnnual < 0)
                throw new AdminException("Harga tidak boleh negatif.");

            // Grandfathering (GR-8): editing plans.price affects NEW subscribers immediately;
            // existing subscribers keep subscriptions.price_locked_idr until renewal.
            Audit(actorUserId, "plan_prices_updated", plan.Id.ToString(), new
            {
                plan.Name,
                oldMonthly = plan.PriceMonthly, newMonthly = item.PriceMonthly,
                oldAnnual = plan.PriceAnnual, newAnnual = item.PriceAnnual,
            });
            plan.PriceMonthly = item.PriceMonthly;
            plan.PriceAnnual = item.PriceAnnual;
        }

        await db.SaveChangesAsync(ct);
        await revalidator.RevalidateAsync(new[] { "/pricing", "/" }, ct);
    }

    private void Audit(Guid actorUserId, string action, string target, object metadata) =>
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actorUserId,
            Action = action,
            Target = target,
            Metadata = JsonSerializer.Serialize(metadata),
        });
}
