using System.Text.RegularExpressions;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Catalog;

/// <summary>Seeds a representative sample curriculum (idempotent — no-op if any level exists).
/// Real curriculum is loaded via the admin UI (M5); this gives the catalog content to render.</summary>
public partial class CatalogSeeder(AppDbContext db)
{
    private sealed record TrackDef(string Name, string Slug, string[] Titles);
    private sealed record LevelDef(string Name, string Slug, int Tier, int Order, TrackDef[] Tracks);

    private static readonly LevelDef[] Curriculum =
    [
        new("Basic", "basic", 1, 0,
        [
            new("Fondasi AI", "fondasi-ai",
                ["Apa itu AI? Penjelasan tanpa istilah rumit", "Mengenal chatbot AI populer", "Etika & keamanan dasar AI"]),
            new("Tool Landscape", "tool-landscape",
                ["ChatGPT untuk pemula", "Google Gemini di Workspace", "Microsoft Copilot di Office", "Memilih tool AI yang tepat"]),
            new("Prompting Fundamentals", "prompting-fundamentals",
                ["Menulis prompt pertama Anda", "Pola prompt yang efektif", "Memperbaiki hasil dengan iterasi"]),
            new("Quick Wins", "quick-wins",
                ["Otomatiskan email harian dengan ChatGPT", "Ringkas dokumen panjang dalam hitungan detik"]),
        ]),
        new("Intermediate", "intermediate", 2, 1,
        [
            new("Workflow Tim", "workflow-tim",
                ["Membangun workflow tim dengan Claude Projects", "Membuat Custom GPT untuk tim", "Otomasi alur kerja dengan Zapier + AI"]),
        ]),
        new("Advanced", "advanced", 3, 2,
        [
            new("Claude Code", "claude-code",
                ["Pengantar Claude Code", "Membangun MCP server pertama", "Deploy agent AI ke produksi"]),
        ]),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Levels.AnyAsync(ct)) return;

        var categories = new[]
        {
            new Category { Name = "Produktivitas", Slug = "produktivitas" },
            new Category { Name = "Penulisan & Konten", Slug = "penulisan" },
            new Category { Name = "Data & Analisis", Slug = "data" },
            new Category { Name = "Otomasi", Slug = "otomasi" },
        };
        db.Categories.AddRange(categories);

        var tags = new[] { "ChatGPT", "Claude", "Gemini", "Prompting", "Workflow", "RAG", "MCP", "Tim" }
            .Select(name => new Tag { Name = name, Slug = name.ToLowerInvariant() }).ToArray();
        db.Tags.AddRange(tags);

        var now = DateTimeOffset.UtcNow;
        var global = 0;
        var previewBudget = 3; // default 3 free preview modules (Basic)

        foreach (var lv in Curriculum)
        {
            var level = new Level { Name = lv.Name, Slug = lv.Slug, RequiredPlanTier = lv.Tier, OrderIndex = lv.Order, Status = ModuleStatus.Published };
            db.Levels.Add(level);

            var trackOrder = 0;
            foreach (var tr in lv.Tracks)
            {
                var track = new Track { Level = level, Name = tr.Name, Slug = tr.Slug, OrderIndex = trackOrder++ };
                db.Tracks.Add(track);

                var modOrder = 0;
                foreach (var title in tr.Titles)
                {
                    var preview = lv.Tier == 1 && previewBudget > 0;
                    if (preview) previewBudget--;

                    var module = new Module
                    {
                        Track = track,
                        Title = title,
                        Slug = Slugify(title),
                        Description = $"Pelajari {title} secara praktis dan langsung bisa diterapkan dalam pekerjaan sehari-hari.",
                        Summary = $"Modul singkat (7–10 menit) tentang {title.ToLowerInvariant()}.",
                        DurationSeconds = 360 + (global % 6) * 60,
                        Status = ModuleStatus.Published,
                        IsPreview = preview,
                        RequiredPlanTier = lv.Tier,
                        OrderIndex = modOrder++,
                        PublishedAt = now.AddDays(-global),
                        Category = categories[global % categories.Length],
                    };
                    module.ModuleTags.Add(new ModuleTag { Module = module, Tag = tags[global % tags.Length] });
                    module.ModuleTags.Add(new ModuleTag { Module = module, Tag = tags[(global + 3) % tags.Length] });
                    db.Modules.Add(module);
                    global++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Slugify(string value) =>
        NonSlug().Replace(value.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}
