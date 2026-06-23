using Academy.Domain.Enums;

namespace Academy.Application.Engagement;

public record VideoNoteDto(Guid Id, int TimestampSeconds, string Type, string? Text, DateTimeOffset CreatedAt);
public record CreateNoteRequest(int TimestampSeconds, string Type, string? Text); // Type: Note | Bookmark

public interface INotesService
{
    Task<IReadOnlyList<VideoNoteDto>> ListAsync(Guid userId, Guid moduleId, CancellationToken ct = default);
    Task<VideoNoteDto> CreateAsync(Guid userId, Guid moduleId, int timestampSeconds, NoteType type, string? text, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid noteId, CancellationToken ct = default);
}

public record ModuleFeedbackDto(int? MyRating, string? MyComment, double AverageRating, int Count);
public record UpsertFeedbackRequest(int Rating, string? Comment);

public interface IModuleFeedbackService
{
    Task<ModuleFeedbackDto> GetAsync(Guid? userId, Guid moduleId, CancellationToken ct = default);
    Task UpsertAsync(Guid userId, Guid moduleId, int rating, string? comment, CancellationToken ct = default);
}
