using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Academy.Application.Abstractions;

namespace Academy.Infrastructure.Learning;

/// <summary>
/// Lists the Bunny Stream library for the admin video picker.
///
/// Every failure becomes an <c>Unavailable</c> message rather than an exception. The picker is a
/// convenience over manual entry, and an admin who cannot reach the list must still be able to type
/// an id and save; a 500 here would take the whole session form down with it.
/// </summary>
public class BunnyVideoLibrary(HttpClient http, VideoOptions options) : IVideoLibrary
{
    public const int PageSize = 50;

    public async Task<VideoLibraryPageDto> ListAsync(string? search, int page, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var url = $"https://video.bunnycdn.com/library/{Uri.EscapeDataString(options.LibraryId)}/videos" +
                  $"?page={page}&itemsPerPage={PageSize}&orderBy=title" +
                  (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search.Trim())}");

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("AccessKey", options.ApiKey);

        try
        {
            using var res = await http.SendAsync(req, ct);

            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Unavailable(
                    "Bunny menolak kunci API. Gunakan kunci API milik pustaka video (Stream → pustaka → API), " +
                    "bukan kunci akun atau kunci token. Sementara itu, isi ID video secara manual.");

            if (!res.IsSuccessStatusCode)
                return Unavailable($"Bunny membalas {(int)res.StatusCode}. Coba lagi, atau isi ID video secara manual.");

            var body = await res.Content.ReadFromJsonAsync<BunnyPage>(Json, ct) ?? new BunnyPage();
            return new VideoLibraryPageDto(
                body.Items.Select(v => new VideoLibraryItemDto(
                    v.Guid, v.Title ?? "", v.Length, StatusName(v.Status), v.EncodeProgress)).ToList(),
                body.CurrentPage, body.TotalItems, null);
        }
        catch (Exception e) when ((e is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            return Unavailable("Bunny tidak dapat dihubungi. Coba lagi, atau isi ID video secara manual.");
        }
    }

    /// <summary>Bunny's documented video statuses. Unknown values stay visible as "Unknown"
    /// rather than being guessed at.</summary>
    public static string StatusName(int status) => status switch
    {
        0 => "Created",
        1 => "Uploaded",
        2 => "Processing",
        3 => "Transcoding",
        4 => "Finished",
        5 => "Error",
        6 => "UploadFailed",
        7 => "JitSegmenting",
        8 => "JitPlaylistsCreated",
        _ => "Unknown",
    };

    private static VideoLibraryPageDto Unavailable(string reason) => new([], 1, 0, reason);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed class BunnyPage
    {
        public int TotalItems { get; set; }
        public int CurrentPage { get; set; } = 1;
        public List<BunnyVideo> Items { get; set; } = [];
    }

    private sealed class BunnyVideo
    {
        public string Guid { get; set; } = "";
        public string? Title { get; set; }
        public int Length { get; set; }
        public int Status { get; set; }
        public int EncodeProgress { get; set; }
    }
}
