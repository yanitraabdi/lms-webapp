"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Modal, Spinner, ErrorState, XIcon } from "@/components/ui";
import { AttendanceModal } from "@/components/admin/AttendanceModal";
import {
  listPrograms, createProgram, updateProgram,
  listSessions, createSession, deleteSession, reorderSessions,
  formatIdr, minutesLabel, num,
  type AdminProgram, type AdminSession, type UpsertSession,
} from "@/lib/programs";

export default function AdminProgramsPage() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();
  const [editing, setEditing] = useState<AdminProgram | null>(null);
  const [creating, setCreating] = useState(false);
  const [sessionsFor, setSessionsFor] = useState<AdminProgram | null>(null);

  const programs = useQuery({
    queryKey: ["admin-programs"],
    queryFn: () => listPrograms(token!),
    enabled: !!token,
  });

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs text-ink-subtle">
          Kelola program, sesi, dan urutannya. Urutan sesi menentukan penguncian bertahap.
        </p>
        <Button size="sm" onClick={() => setCreating(true)}>+ Program baru</Button>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {!token || programs.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : programs.isError ? (
          <div className="p-6">
            <ErrorState title="Gagal memuat program"
              action={<Button variant="neutral" size="sm" onClick={() => programs.refetch()}>Muat ulang</Button>} />
          </div>
        ) : programs.data.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">Belum ada program.</p>
        ) : (
          <table className="w-full min-w-[640px] border-collapse">
            <thead>
              <tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">
                <th className="px-5 py-3">Program</th>
                <th className="px-3 py-3">Harga</th>
                <th className="px-3 py-3">Sesi</th>
                <th className="px-3 py-3">Peserta</th>
                <th className="px-3 py-3">Status</th>
                <th className="px-5 py-3 text-right">Aksi</th>
              </tr>
            </thead>
            <tbody className="text-[13.5px]">
              {programs.data.map((p) => (
                <tr key={p.id} className="border-t border-border">
                  <td className="px-5 py-3.5">
                    <div className="font-semibold">{p.name}</div>
                    <div className="text-[11.5px] text-ink-subtle">/{p.slug}</div>
                  </td>
                  <td className="px-3 py-3.5">{formatIdr(p.priceIdr)}</td>
                  <td className="px-3 py-3.5">{num(p.sessionCount)}</td>
                  <td className="px-3 py-3.5">{num(p.enrollmentCount)}</td>
                  <td className="px-3 py-3.5">
                    <Badge tone={p.status === "Published" ? "success" : "neutral"} className="px-2.5 py-0.5 text-[11.5px]">
                      {p.status === "Published" ? "Terbit" : p.status === "Draft" ? "Draf" : "Arsip"}
                    </Badge>
                  </td>
                  <td className="px-5 py-3.5">
                    <div className="flex justify-end gap-2">
                      <Button variant="neutral" size="sm" onClick={() => setSessionsFor(p)}>Sesi</Button>
                      <Button variant="neutral" size="sm" onClick={() => setEditing(p)}>Edit</Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {(creating || editing) && token && (
        <ProgramForm
          token={token}
          program={editing}
          onClose={() => { setCreating(false); setEditing(null); qc.invalidateQueries({ queryKey: ["admin-programs"] }); }}
        />
      )}

      {sessionsFor && token && (
        <SessionManager
          token={token}
          program={sessionsFor}
          onClose={() => { setSessionsFor(null); qc.invalidateQueries({ queryKey: ["admin-programs"] }); }}
        />
      )}
    </div>
  );
}

function ProgramForm({ token, program, onClose }: { token: string; program: AdminProgram | null; onClose: () => void }) {
  const [name, setName] = useState(program?.name ?? "");
  const [slug, setSlug] = useState(program?.slug ?? "");
  const [summary, setSummary] = useState(program?.summary ?? "");
  const [description, setDescription] = useState(program?.description ?? "");
  const [price, setPrice] = useState(String(num(program?.priceIdr ?? 0)));
  const [published, setPublished] = useState(program?.status === "Published");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true); setError(null);
    try {
      const body = {
        name: name.trim(),
        slug: slug.trim() || null,
        description: description.trim(),
        summary: summary.trim() || null,
        priceIdr: Number(price) || 0,
        published,
      };
      if (program) await updateProgram(token, program.id, body);
      else await createProgram(token, body);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={program ? `Edit — ${program.name}` : "Program baru"}
      className="max-w-xl"
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
          <Button size="sm" onClick={save} loading={busy} disabled={!name.trim() || !description.trim()}>Simpan</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}
        <Field label="Nama program"><input value={name} onChange={(e) => setName(e.target.value)} className={inputCls} /></Field>
        <Field label="Slug (opsional)"><input value={slug} onChange={(e) => setSlug(e.target.value)} placeholder="otomatis dari nama" className={inputCls} /></Field>
        <Field label="Ringkasan"><input value={summary} onChange={(e) => setSummary(e.target.value)} className={inputCls} /></Field>
        <Field label="Deskripsi">
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4} className={inputCls} />
        </Field>
        <Field label="Harga (IDR)">
          <input type="number" min={0} value={price} onChange={(e) => setPrice(e.target.value)} className={inputCls} />
        </Field>
        <label className="flex items-center gap-2 text-[13px] font-semibold">
          <input type="checkbox" checked={published} onChange={(e) => setPublished(e.target.checked)} className="accent-primary" />
          Terbitkan (tampil di halaman publik)
        </label>
      </div>
    </Modal>
  );
}

