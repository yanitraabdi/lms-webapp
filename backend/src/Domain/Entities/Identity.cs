// Identity, external logins, refresh tokens, B2B org (TSD §6.1)
using Academy.Domain.Common;
using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public class User : Entity
{
    public string Email { get; set; } = default!;          // UNIQUE
    public string? PasswordHash { get; set; }              // null for SSO-only accounts
    public string Name { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.User;
    public bool EmailVerified { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;

    // UU PDP erasure: soft-delete + anonymization (GR-10). Filtered out by global query filter.
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset? AnonymizedAt { get; set; }

    // Lockout (auth brute-force protection, M1).
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEndsAt { get; set; }

    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

/// <summary>Multi-provider by design. MVP: provider == "google".</summary>
public class UserExternalLogin : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string Provider { get; set; } = default!;       // UNIQUE(Provider, ProviderKey)
    public string ProviderKey { get; set; } = default!;
}

/// <summary>Rotating refresh tokens with family-based reuse detection (TSD §7.1).</summary>
public class RefreshToken : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public Guid FamilyId { get; set; }                     // revoke whole family on reuse
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}

// ---- B2B-ready (entities defined now; seat-management UX is Phase 2) ----

public class Organization : Entity
{
    public string Name { get; set; } = default!;
    public Guid BillingOwnerUserId { get; set; }
    public string Status { get; set; } = "active";
    public ICollection<OrgSeat> Seats { get; set; } = new List<OrgSeat>();
    public ICollection<OrgMembership> Memberships { get; set; } = new List<OrgMembership>();
}

public class OrgSeat : Entity
{
    public Guid OrgId { get; set; }
    public Organization Org { get; set; } = default!;
    public Guid? UserId { get; set; }                      // null until assigned
    public SeatStatus SeatStatus { get; set; } = SeatStatus.Unassigned;
    public DateTimeOffset? AssignedAt { get; set; }
}

public class OrgMembership : Entity
{
    public Guid OrgId { get; set; }
    public Organization Org { get; set; } = default!;
    public Guid UserId { get; set; }
    public OrgMemberRole MemberRole { get; set; } = OrgMemberRole.Member;
}
