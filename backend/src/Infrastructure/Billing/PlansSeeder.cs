using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Billing;

/// <summary>Seeds the tier plans (Free/Beginner/Intermediate/Advanced). Idempotent.
/// Prices are placeholders (PRD §16.5 — finalize before go-live); admin-editable later.</summary>
public class PlansSeeder(AppDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Plans.AnyAsync(ct)) return;

        db.Plans.AddRange(
            Plan("Free", 0, 0m, 0m, "Pratinjau modul Basic terpilih."),
            Plan("Beginner", 1, 149_000m, 1_490_000m, "Semua modul Level Basic + sertifikat Basic."),
            Plan("Intermediate", 2, 249_000m, 2_490_000m, "Basic + Intermediate (kumulatif) + sertifikat."),
            Plan("Advanced", 3, 349_000m, 3_490_000m, "Seluruh kurikulum + semua sertifikat."));

        await db.SaveChangesAsync(ct);
    }

    private static Plan Plan(string name, int tier, decimal monthly, decimal annual, string desc) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        TierLevel = tier,
        PriceMonthly = monthly,
        PriceAnnual = annual,
        IsActive = true,
        Description = desc,
        IncludedContentMapping = "{}",
    };
}
