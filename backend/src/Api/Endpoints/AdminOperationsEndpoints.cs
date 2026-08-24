using System.Security.Claims;
using Academy.Application.Programs;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Academy.Api.Endpoints;

/// <summary>INVERTA M5 — live attendance and operational dashboards (KAK §9.11–§9.12).</summary>
public static class AdminOperationsEndpoints
{
    public static IEndpointRouteBuilder MapAdminOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("Admin").WithTags("AdminOperations");

        // ---- live attendance ----
        g.MapGet("/sessions/{sessionId:guid}/attendance", async Task<Ok<AttendanceRosterDto>> (
                Guid sessionId, IAttendanceService s, CancellationToken ct) =>
            TypedResults.Ok(await s.GetRosterAsync(sessionId, ct)));

        g.MapPost("/sessions/{sessionId:guid}/attendance", async Task<NoContent> (
            Guid sessionId, MarkAttendanceRequest r, ClaimsPrincipal u, IAttendanceService s, CancellationToken ct) =>
        { await s.MarkAsync(u.UserId(), sessionId, r.UserIds, r.Attended, ct); return TypedResults.NoContent(); });

        g.MapPost("/sessions/{sessionId:guid}/attendance/all", async Task<NoContent> (
            Guid sessionId, ClaimsPrincipal u, IAttendanceService s, CancellationToken ct) =>
        { await s.MarkAllAsync(u.UserId(), sessionId, ct); return TypedResults.NoContent(); });

        // ---- enrollments ----
        g.MapGet("/enrollments", async Task<Ok<AdminEnrollmentListDto>> (
                string? search, string? status, Guid? programId, int? skip, int? take,
                IAdminOperationsService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListEnrollmentsAsync(search, status, programId, skip ?? 0, take ?? 25, ct)));

        g.MapPost("/enrollments/grant", async Task<NoContent> (
            GrantEnrollmentRequest r, ClaimsPrincipal u, IAdminOperationsService s, CancellationToken ct) =>
        { await s.GrantEnrollmentAsync(u.UserId(), r, ct); return TypedResults.NoContent(); });

        // ---- attempts / proctor review ----
        g.MapGet("/attempts", async Task<Ok<AdminAttemptListDto>> (
                bool? flaggedOnly, Guid? programId, int? skip, int? take,
                IAdminOperationsService s, CancellationToken ct) =>
            TypedResults.Ok(await s.ListAttemptsAsync(flaggedOnly, programId, skip ?? 0, take ?? 25, ct)));

        g.MapGet("/attempts/{id:guid}", async Task<Results<Ok<AdminAttemptDetailDto>, NotFound>> (
            Guid id, IAdminOperationsService s, CancellationToken ct) =>
        {
            var a = await s.GetAttemptAsync(id, ct);
            return a is null ? TypedResults.NotFound() : TypedResults.Ok(a);
        });

        // ---- ops: trigger the reminder sweep on demand (also runs hourly) ----
        g.MapPost("/live-reminders/run", async Task<Ok<ReminderRunResult>> (
                ILiveSessionReminder s, CancellationToken ct) =>
            TypedResults.Ok(new ReminderRunResult(await s.SendDueRemindersAsync(ct))));

        return app;
    }
}

public record ReminderRunResult(int EmailsSent);
