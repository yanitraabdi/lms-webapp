using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Programs;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;

namespace Academy.Infrastructure.Learning;

public class VideoUploadService(IVideoLibrary library, AppDbContext db) : IVideoUploadService
{
    public async Task<UploadTicketDto> StartAsync(Guid actor, string? title, CancellationToken ct = default)
    {
        var clean = title?.Trim() ?? "";
        if (clean.Length is 0 or > 200)
            throw new ProgramException("Judul video wajib diisi (maksimal 200 karakter).", 400);

        var ticket = await library.CreateUploadAsync(clean, ct);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.CreateVersion7(), ActorUserId = actor, Action = "video_upload_started",
            Target = ticket.VideoId, Metadata = JsonSerializer.Serialize(new { videoId = ticket.VideoId, title = clean }),
        });
        await db.SaveChangesAsync(ct);
        return ticket;
    }

    public UploadTicketDto Renew(string videoId)
    {
        if (!Guid.TryParseExact(videoId, "D", out _))
            throw new ProgramException("ID video tidak valid.", 400);
        return library.RenewUpload(videoId);
    }
}
