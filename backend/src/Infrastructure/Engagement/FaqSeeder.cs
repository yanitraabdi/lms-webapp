using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Engagement;

/// <summary>Seeds starter FAQ entries (Bahasa Indonesia). Idempotent; admin editor lands later.</summary>
public class FaqSeeder(AppDbContext db)
{
    private static readonly (string Q, string A)[] Items =
    [
        ("Apa itu AI Productivity Academy?",
            "Platform belajar keterampilan AI praktis dalam Bahasa Indonesia untuk profesional — video singkat, latihan langsung, dan sertifikat yang dapat diverifikasi."),
        ("Apakah saya bisa mencoba gratis?",
            "Bisa. Daftar gratis dan tonton modul pratinjau terpilih tanpa memasukkan metode pembayaran."),
        ("Bagaimana paket berlangganan bekerja?",
            "Paket bersifat kumulatif: Basic membuka Level Basic, Intermediate menambah Level Intermediate, dan Advanced membuka seluruh kurikulum."),
        ("Bisakah saya upgrade di tengah periode?",
            "Bisa. Upgrade langsung aktif dan ditagih secara prorata. Downgrade berlaku pada periode tagihan berikutnya."),
        ("Apakah sertifikat tetap berlaku jika saya berhenti berlangganan?",
            "Ya. Sertifikat yang sudah terbit bersifat permanen dan tetap dapat diverifikasi melalui halaman publik."),
        ("Metode pembayaran apa yang didukung?",
            "Pembayaran diproses melalui Xendit: transfer bank/virtual account, e-wallet, kartu, dan QRIS."),
        ("Apakah video memiliki teks Bahasa Indonesia?",
            "Ya. Modul dilengkapi caption Bahasa Indonesia, dan Anda dapat memilih kualitas video untuk menghemat kuota."),
        ("Bagaimana kebijakan pengembalian dana?",
            "Lihat halaman Kebijakan Pengembalian Dana untuk syarat, tenggat, dan proses pengajuannya."),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.FaqItems.AnyAsync(ct)) return;

        var order = 0;
        foreach (var (q, a) in Items)
            db.FaqItems.Add(new FaqItem
            {
                Id = Guid.CreateVersion7(), Question = q, Answer = a, OrderIndex = order++, IsPublished = true,
            });

        await db.SaveChangesAsync(ct);
    }
}