function SessionManager({ token, program, onClose }: { token: string; program: AdminProgram; onClose: () => void }) {
  const qc = useQueryClient();
  const key = ["admin-sessions", program.id];
  const q = useQuery({ queryKey: key, queryFn: () => listSessions(token, program.id) });
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [attendanceFor, setAttendanceFor] = useState<string | null>(null);

  const sessions = [...(q.data ?? [])].sort((a, b) => num(a.orderIndex) - num(b.orderIndex));

  async function move(index: number, delta: number) {
    const next = [...sessions];
    const target = index + delta;
    if (target < 0 || target >= next.length) return;
    [next[index], next[target]] = [next[target], next[index]];
    setBusy(true);
    try {
      await reorderSessions(token, program.id, next.map((s) => s.id));
      await qc.invalidateQueries({ queryKey: key });
    } finally { setBusy(false); }
  }

  async function remove(s: AdminSession) {
    if (!confirm(`Hapus sesi "${s.title}"?`)) return;
    setBusy(true);
    try {
      await deleteSession(token, s.id);
      await qc.invalidateQueries({ queryKey: key });
    } catch (e) {
      alert(e instanceof Error ? e.message : "Gagal menghapus.");
    } finally { setBusy(false); }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title={`Sesi — ${program.name}`}
      className="max-w-2xl"
      footer={
        <div className="flex w-full items-center justify-between gap-2">
          <Button variant="neutral" size="sm" onClick={() => setAdding(true)}>+ Tambah sesi</Button>
          <Button size="sm" onClick={onClose}>Selesai</Button>
        </div>
      }
    >
      {q.isPending ? (
        <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
      ) : sessions.length === 0 ? (
        <p className="py-6 text-center text-sm text-ink-muted">Belum ada sesi. Tambahkan sesi pertama.</p>
      ) : (
        <ol className="flex flex-col gap-2">
          {sessions.map((s, i) => (
            <li key={s.id} className="flex items-center gap-3 rounded-base border border-border px-3.5 py-2.5">
              <span className="w-6 shrink-0 text-[12px] font-bold text-ink-subtle">{i + 1}</span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-[13.5px] font-semibold text-ink">{s.title}</span>
                <span className="block text-[11.5px] text-ink-subtle">
                  {s.type}{s.durationSeconds != null && ` · ${minutesLabel(s.durationSeconds)}`}
                </span>
              </span>
              <div className="flex shrink-0 items-center gap-1">
                {s.type === "Live" && (
                  <button
                    type="button"
                    onClick={() => setAttendanceFor(s.id)}
                    className="rounded px-2 py-1 text-[11.5px] font-bold text-primary hover:bg-primary-soft"
                  >
                    Kehadiran
                  </button>
                )}
                <button type="button" disabled={busy || i === 0} onClick={() => move(i, -1)}
                  aria-label="Naikkan" className="rounded px-1.5 py-1 text-ink-muted hover:bg-surface-2 disabled:opacity-30">↑</button>
                <button type="button" disabled={busy || i === sessions.length - 1} onClick={() => move(i, 1)}
                  aria-label="Turunkan" className="rounded px-1.5 py-1 text-ink-muted hover:bg-surface-2 disabled:opacity-30">↓</button>
                <button type="button" disabled={busy} onClick={() => remove(s)}
                  aria-label="Hapus" className="rounded px-1.5 py-1 text-ink-subtle hover:text-danger disabled:opacity-30">
                  <XIcon size={14} />
                </button>
              </div>
            </li>
          ))}
        </ol>
      )}

      {attendanceFor && (
        <AttendanceModal
          token={token}
          sessionId={attendanceFor}
          onClose={() => setAttendanceFor(null)}
        />
      )}

      {adding && (
        <SessionForm
          token={token}
          programId={program.id}
          nextOrder={sessions.length + 1}
          onClose={async () => { setAdding(false); await qc.invalidateQueries({ queryKey: key }); }}
        />
      )}
    </Modal>
  );
}

