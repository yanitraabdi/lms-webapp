using Academy.Application.Admin;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// The admin dashboard was built for the archived subscription product and kept measuring it after
/// the pivot: active subscriptions per plan tier, and a most-watched list grouped by module id.
///
/// The grouping was the live bug. `WatchProgress` carries BOTH a dormant `ModuleId` and INVERTA's
/// `SessionId`, and every INVERTA row leaves `ModuleId` null — so all of them collapsed into one
/// null bucket whose title resolved to "—". The panel could not show anything else.
///
/// These tests pin each number to the table it is supposed to count, because a metric reading the
/// wrong table is worse than no metric: it is confidently wrong.
/// </summary>
public class AdminAnalyticsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private async Task<AdminAnalyticsDto> Analytics()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAdminAnalyticsService>().GetAsync();
    }

    private async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>A programme with one video session, and a learner enrolled on it.</summary>
    private async Task<(Guid UserId, Guid SessionId)> SeedEnrolledLearner(string suffix)
    {
        return await WithDb(async db =>
        {
            var user = new User
            {
                Id = Guid.CreateVersion7(),
                Name = $"Analytics {suffix}",
                Email = $"analytics-{suffix}-{Guid.NewGuid():N}@test.local",
                PasswordHash = "x",
                EmailVerified = true,
            };
            db.Users.Add(user);

            // Fully qualified: the test project references Academy.Api, whose top-level Program
            // class sits in the global namespace and wins the unqualified name.
            var program = new Academy.Domain.Entities.Program
            {
                Id = Guid.CreateVersion7(),
                Name = $"Analytics Program {suffix}",
                Slug = $"analytics-{Guid.NewGuid():N}",
                Description = "d",
                PriceIdr = 1m,
                Status = ProgramStatus.Published,
            };
            db.Programs.Add(program);

            var session = new ProgramSession
            {
                Id = Guid.CreateVersion7(),
                ProgramId = program.Id,
                OrderIndex = 0,
                Type = SessionType.Video,
                Title = $"Sesi {suffix}",
            };
            db.ProgramSessions.Add(session);

            db.Enrollments.Add(new Enrollment
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                ProgramId = program.Id,
                Status = EnrollmentStatus.Active,
                EnrolledAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
            return (user.Id, session.Id);
        });
    }

    [Fact]
    public async Task Active_enrollments_are_counted_not_subscriptions()
    {
        // INVERTA is a one-time purchase. The subscription table belongs to the archived product
        // and will sit at whatever stale value it held on the day of the pivot.
        var before = (await Analytics()).ActiveEnrollments;

        await SeedEnrolledLearner("enroll");

        Assert.Equal(before + 1, (await Analytics()).ActiveEnrollments);
    }

    [Fact]
    public async Task A_revoked_enrollment_is_not_counted_as_active()
    {
        var (userId, _) = await SeedEnrolledLearner("revoke");
        var before = (await Analytics()).ActiveEnrollments;

        await WithDb(async db =>
        {
            var e = await db.Enrollments.FirstAsync(x => x.UserId == userId);
            e.Status = EnrollmentStatus.Revoked;   // access only — the row is retained (GR-7)
            return await db.SaveChangesAsync();
        });

        Assert.Equal(before - 1, (await Analytics()).ActiveEnrollments);
    }

    [Fact]
    public async Task Session_completions_are_counted_not_module_watch_rows()
    {
        // The old metric counted WatchProgress.Completed, which mixes archived module watching
        // with INVERTA session watching. SessionCompletions is the signal the linear lock uses,
        // so it is the one an admin should see.
        var (userId, sessionId) = await SeedEnrolledLearner("completion");
        var before = (await Analytics()).SessionCompletionsLast30Days;

        await WithDb(async db =>
        {
            db.SessionCompletions.Add(new SessionCompletion
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SessionId = sessionId,
                CompletedAt = DateTimeOffset.UtcNow,
                Method = CompletionMethod.WatchAndTest,
            });
            return await db.SaveChangesAsync();
        });

        Assert.Equal(before + 1, (await Analytics()).SessionCompletionsLast30Days);
    }

    [Fact]
    public async Task A_completion_older_than_thirty_days_is_excluded()
    {
        var (userId, sessionId) = await SeedEnrolledLearner("old");
        var before = (await Analytics()).SessionCompletionsLast30Days;

        await WithDb(async db =>
        {
            db.SessionCompletions.Add(new SessionCompletion
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SessionId = sessionId,
                CompletedAt = DateTimeOffset.UtcNow.AddDays(-31),
                Method = CompletionMethod.WatchAndTest,
            });
            return await db.SaveChangesAsync();
        });

        Assert.Equal(before, (await Analytics()).SessionCompletionsLast30Days);
    }

    [Fact]
    public async Task Most_watched_lists_sessions_by_title_never_a_dash()
    {
        // The live bug: grouping by ModuleId put every INVERTA row in one null bucket whose title
        // resolved to "—", so the panel could only ever show a dash.
        var (userId, sessionId) = await SeedEnrolledLearner("watched");

        await WithDb(async db =>
        {
            db.WatchProgress.Add(new WatchProgress
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SessionId = sessionId,
                ModuleId = null,                  // exactly how INVERTA writes it
                PercentComplete = 80m,
                LastWatchedAt = DateTimeOffset.UtcNow,
            });
            return await db.SaveChangesAsync();
        });

        var watched = (await Analytics()).MostWatched;

        Assert.Contains(watched, w => w.Title.StartsWith("Sesi watched"));
        Assert.DoesNotContain(watched, w => w.Title == "—");
    }

    [Fact]
    public async Task The_dto_no_longer_reports_the_archived_subscription_product()
    {
        // A compile-time guard as much as a runtime one: if someone reintroduces a subscription
        // field, this stops naming it and the test stops compiling, which is the point.
        var properties = typeof(AdminAnalyticsDto).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("ActiveSubscriptions", properties);
        Assert.DoesNotContain("ActiveByTier", properties);
    }
}
