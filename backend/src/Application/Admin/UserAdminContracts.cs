using Academy.Domain.Enums;

namespace Academy.Application.Admin;

public record AdminUserListItemDto(
    Guid Id, string Email, string Name, string Role, string Status, int? ActiveTier, DateTimeOffset CreatedAt);

public record AdminUserListDto(IReadOnlyList<AdminUserListItemDto> Users, int Total);

public record AdminPaymentDto(decimal AmountIdr, string Kind, string Status, DateTimeOffset CreatedAt);

public record AdminUserDetailDto(
    Guid Id, string Email, string Name, string Role, string Status, bool EmailVerified, DateTimeOffset CreatedAt,
    int? ActiveTier, string? PlanName, string? SubscriptionStatus, DateTimeOffset? CurrentPeriodEnd,
    int CompletedModules, int CertificateCount, IReadOnlyList<AdminPaymentDto> RecentPayments);

public record SetUserStatusRequest(string Status);  // Active | Suspended
public record SetUserRoleRequest(string Role);      // User | Admin | SuperAdmin
public record GrantPlanRequest(Guid PlanId, int Days);

public interface IUserAdminService
{
    Task<AdminUserListDto> ListAsync(string? search, string? status, int? tier, int skip, int take, CancellationToken ct = default);
    Task<AdminUserDetailDto> GetAsync(Guid userId, CancellationToken ct = default);
    Task SetStatusAsync(Guid actor, Guid userId, UserStatus status, CancellationToken ct = default);
    Task SetRoleAsync(Guid actor, Guid userId, UserRole role, CancellationToken ct = default);
    Task GrantPlanAsync(Guid actor, Guid userId, Guid planId, int days, CancellationToken ct = default);
    Task RevokeAsync(Guid actor, Guid userId, CancellationToken ct = default);
}

// ---- analytics ----

public record TierCountDto(int Tier, string Name, int Count);
public record ModuleWatchDto(string Title, int Viewers);

public record AdminAnalyticsDto(
    int TotalUsers,
    int SignupsLast30Days,
    int ActiveSubscriptions,
    IReadOnlyList<TierCountDto> ActiveByTier,
    int CompletionsLast30Days,
    int CertificatesIssued,
    IReadOnlyList<ModuleWatchDto> MostWatched);

public interface IAdminAnalyticsService
{
    Task<AdminAnalyticsDto> GetAsync(CancellationToken ct = default);
}
