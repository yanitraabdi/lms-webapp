using System.Security.Claims;
using Academy.Application.Engagement;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class EngagementEndpoints
{
    public static IEndpointRouteBuilder MapEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        // ---- public ----
        app.MapGet("/api/faq", async Task<Ok<IReadOnlyList<FaqItemDto>>> (IContentService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetFaqAsync(ct))).WithTags("Content");

        app.MapPost("/api/contact", async Task<NoContent> (
            ContactRequest req, IValidator<ContactRequest> v, IContentService svc, CancellationToken ct) =>
        {
            await v.ValidateAndThrowAsync(req, ct);
            await svc.SubmitContactAsync(req, ct);
            return TypedResults.NoContent();
        }).RequireRateLimiting("auth").WithTags("Content");

        // Feedback: optional auth — ties to the user if a valid bearer is present.
        app.MapPost("/api/feedback", async Task<NoContent> (
            FeedbackRequest req, IValidator<FeedbackRequest> v, ClaimsPrincipal user, IContentService svc, CancellationToken ct) =>
        {
            await v.ValidateAndThrowAsync(req, ct);
            var userId = Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : (Guid?)null;
            await svc.SubmitFeedbackAsync(userId, req, ct);
            return TypedResults.NoContent();
        }).RequireRateLimiting("auth").WithTags("Content");

        // ---- onboarding (auth) ----
        var onboarding = app.MapGroup("/api/onboarding").RequireAuthorization().WithTags("Onboarding");

        onboarding.MapGet("", async Task<Ok<OnboardingStateDto>> (
            ClaimsPrincipal user, IOnboardingService svc, CancellationToken ct) =>
            TypedResults.Ok(await svc.GetStateAsync(user.UserId(), ct)));

        onboarding.MapPost("/tour", async Task<NoContent> (
            SaveTourRequest req, ClaimsPrincipal user, IOnboardingService svc, CancellationToken ct) =>
        {
            await svc.CompleteTourAsync(user.UserId(), req.TourKey, req.Status, ct);
            return TypedResults.NoContent();
        });

        onboarding.MapPost("/survey", async Task<NoContent> (
            SaveSurveyRequest req, ClaimsPrincipal user, IOnboardingService svc, CancellationToken ct) =>
        {
            await svc.SaveSurveyAsync(user.UserId(), req, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
