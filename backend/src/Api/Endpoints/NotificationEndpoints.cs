using System.Security.Claims;
using Academy.Application.Engagement;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/notifications").RequireAuthorization().WithTags("Notifications");

        g.MapGet("", async Task<Ok<NotificationListDto>> (
            bool? unreadOnly, ClaimsPrincipal u, INotificationService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListAsync(u.UserId(), unreadOnly ?? false, ct)));

        g.MapPost("/{id:guid}/read", async Task<NoContent> (Guid id, ClaimsPrincipal u, INotificationService s, CancellationToken ct) =>
        { await s.MarkReadAsync(u.UserId(), id, ct); return TypedResults.NoContent(); });

        g.MapPost("/read-all", async Task<NoContent> (ClaimsPrincipal u, INotificationService s, CancellationToken ct) =>
        { await s.MarkAllReadAsync(u.UserId(), ct); return TypedResults.NoContent(); });

        g.MapGet("/preferences", async Task<Ok<IReadOnlyList<NotificationPrefDto>>> (ClaimsPrincipal u, INotificationService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetPreferencesAsync(u.UserId(), ct)));

        g.MapPut("/preferences", async Task<NoContent> (UpdatePreferencesRequest r, ClaimsPrincipal u, INotificationService s, CancellationToken ct) =>
        { await s.UpdatePreferencesAsync(u.UserId(), r.Preferences, ct); return TypedResults.NoContent(); });

        return app;
    }
}
