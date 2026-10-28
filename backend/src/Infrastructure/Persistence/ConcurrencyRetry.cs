using Academy.Application.Assessments;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Persistence;

/// <summary>
/// Re-runs an operation that read-modify-writes a row with a concurrency token, on fresh data,
/// when another request wrote the row first. The operation must be safe to repeat from scratch:
/// it re-reads everything it needs (it is not handed tracked entities from outside).
/// </summary>
public static class ConcurrencyRetry
{
    public static async Task<T> RunAsync<T>(AppDbContext db, Func<Task<T>> operation, int attempts = 3)
    {
        for (var i = 1; ; i++)
        {
            try { return await operation(); }
            catch (DbUpdateConcurrencyException) when (i < attempts)
            {
                db.ChangeTracker.Clear();      // drop the stale tracked rows; the retry re-reads them
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                throw new AssessmentException("Tes sedang diperbarui dari permintaan lain. Coba lagi.", 409);
            }
        }
    }

    public static Task RunAsync(AppDbContext db, Func<Task> operation, int attempts = 3)
        => RunAsync(db, async () => { await operation(); return true; }, attempts);
}
