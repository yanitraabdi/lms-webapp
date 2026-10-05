using Academy.Application.Abstractions;
using Academy.Application.Programs;

namespace Academy.Infrastructure.Learning;

/// <summary>Stands in when there is no library to list: the dev provider, or Bunny without an API
/// key. Reports why, so the admin picker can say so and offer manual entry.</summary>
public class UnavailableVideoLibrary(string reason) : IVideoLibrary
{
    public Task<VideoLibraryPageDto> ListAsync(string? search, int page, CancellationToken ct = default)
        => Task.FromResult(new VideoLibraryPageDto([], 1, 0, reason));

    private const string NotActive = "Unggah video hanya tersedia saat Bunny aktif.";

    public Task<UploadTicketDto> CreateUploadAsync(string title, CancellationToken ct = default)
        => throw new ProgramException(NotActive, 409);

    public UploadTicketDto RenewUpload(string videoId) => throw new ProgramException(NotActive, 409);
}
