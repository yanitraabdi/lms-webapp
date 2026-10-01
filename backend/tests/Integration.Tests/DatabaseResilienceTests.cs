using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// A Postgres restart used to be an OUTAGE rather than a blip. The pool kept sockets to a server
/// that had gone, every request hung on "Attempted to read past the end of the stream", and the
/// API stayed wedged until a human ran `docker compose restart api`. It happened ten times in two
/// days on the deployed stack.
///
/// Retry cannot be exercised honestly here — proving it would mean killing Postgres underneath a
/// live request, which is a flaky test that punishes everyone else sharing this database. So this
/// pins the CONFIGURATION instead: the thing that silently regresses is someone editing the
/// DbContext registration and dropping EnableRetryOnFailure, and that is exactly what this
/// catches.
/// </summary>
public class DatabaseResilienceTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private T Resolve<T>(Func<AppDbContext, T> read)
    {
        using var scope = factory.Services.CreateScope();
        return read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void The_database_retries_on_transient_failures()
    {
        var strategy = Resolve(db => db.Database.CreateExecutionStrategy());

        Assert.True(strategy.RetriesOnFailure,
            "EnableRetryOnFailure is gone from the DbContext registration: a Postgres restart is " +
            "an outage again, needing a manual API restart to recover.");
    }

    [Fact]
    public void Nothing_opens_an_explicit_transaction()
    {
        // The retrying strategy REFUSES user-initiated transactions — BeginTransaction throws at
        // runtime unless the caller wraps the work in the execution strategy itself. Nothing in
        // this codebase does that today, which is what makes retry safe to apply globally; this
        // fails the moment someone adds one, rather than in production on the path they added it to.
        var strategy = Resolve(db => db.Database.CreateExecutionStrategy());

        // A CALL, not the word: match ".BeginTransaction(" and skip comment lines, or this test
        // fails on prose that merely mentions the API — the first version flagged the comment
        // explaining why the rule exists.
        var sources = Directory.GetFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("Migrations", StringComparison.Ordinal))
            .Where(f => File.ReadLines(f).Any(line =>
                line.Contains(".BeginTransaction(", StringComparison.Ordinal)
                && !line.TrimStart().StartsWith("//", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(sources.Count == 0,
            $"{string.Join(", ", sources)} opens a transaction, which {strategy.GetType().Name} " +
            "refuses. Wrap the work in db.Database.CreateExecutionStrategy().ExecuteAsync(...).");
    }

    /// <summary>Walks up from the test binary to the backend's src directory.</summary>
    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src");
    }
}
