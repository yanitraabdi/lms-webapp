using Academy.Domain.Enums;

namespace Academy.Domain;

public static class QuestionBanks
{
    /// <summary>The bank a test of this kind draws from.</summary>
    public static QuestionBank For(AssessmentKind kind)
        => kind == AssessmentKind.Final ? QuestionBank.Simulation : QuestionBank.SessionTest;
}
