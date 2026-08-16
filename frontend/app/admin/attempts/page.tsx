"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Modal, Spinner, ErrorState } from "@/components/ui";
import { listAdminAttempts, getAdminAttempt, fmtDateTime, num } from "@/lib/programs";
import { reinstateAttempt } from "@/lib/sessions";

export default function AdminAttemptsPage() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();
  const [flaggedOnly, setFlaggedOnly] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);

  const attempts = useQuery({
    queryKey: ["admin-attempts", flaggedOnly],
    queryFn: () => listAdminAttempts(token!, { flaggedOnly }),
    enabled: !!token,
  });

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-ink-subtle">
          Percobaan tes. Tinjau kasus yang ditandai pengawasan dan pulihkan jika terjadi salah deteksi.
        </p>
        <label className="flex items-center gap-2 text-[13px] font-semibold">
          <input
            type="checkbox"
            checked={flaggedOnly}
            onChange={(e) => setFlaggedOnly(e.target.checked)}
            className="accent-primary"
          />
          Hanya yang ditandai
        </label>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {!token || attempts.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : attempts.isError ? (
          <div className="p-6">
            <ErrorState title="Gagal memuat percobaan"
              action={<Button variant="neutral" size="sm" onClick={() => attempts.refetch()}>Muat ulang</Button>} />
          </div>
        ) : attempts.data.items.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">
            {flaggedOnly ? "Tidak ada percobaan yang ditandai." : "Belum ada percobaan."}
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] border-collapse">
              <thead>
                <tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">
                  <th className="px-5 py-3">Peserta</th>
                  <th className="px-3 py-3">Tes</th>
                  <th className="px-3 py-3">Skor</th>
                  <th className="px-3 py-3">Prediksi</th>
                  <th className="px-3 py-3">Status</th>
                  <th className="px-5 py-3 text-right">Aksi</th>
                </tr>
              </thead>
              <tbody className="text-[13.5px]">
                {attempts.data.items.map((a) => (
                  <tr key={a.id} className="border-t border-border">
                    <td className="px-5 py-3.5">
                      <div className="font-semibold">{a.userName}</div>
                      <div className="text-[11.5px] text-ink-subtle">{a.userEmail}</div>
                    </td>
                    <td className="px-3 py-3.5">
                      <div>{a.assessmentTitle}</div>
                      <div className="text-[11.5px] text-ink-subtle">
                        {a.kind === "Final" ? "Tes akhir" : "Tes sesi"} · {fmtDateTime(a.startedAt)}
                      </div>
                    </td>
                    <td className="px-3 py-3.5">{num(a.totalScore)}/{num(a.maxScore)}</td>
                    <td className="px-3 py-3.5">
                      {a.totalScaledScore != null
                        ? <span className="font-bold text-primary">{num(a.totalScaledScore)}</span>
                        : <span className="text-ink-subtle">—</span>}
                    </td>
                    <td className="px-3 py-3.5">
                      <div className="flex flex-wrap gap-1.5">
                        {a.submittedAt == null && (
                          <Badge tone="neutral" className="px-2 py-0.5 text-[10.5px]">Berjalan</Badge>
                        )}
                        {a.autoSubmitted && (
                          <Badge tone="warning" className="px-2 py-0.5 text-[10.5px]">Auto-submit</Badge>
                        )}
                        {a.proctorFlagged && (
                          <Badge tone="danger" className="px-2 py-0.5 text-[10.5px]">Ditandai</Badge>
                        )}
                        {a.reinstated && (
                          <Badge tone="success" className="px-2 py-0.5 text-[10.5px]">Dipulihkan</Badge>
                        )}
                        {num(a.strikeCount) > 0 && (
                          <span className="text-[11px] text-ink-subtle">{num(a.strikeCount)} pelanggaran</span>
                        )}
                      </div>
                    </td>
                    <td className="px-5 py-3.5">
                      <div className="flex justify-end">
                        <Button variant="neutral" size="sm" onClick={() => setOpenId(a.id)}>Tinjau</Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {openId && token && (
        <AttemptDetail
          token={token}
          attemptId={openId}
          onClose={() => { setOpenId(null); qc.invalidateQueries({ queryKey: ["admin-attempts"] }); }}
        />
      )}
    </div>
  );
}

function AttemptDetail({
  token, attemptId, onClose,
}: { token: string; attemptId: string; onClose: () => void }) {
  const [busy, setBusy] = useState(false);
  const q = useQuery({
    queryKey: ["admin-attempt", attemptId],
    queryFn: () => getAdminAttempt(token, attemptId),
  });

  async function reinstate() {
    if (!confirm("Pulihkan percobaan ini? Tanda pengawasan dihapus, tetapi jejak kejadian tetap tersimpan.")) return;
    setBusy(true);
    try {
      await reinstateAttempt(token, attemptId);
      await q.refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Gagal memulihkan.");
    } finally {
      setBusy(false);
    }
  }

  const a = q.data?.attempt;
  const sections = Object.entries(q.data?.sectionScores ?? {});

  return (
    <Modal
      open
      onClose={onClose}
      title="Tinjau percobaan"
      className="max-w-2xl"
      footer={
        <div className="flex w-full items-center justify-between gap-2">
          {a?.proctorFlagged ? (
            <Button variant="neutral" size="sm" onClick={reinstate} loading={busy}>Pulihkan (hapus tanda)</Button>
          ) : <span />}
          <Button size="sm" onClick={onClose}>Tutup</Button>
        </div>
      }
    >
      {q.isPending || !a ? (
        <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-[13px]">
            <Row label="Peserta" value={`${a.userName} (${a.userEmail})`} />
            <Row label="Tes" value={a.assessmentTitle} />
            <Row label="Mulai" value={fmtDateTime(a.startedAt)} />
            <Row label="Dikirim" value={a.submittedAt ? fmtDateTime(a.submittedAt) : "Belum"} />
            <Row label="Skor mentah" value={`${num(a.totalScore)}/${num(a.maxScore)}`} />
            <Row label="Skor prediksi" value={a.totalScaledScore != null ? String(num(a.totalScaledScore)) : "—"} />
          </div>

          {sections.length > 0 && (
            <div>
              <h3 className="text-[12px] font-bold uppercase tracking-wide text-ink-subtle">Skor per bagian</h3>
              <ul className="mt-1.5 flex flex-wrap gap-x-6 gap-y-1">
                {sections.map(([name, value]) => (
                  <li key={name} className="text-[13px] text-ink-muted">
                    {name}: <strong className="text-ink">{num(value)}</strong>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <div>
            <h3 className="text-[12px] font-bold uppercase tracking-wide text-ink-subtle">
              Jejak pengawasan ({q.data!.events.length})
            </h3>
            <p className="mt-1 text-[11.5px] text-ink-subtle">
              Termasuk kejadian yang diabaikan (di bawah 2 detik) — jejak lengkap sengaja
              dipertahankan untuk peninjauan sengketa.
            </p>
            {q.data!.events.length === 0 ? (
              <p className="mt-2 text-[13px] text-ink-muted">Tidak ada kejadian.</p>
            ) : (
              <ul className="mt-2 flex max-h-[220px] flex-col gap-1 overflow-y-auto">
                {q.data!.events.map((e) => (
                  <li key={e.id} className="flex items-start gap-2.5 rounded-base border border-border px-3 py-1.5">
                    <span className={
                      "mt-0.5 shrink-0 rounded px-1.5 py-0.5 text-[10.5px] font-bold " +
                      (e.kind === "Ignored" ? "bg-surface-2 text-ink-subtle"
                        : e.kind === "AutoSubmitted" ? "bg-danger-soft text-danger"
                        : e.kind === "Warned" ? "bg-warning-soft text-warning"
                        : "bg-primary-soft text-primary")
                    }>
                      {e.kind}
                    </span>
                    <span className="min-w-0 flex-1 text-[12px] text-ink-muted">
                      <span className="block">{fmtDateTime(e.occurredAt)}</span>
                      <span className="block truncate font-mono text-[11px] text-ink-subtle">{e.clientMeta}</span>
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </Modal>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col">
      <span className="text-[11px] font-bold uppercase tracking-wide text-ink-subtle">{label}</span>
      <span className="text-ink">{value}</span>
    </div>
  );
}
