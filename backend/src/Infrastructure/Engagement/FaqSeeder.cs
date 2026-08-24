using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Engagement;

/// <summary>Seeds starter FAQ entries for INVERTA (Bahasa Indonesia). Idempotent.
/// Copy is deliberately explicit that the score is a PREDICTION, not an official ETS result (GR-14).</summary>
public class FaqSeeder(AppDbContext db)
{
    private static readonly (string Q, string A)[] Items =
    [
        ("Apa itu INVERTA?",
            "Program persiapan TOEFL terstruktur dalam Bahasa Indonesia: video pembelajaran dengan tes singkat di setiap sesi, sesi live bersama pengajar, dan simulasi tes akhir berformat TOEFL ITP yang menghasilkan prediksi skor."),
        ("Apakah ini langganan bulanan?",
            "Bukan. INVERTA dibeli SEKALI BAYAR untuk keseluruhan program. Tidak ada penagihan berulang dan tidak ada perpanjangan otomatis."),
        ("Apakah skornya adalah skor TOEFL resmi?",
            "Bukan. Skor dan sertifikat yang Anda terima adalah PREDIKSI internal INVERTA berdasarkan simulasi, bukan skor TOEFL resmi dan tidak diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik Educational Testing Service (ETS). Gunakan prediksi ini untuk mengukur kesiapan sebelum mengambil tes resmi."),
        ("Bagaimana urutan belajarnya?",
            "Program bersifat berurutan. Sesi berikutnya terbuka setelah sesi sebelumnya selesai: video harus ditonton sampai tuntas DAN tes sesinya lulus, kehadiran sesi live ditandai oleh admin, dan tes akhir cukup dikirim."),
        ("Berapa kali saya boleh mengulang tes sesi?",
            "Tes sesi umumnya dapat diulang tanpa batas sampai Anda lulus, kecuali admin menetapkan batas untuk tes tertentu. Tujuannya memastikan pemahaman, bukan menjegal Anda."),
        ("Bagaimana dengan tes akhir?",
            "Tes akhir mengikuti format TOEFL ITP: tiga bagian berwaktu (Listening 35 menit, Structure 25 menit, Reading 55 menit). Waktu berjalan di server dan tidak dapat dijeda. Secara bawaan tes akhir hanya dapat dikerjakan satu kali; admin dapat memberikan kesempatan mengulang bila diperlukan."),
        ("Apa itu pengawasan saat tes akhir?",
            "Sistem mendeteksi bila Anda meninggalkan halaman tes. Pelanggaran pertama memberi peringatan, pelanggaran kedua membuat tes dikirim otomatis dan ditandai untuk ditinjau. Perpindahan fokus sangat singkat (di bawah 2 detik) diabaikan. Pengawasan ini bersifat pencegahan, bukan pengawasan ujian penuh — jika Anda merasa ada salah deteksi, hubungi kami untuk peninjauan."),
        ("Kapan sertifikat saya terbit?",
            "Segera setelah tes akhir dikirim dan dinilai. Sertifikat dikirim ke email Anda, dapat diunduh dari aplikasi, dan dapat diverifikasi publik melalui kode verifikasinya."),
        ("Apakah sertifikat tetap berlaku jika akses saya dicabut?",
            "Ya. Sertifikat yang sudah terbit bersifat permanen dan tetap dapat diverifikasi. Progres belajar dan hasil tes Anda juga tidak dihapus."),
        ("Metode pembayaran apa yang didukung?",
            "Pembayaran diproses melalui Xendit: transfer bank/virtual account, e-wallet, kartu, dan QRIS."),
        ("Saya sudah membayar tetapi program belum terbuka.",
            "Akses terbuka otomatis setelah pembayaran dikonfirmasi oleh penyedia pembayaran, biasanya dalam beberapa menit. Jika lebih lama, hubungi kami melalui halaman Kontak dengan menyertakan bukti pembayaran."),
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
