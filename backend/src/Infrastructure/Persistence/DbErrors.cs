using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Academy.Infrastructure.Persistence;

public static class DbErrors
{
    /// <summary>True only for a unique violation of the named index/constraint.</summary>
    public static bool IsUniqueViolation(DbUpdateException ex, string constraintName)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
           && pg.ConstraintName == constraintName;

    /// <summary>The filtered unique index on session_parts.assessment_id. Verify against the
    /// generated migration's index name (EFCore.NamingConventions) and keep in sync.</summary>
    public const string SessionPartAssessmentIndex = "ix_session_parts_assessment_id";
}