function SessionForm({
  token, programId, nextOrder, onClose,
}: { token: string; programId: string; nextOrder: number; onClose: () => void }) {
  const [type, setType] = useState<SessionKind>("Video");
  const [title, setTitle] = useState("");
  const [minutes, setMinutes] = useState("15");
  const [assetId, setAssetId] = useState("sample");
  const [scheduledAt, setScheduledAt] = useState("");
  const [joinUrl, setJoinUrl] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function save() {
    setBusy(true); setError(null);
    try {
      const body: UpsertSession = {
        type,
        title: title.trim(),
        description: null,
        orderIndex: nextOrder,
        providerAssetId: type === "Video" ? assetId.trim() || null : null,
        durationSeconds: type === "Video" ? (Number(minutes) || 0) * 60 : null,
        scheduledAt: type === "Live" && scheduledAt ? new Date(scheduledAt).toISOString() : null,
        liveMode: type === "Live" ? "Zoom" : null,
        joinUrl: type === "Live" ? joinUrl.trim() || null : null,
        location: null,
        assessmentId: null,
      };
      await createSession(token, programId, body);
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Gagal menyimpan sesi.");
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      onClose={onClose}
      title="Sesi baru"
      className="max-w-lg"
      footer={
        <div className="flex w-full justify-end gap-2">
          <Button variant="neutral" size="sm" onClick={onClose}>Batal</Button>
          <Button size="sm" onClick={save} loading={busy} disabled={!title.trim()}>Simpan</Button>
        </div>
      }
    >
      <div className="flex flex-col gap-3">
        {error && <div className="rounded-base bg-danger-soft px-3 py-2 text-[13px] font-semibold text-danger">{error}</div>}
        <Field label="Tipe sesi">
          <select value={type} onChange={(e) => setType(e.target.value as typeof type)} className={inputCls}>
            <option value="Video">Video + tes</option>
            <option value="Live">Sesi Live</option>
            <option value="FinalAssessment">Tes Akhir</option>
          </select>
        </Field>
        <Field label="Judul"><input value={title} onChange={(e) => setTitle(e.target.value)} className={inputCls} /></Field>

        {type === "Video" && (
          <>
            <Field label="Durasi (menit)">
              <input type="number" min={1} value={minutes} onChange={(e) => setMinutes(e.target.value)} className={inputCls} />
            </Field>
            <Field label="Bunny asset id"><input value={assetId} onChange={(e) => setAssetId(e.target.value)} className={inputCls} /></Field>
          </>
        )}

        {type === "Live" && (
          <>
            <Field label="Jadwal">
              <input type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} className={inputCls} />
            </Field>
            <Field label="Tautan Zoom"><input value={joinUrl} onChange={(e) => setJoinUrl(e.target.value)} className={inputCls} /></Field>
          </>
        )}

        {type === "FinalAssessment" && (
          <p className="rounded-base bg-surface-2 px-3 py-2 text-[12.5px] text-ink-muted">
            Soal dan konfigurasi tes akhir dikelola di bank soal (milestone berikutnya).
          </p>
        )}
      </div>
    </Modal>
  );
}

type SessionKind = "Video" | "Live" | "FinalAssessment";

const inputCls =
  "w-full rounded-sm border border-border bg-surface px-3 py-2 text-[13.5px] outline-none focus:border-primary";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-[12px] font-bold text-ink-muted">{label}</span>
      {children}
    </label>
  );
}
