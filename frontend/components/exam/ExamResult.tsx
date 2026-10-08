import Link from "next/link";
import { num, type AttemptResult } from "@/lib/sessions";

export function ExamResult({ result, sessionId }: { result: AttemptResult; sessionId?: string | null }) {
  // ITP order, same names as the certificate page.
  const sections = ["Listening", "Structure", "Reading"].filter((s) => s in result.sectionScores);

  return (
    <div className="flex flex-col gap-5">
      <div className="rounded-lg border border-border bg-surface p-6 text-center shadow-sm">
        <h1 id="exam-result-title" tabIndex={-1} className="text-xl font-extrabold focus:outline-none">Tes selesai</h1>

        {result.totalScaledScore != null ? (
          <>
            <p className="mt-4 text-[12px] font-bold uppercase tracking-wide text-ink-subtle">
              Skor prediksi TOEFL
            </p>
            <p className="text-[52px] font-extrabold leading-none tracking-tight text-primary">
              {num(result.totalScaledScore)}
            </p>
            {result.predictedBand && (
              <p className="mt-1 text-[13.5px] font-semibold text-ink-muted">{result.predictedBand}</p>
            )}
          </>
        ) : (
          <div className="mt-4 rounded-base bg-warning-soft px-4 py-3 text-[13px] text-ink">
            Skor mentah Anda tersimpan, namun konversi ke skor prediksi belum tersedia.
            Tim kami akan menerbitkan sertifikat Anda setelah tabel konversi dilengkapi.
          </div>
        )}

        <p className="mt-4 text-[13px] text-ink-muted">
          Jawaban benar: <strong className="text-ink">{num(result.score)}</strong> dari {num(result.maxScore)}
        </p>

        {result.autoSubmitted && (
          <p className="mt-2 text-[12.5px] font-semibold text-warning">
            Sebagian atau seluruh tes ditutup otomatis (waktu habis atau pengawasan).
          </p>
        )}
        {result.proctorFlagged && (
          <p className="mt-1 text-[12.5px] font-semibold text-danger">
            Ditandai untuk ditinjau. Hubungi admin jika Anda merasa ini keliru.
          </p>
        )}
      </div>

      {sections.length > 0 && (
        <div className="rounded-lg border border-border bg-surface p-5 shadow-sm">
          <h2 className="text-sm font-extrabold">Rincian per bagian</h2>
          <ul className="mt-3 flex flex-col gap-2">
            {sections.map((name) => {
              const scaled = result.scaledScores?.[name];
              const raw = result.sectionScores[name];
              return (
                <li key={name} className="flex items-center justify-between text-[13.5px]">
                  <span className="text-ink-muted">{name}</span>
                  <span>
                    {scaled != null && <span className="font-bold text-ink">{num(scaled)} </span>}
                    <span className={scaled != null ? "text-ink-muted" : "font-bold text-ink"}>{num(raw)} benar</span>
                  </span>
                </li>
              );
            })}
          </ul>
        </div>
      )}

      <div className="rounded-base border border-border bg-surface-2 px-5 py-4">
        <p className="text-[12px] leading-relaxed text-ink-muted">
          <strong className="text-ink">Penting.</strong> Skor ini adalah <strong className="text-ink">prediksi
          INVERTA</strong> berdasarkan simulasi internal — <strong className="text-ink">bukan skor TOEFL
          resmi</strong> dan tidak diterbitkan oleh ETS. TOEFL adalah merek dagang terdaftar milik ETS.
        </p>
      </div>

      <div className="flex flex-wrap gap-3">
        <Link
          href="/app/certificates"
          className="inline-flex h-10 items-center justify-center rounded-sm bg-primary px-4 text-[13px] font-bold text-primary-ink hover:bg-primary-hover"
        >
          Lihat sertifikat
        </Link>
        {sessionId && (
          <Link
            href={`/app/session/${sessionId}`}
            className="inline-flex h-10 items-center justify-center rounded-sm border border-border bg-surface px-4 text-[13px] font-bold text-ink hover:bg-surface-2"
          >
            Kembali ke sesi
          </Link>
        )}
      </div>
    </div>
  );
}
