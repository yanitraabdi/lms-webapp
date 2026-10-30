"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState, AlertTriangleIcon, SheetPage, RunningHead, PageTitle, Footnote } from "@/components/ui";
import { getSessionAssessment, startAttempt, num, type StudentAssessment } from "@/lib/sessions";

export default function AssessmentPage() {
  const { status, accessToken } = useAuth();
  const router = useRouter();
  const sessionId = useParams<{ id: string }>().id;

  useEffect(() => {
    if (status === "unauthenticated") router.replace(`/login?next=/app/assessment/${sessionId}`);
  }, [status, sessionId, router]);

  if (status !== "authenticated" || !accessToken) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  return <IntroPage token={accessToken} sessionId={sessionId} />;
}

function IntroPage({ token, sessionId }: { token: string; sessionId: string }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Never served from cache: a stale openAttemptId would bounce a learner into a finished sitting.
  const meta = useQuery({
    queryKey: ["assessment-meta", sessionId],
    queryFn: () => getSessionAssessment(token, sessionId),
    retry: false,
    gcTime: 0,
  });
  const openAttemptId = meta.data?.openAttemptId;

  // A sitting already under way is resumed, never restarted from the intro.
  useEffect(() => {
    if (openAttemptId) router.replace(`/app/exam/${openAttemptId}`);
  }, [openAttemptId, router]);

  async function begin() {
    if (!meta.data) return;
    setBusy(true); setError(null);
    try {
      const attempt = await startAttempt(token, meta.data.id);
      router.replace(`/app/exam/${attempt.id}`);       // busy stays on while the page changes
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal memulai tes.");
      setBusy(false);
    }
  }

  if (meta.isPending || openAttemptId) {
    return <div className="flex min-h-screen items-center justify-center bg-bg"><Spinner size={24} /></div>;
  }
  if (meta.isError || !meta.data) {
    return (
      <SheetPage>
        <ErrorState
          title="Tes tidak tersedia"
          message="Selesaikan sesi sebelumnya terlebih dahulu."
          action={<Link href="/app/dashboard" className="text-sm font-bold text-primary hover:underline">Ke dasbor</Link>}
        />
      </SheetPage>
    );
  }
  return (
    <SheetPage>
      <Intro test={meta.data} onBegin={begin} busy={busy} error={error} />
    </SheetPage>
  );
}

function Intro({
  test, onBegin, busy, error,
}: { test: StudentAssessment; onBegin: () => void; busy: boolean; error: string | null }) {
  return (
    <>
      <RunningHead>Tes akhir</RunningHead>
      <PageTitle>{test.title}</PageTitle>
      <p className="mt-4 max-w-[60ch] leading-relaxed text-ink-muted">
        Tes ini terdiri dari beberapa bagian berwaktu. Waktu setiap bagian berjalan di server dan
        tidak dapat dijeda. Setelah sebuah bagian selesai, Anda <strong className="text-ink">tidak
        dapat kembali</strong> ke bagian tersebut.
      </p>

      <table className="data-table mt-6">
        <tbody>
          <tr><td className="text-ink-muted">Jumlah soal</td><td className="font-bold">{num(test.questionCount)}</td></tr>
          {test.timeLimitMinutes != null && (
            <tr><td className="text-ink-muted">Total waktu</td><td className="font-bold">{num(test.timeLimitMinutes)} menit</td></tr>
          )}
          <tr>
            <td className="text-ink-muted">Kesempatan</td>
            <td className="font-bold">{test.retakeCap == null ? "tidak dibatasi" : `${num(test.retakeCap)}× (terpakai ${num(test.attemptsUsed)})`}</td>
          </tr>
        </tbody>
      </table>

      {test.proctoringEnabled && (
        <div className="mt-6 rounded-sm bg-warning-soft px-4 py-3.5">
          <div className="flex items-start gap-2.5">
            <AlertTriangleIcon size={18} className="mt-0.5 shrink-0 text-warning" />
            <div className="text-[13px] leading-relaxed text-ink">
              <strong>Pengawasan aktif.</strong> Sistem mendeteksi jika Anda meninggalkan halaman tes.
              Peringatan diberikan pada pelanggaran pertama; pada pelanggaran kedua tes
              <strong> dikirim otomatis</strong> dan ditandai untuk ditinjau.
              <div className="mt-1.5 text-[12px] text-ink-muted">
                Perpindahan fokus yang sangat singkat (di bawah 2 detik) diabaikan. Pengawasan ini
                bersifat pencegahan, bukan pengawasan ujian penuh.
              </div>
            </div>
          </div>
        </div>
      )}

      <Footnote>
        Skor yang dihasilkan adalah <strong className="text-ink">prediksi INVERTA</strong>,
        bukan skor TOEFL resmi dan tidak diterbitkan oleh ETS.
      </Footnote>

      {error && (
        <div className="mt-4 rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>
      )}

      <Button className="mt-5" fullWidth onClick={onBegin} loading={busy} disabled={!test.canAttempt}>
        {test.canAttempt ? "Mulai tes" : "Batas percobaan tercapai"}
      </Button>
    </>
  );
}
