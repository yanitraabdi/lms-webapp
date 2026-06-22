// Level/Track/Category/Tag/Module/Resource (TSD §6.2)
using Academy.Domain.Common;
using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public class Level : Entity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;           // UNIQUE
    public int RequiredPlanTier { get; set; }              // 0..3
    public int OrderIndex { get; set; }
    public ModuleStatus Status { get; set; } = ModuleStatus.Draft;
    public ICollection<Track> Tracks { get; set; } = new List<Track>();
}

public class Track : Entity
{
    public Guid LevelId { get; set; }
    public Level Level { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public int OrderIndex { get; set; }
    public ICollection<Module> Modules { get; set; } = new List<Module>();
}

public class Category : Entity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;           // UNIQUE
}

public class Tag : Entity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;           // UNIQUE
}

public class Module : Entity
{
    public Guid TrackId { get; set; }
    public Track Track { get; set; } = default!;
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;           // UNIQUE — public SEO route
    public string Description { get; set; } = default!;
    public string? Summary { get; set; }
    public int DurationSeconds { get; set; }

    public string? ProviderAssetId { get; set; }           // Bunny asset id (set on encode webhook)
    public string? ThumbnailUrl { get; set; }
    public int OrderIndex { get; set; }

    public ModuleStatus Status { get; set; } = ModuleStatus.Draft;
    public bool IsPreview { get; set; }                    // admin flag → free tier
    public int RequiredPlanTier { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }       // drives "what's new"
    public DateTimeOffset? LastRefreshedAt { get; set; }

    public ICollection<ModuleTag> ModuleTags { get; set; } = new List<ModuleTag>();
    public ICollection<Resource> Resources { get; set; } = new List<Resource>();
}

public class ModuleTag                                     // m:n join (composite PK)
{
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = default!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = default!;
}

public class Resource : Entity
{
    public Guid ModuleId { get; set; }
    public Module Module { get; set; } = default!;
    public ResourceType Type { get; set; }
    public string Ref { get; set; } = default!;            // R2 key (pdf) or url (link)
    public string Title { get; set; } = default!;
}
