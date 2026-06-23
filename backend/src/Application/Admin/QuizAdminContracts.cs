namespace Academy.Application.Admin;

public record AdminQuizQuestionDto(Guid Id, string Prompt, IReadOnlyList<string> Choices, int CorrectIndex);
public record AdminQuizDto(Guid Id, Guid ModuleId, int PassThreshold, bool IsActive, IReadOnlyList<AdminQuizQuestionDto> Questions);

public record QuizQuestionInput(string Prompt, IReadOnlyList<string> Choices, int CorrectIndex);
public record UpsertQuizRequest(int PassThreshold, bool IsActive, IReadOnlyList<QuizQuestionInput> Questions);

public interface IQuizAdminService
{
    Task<AdminQuizDto?> GetAsync(Guid moduleId, CancellationToken ct = default);
    Task UpsertAsync(Guid actor, Guid moduleId, UpsertQuizRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid actor, Guid moduleId, CancellationToken ct = default);
}
