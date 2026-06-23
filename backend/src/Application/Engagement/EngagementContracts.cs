using Academy.Domain.Enums;

namespace Academy.Application.Engagement;

// ---- public content ----

public record FaqItemDto(Guid Id, string Question, string Answer);

public record ContactRequest(string Name, string Email, string Message);

public record FeedbackRequest(string Message, string? Context);

// ---- onboarding (auth) ----

public record OnboardingStateDto(bool TourCompleted, bool SurveyCompleted);

public record SaveTourRequest(string TourKey, TourStatus Status);

public record SaveSurveyRequest(string? Role, IReadOnlyList<string> Goals, IReadOnlyList<string> PreferredTools);

// ---- services ----

public interface IContentService
{
    Task<IReadOnlyList<FaqItemDto>> GetFaqAsync(CancellationToken ct = default);
    Task SubmitContactAsync(ContactRequest req, CancellationToken ct = default);
    Task SubmitFeedbackAsync(Guid? userId, FeedbackRequest req, CancellationToken ct = default);
}

/// <summary>First-run tour state + interest survey. Recommendation logic that consumes the
/// survey is deferred (M7); the P0 deterministic next-module fallback works without it.</summary>
public interface IOnboardingService
{
    /// <summary>The default first-run dashboard tour key.</summary>
    public const string FirstRunTourKey = "dashboard_first_run";

    Task<OnboardingStateDto> GetStateAsync(Guid userId, CancellationToken ct = default);
    Task CompleteTourAsync(Guid userId, string tourKey, TourStatus status, CancellationToken ct = default);
    Task SaveSurveyAsync(Guid userId, SaveSurveyRequest req, CancellationToken ct = default);
}
