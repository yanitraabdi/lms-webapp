using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Learning;

/// <summary>Stands in when there is no library to list: the dev provider, or Bunny without an API
/// key. Reports why, so the admin picker can say so and offer manual entry.</summary>
public class UnavailableVideoLibrary(string reason) : IVideoLibrary
{
    public Task<VideoLibraryPageDto> ListAsync(string? search, int page, CancellationToken ct = default)
        => Task.FromResult(new VideoLibraryPageDto([], 1, 0, reason));
}
