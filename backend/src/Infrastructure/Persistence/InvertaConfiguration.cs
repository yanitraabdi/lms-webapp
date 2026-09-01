// INVERTA: programs, sessions, enrollment, assessment engine (TSD-delta §2).
// FK policy per docs/DECISIONS.md: Restrict on retained/financial, Cascade intra-aggregate,
// SetNull on optional actor links.
using Academy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Academy.Infrastructure.Persistence;

public class ProgramConfig : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> e)
    {
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.PriceIdr).HasPrecision(18, 2);
    }
}

public class ProgramBatchConfig : IEntityTypeConfiguration<ProgramBatch>
{
    public void Configure(EntityTypeBuilder<ProgramBatch> e)
    {
        e.HasIndex(x => new { x.ProgramId, x.StartDate });
        e.HasOne(x => x.Program).WithMany(p => p.Batches).HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);   // intra-aggregate
    }
}

public class ProgramSessionConfig : IEntityTypeConfiguration<ProgramSession>
{
    public void Configure(EntityTypeBuilder<ProgramSession> e)
    {
        e.HasIndex(x => new { x.ProgramId, x.OrderIndex }).IsUnique();  // ordering is the lock
        e.HasOne(x => x.Program).WithMany(p => p.Sessions).HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);   // intra-aggregate
        e.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId)
            .OnDelete(DeleteBehavior.SetNull);   // optional link
    }
}

public class EnrollmentConfig : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> e)
    {
        // One enrollment row per (user, program). A re-purchase reuses/reactivates it.
        e.HasIndex(x => new { x.UserId, x.ProgramId }).IsUnique();
        e.HasIndex(x => x.ProviderRef);
        e.Property(x => x.AmountPaidIdr).HasPrecision(18, 2);
        e.Ignore(x => x.GrantsAccess);           // computed, not persisted
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);  // financial/retained
        e.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.SetNull);   // optional cohort
    }
}

public class SessionCompletionConfig : IEntityTypeConfiguration<SessionCompletion>
{
    public void Configure(EntityTypeBuilder<SessionCompletion> e)
    {
        e.HasIndex(x => new { x.UserId, x.SessionId }).IsUnique();  // idempotent completion
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);  // retained progress (GR-7)
        e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LiveAttendanceConfig : IEntityTypeConfiguration<LiveAttendance>
{
    public void Configure(EntityTypeBuilder<LiveAttendance> e)
    {
        e.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique();
        e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.MarkedByUserId)
            .OnDelete(DeleteBehavior.SetNull);   // optional actor
    }
}

// ---------------------------------------------------------------- Assessment engine

// Named ...EntityConfig to avoid colliding with the Application DTO Academy.Application
// .Assessments.AssessmentConfig (the parsed form of the jsonb `config` column).
public class AssessmentEntityConfig : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> e)
        => e.Property(x => x.Config).HasColumnType("jsonb");
}

public class QuestionConfig : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> e)
    {
        e.HasIndex(x => x.Section);
        e.Property(x => x.ExternalId).HasMaxLength(64);
        // Filtered: hand-authored questions all have NULL and must not collide with one another.
        e.HasIndex(x => x.ExternalId).IsUnique().HasFilter("external_id IS NOT NULL");
        e.Property(x => x.Choices).HasColumnType("jsonb");
        e.Property(x => x.Correct).HasColumnType("jsonb");   // server-only — never in a student DTO
        e.Property(x => x.Tags).HasColumnType("jsonb");
    }
}

public class AssessmentQuestionConfig : IEntityTypeConfiguration<AssessmentQuestion>
{
    public void Configure(EntityTypeBuilder<AssessmentQuestion> e)
    {
        e.HasIndex(x => new { x.AssessmentId, x.OrderIndex }).IsUnique();
        e.HasOne(x => x.Assessment).WithMany(a => a.Questions).HasForeignKey(x => x.AssessmentId)
            .OnDelete(DeleteBehavior.Cascade);   // intra-aggregate
        e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);  // bank item stays if referenced
    }
}

public class AttemptConfig : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> e)
    {
        e.HasIndex(x => new { x.UserId, x.AssessmentId });
        e.Property(x => x.Answers).HasColumnType("jsonb");
        e.Property(x => x.SectionScores).HasColumnType("jsonb");
        e.Property(x => x.State).HasColumnType("jsonb");
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);  // retained (GR-7)
        e.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProctorEventConfig : IEntityTypeConfiguration<ProctorEvent>
{
    public void Configure(EntityTypeBuilder<ProctorEvent> e)
    {
        e.HasIndex(x => new { x.AttemptId, x.OccurredAt });
        e.Property(x => x.ClientMeta).HasColumnType("jsonb");
        e.HasOne(x => x.Attempt).WithMany().HasForeignKey(x => x.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);   // evidence belongs to the attempt
    }
}

public class ScoreBandMappingConfig : IEntityTypeConfiguration<ScoreBandMapping>
{
    public void Configure(EntityTypeBuilder<ScoreBandMapping> e)
    {
        e.HasIndex(x => new { x.ProgramId, x.Section, x.MinRaw });
        e.HasOne(x => x.Program).WithMany().HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
