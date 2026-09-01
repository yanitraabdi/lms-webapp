using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

/// <summary>
/// The upsert key. The index must be UNIQUE for imported questions and silent for hand-authored
/// ones — an unfiltered unique index would let exactly one question in the whole bank have no
/// external id, which is the opposite of what the admin UI needs.
/// </summary>
public class QuestionExternalIdTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static Question New(string? externalId) => new()
    {
        Id = Guid.CreateVersion7(),
        ExternalId = externalId,
        Section = QuestionSection.Reading,
        Prompt = "Prompt",
        Choices = """["a","b"]""",
        Correct = "[0]",
    };

    [Fact]
    public async Task Many_hand_authored_questions_share_a_null_external_id()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Questions.AddRange(New(null), New(null), New(null));
        await db.SaveChangesAsync();   // must not throw
    }

    [Fact]
    public async Task Two_questions_cannot_share_one_external_id()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var id = $"X{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        db.Questions.Add(New(id));
        await db.SaveChangesAsync();

        db.Questions.Add(New(id));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
