using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Integration.Tests;

public class QuestionBankBackfillTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Backfill_assigns_banks_by_usage_and_is_idempotent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Question Q() => new()
        {
            Id = Guid.CreateVersion7(), Section = QuestionSection.General, Prompt = "p",
            Bank = QuestionBank.Simulation,
        };
        Assessment A(AssessmentKind k) => new() { Id = Guid.CreateVersion7(), Kind = k, Title = k.ToString() };
        var (a, b, c, d) = (Q(), Q(), Q(), Q());
        var fin = A(AssessmentKind.Final);
        var gate = A(AssessmentKind.Gating);
        db.Questions.AddRange(a, b, c, d);
        db.Assessments.AddRange(fin, gate);
        var order = 0;
        void Link(Assessment x, Question q) => db.AssessmentQuestions.Add(new AssessmentQuestion
        { Id = Guid.CreateVersion7(), AssessmentId = x.Id, QuestionId = q.Id, OrderIndex = ++order });
        Link(fin, a); Link(gate, b); Link(fin, c); Link(gate, c);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE questions SET bank = '' WHERE id IN ({0},{1},{2},{3})", a.Id, b.Id, c.Id, d.Id);
        await db.Database.ExecuteSqlRawAsync(QuestionBankBackfill.Sql);
        await db.Database.ExecuteSqlRawAsync(QuestionBankBackfill.Sql);

        var banks = await db.Questions.AsNoTracking()
            .Where(x => new[] { a.Id, b.Id, c.Id, d.Id }.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Bank);
        Assert.Equal(QuestionBank.Simulation, banks[a.Id]);
        Assert.Equal(QuestionBank.SessionTest, banks[b.Id]);
        Assert.Equal(QuestionBank.Simulation, banks[c.Id]);
        Assert.Equal(QuestionBank.SessionTest, banks[d.Id]);
    }
}
