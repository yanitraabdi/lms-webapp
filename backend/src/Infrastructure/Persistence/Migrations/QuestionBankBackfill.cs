namespace Academy.Infrastructure.Persistence.Migrations;

/// <summary>
/// Assigns every pre-2026-10-05 question its bank by where it is used: in any Final assessment →
/// Simulation (this includes questions shared with a session test — no copies, spec D3);
/// otherwise (only session tests, or unused) → SessionTest. Touches only rows without a valid
/// bank, so it is idempotent. Run by migration InvertaQuestionBank — never edit after merge.
/// </summary>
public static class QuestionBankBackfill
{
    public const string Sql = """
        UPDATE questions q
        SET bank = CASE WHEN EXISTS (
                SELECT 1 FROM assessment_questions aq
                JOIN assessments a ON a.id = aq.assessment_id
                WHERE aq.question_id = q.id AND a.kind = 'Final')
            THEN 'Simulation' ELSE 'SessionTest' END
        WHERE q.bank IS NULL OR q.bank NOT IN ('Simulation', 'SessionTest');
        """;
}
