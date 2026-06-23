namespace Academy.Application.Learning;

public record QuizQuestionDto(Guid Id, string Prompt, IReadOnlyList<string> Choices); // correctIndex withheld
public record QuizDto(
    Guid Id, Guid ModuleId, int PassThreshold, int QuestionCount,
    IReadOnlyList<QuizQuestionDto> Questions, bool Passed, int? BestScore);

public record SubmitQuizRequest(IReadOnlyList<int> Answers);
public record QuizResultDto(int Score, int Total, bool Passed, bool ModuleCompleted);

public interface IQuizService
{
    /// <summary>The module's active quiz (without answers) + the user's pass state, or null if none.</summary>
    Task<QuizDto?> GetForModuleAsync(Guid userId, Guid moduleId, CancellationToken ct = default);
    Task<QuizResultDto> SubmitAsync(Guid userId, Guid moduleId, IReadOnlyList<int> answers, CancellationToken ct = default);
}

/// <summary>Single place that decides module completion: watch ≥ threshold AND quiz gate satisfied.
/// Idempotent and non-retroactive (never un-completes). Triggers certificate issuance.</summary>
public interface IModuleCompletionService
{
    Task<bool> TryCompleteAsync(Guid userId, Guid moduleId, CancellationToken ct = default);
}
