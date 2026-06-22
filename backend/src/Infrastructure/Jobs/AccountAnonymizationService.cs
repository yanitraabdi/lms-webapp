using Academy.Infrastructure.Auth;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Academy.Infrastructure.Jobs;

/// <summary>UU PDP: after the soft-delete grace window, scrub PII while retaining the row
/// (financial/audit integrity). Runs hourly; <see cref="RunOnceAsync"/> is callable for tests.</summary>
public class AccountAnonymizationService(
    IServiceProvider services,
    IOptions<AuthOptions> options,
    ILogger<AccountAnonymizationService> logger) : BackgroundService
{
    private readonly AuthOptions _o = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                var n = await RunOnceAsync(stoppingToken);
                if (n > 0) logger.LogInformation("Anonymized {Count} expired soft-deleted account(s).", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Account anonymization sweep failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_o.DeletionGraceDays);
        var due = await db.Users.IgnoreQueryFilters()
            .Where(u => u.DeletedAt != null && u.DeletedAt < cutoff && u.AnonymizedAt == null)
            .ToListAsync(ct);

        foreach (var u in due)
        {
            u.Email = $"deleted+{u.Id:N}@anonymized.invalid";
            u.Name = "Pengguna terhapus";
            u.PasswordHash = null;
            u.AnonymizedAt = DateTimeOffset.UtcNow;

            await db.UserExternalLogins.Where(x => x.UserId == u.Id).ExecuteDeleteAsync(ct);
            await db.RefreshTokens.Where(x => x.UserId == u.Id && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTimeOffset.UtcNow), ct);
        }

        if (due.Count > 0) await db.SaveChangesAsync(ct);
        return due.Count;
    }
}
