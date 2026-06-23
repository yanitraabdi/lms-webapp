"use client";

import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Modal, Spinner, Input } from "@/components/ui";
import { cn } from "@/lib/cn";
import * as A from "@/lib/admin";

type Tab = "levels" | "tracks" | "modules" | "categories" | "tags";
const TABS: { key: Tab; label: string }[] = [
  { key: "levels", label: "Level" },
  { key: "tracks", label: "Track" },
  { key: "modules", label: "Modul" },
  { key: "categories", label: "Kategori" },
  { key: "tags", label: "Tag" },
];

export default function CurriculumPage() {
  const token = useAuth().accessToken;
  const [tab, setTab] = useState<Tab>("levels");
  if (!token) return <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex gap-1 border-b border-border">
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            onClick={() => setTab(t.key)}
            className={cn("-mb-px border-b-2 px-3.5 py-2.5 text-[13.5px] font-bold",
              tab === t.key ? "border-primary text-primary" : "border-transparent text-ink-muted hover:text-ink")}
          >
            {t.label}
          </button>
        ))}
      </div>
      {tab === "levels" && <LevelsTab token={token} />}
      {tab === "tracks" && <TracksTab token={token} />}
      {tab === "modules" && <ModulesTab token={token} />}
      {tab === "categories" && <CategoriesTab token={token} />}
      {tab === "tags" && <TagsTab token={token} />}
    </div>
  );
}

function useErr() {
  const [error, setError] = useState<string | null>(null);
  const run = async (fn: () => Promise<unknown>, after?: () => void) => {
    setError(null);
    try { await fn(); after?.(); } catch (e) { setError(e instanceof Error ? e.message : "Gagal."); }
  };
  return { error, run };
}

function Toolbar({ title, onAdd }: { title: string; onAdd: () => void }) {
  return (
    <div className="flex items-center justify-between">
      <h2 className="text-sm font-extrabold">{title}</h2>
      <Button size="sm" onClick={onAdd}>+ Tambah</Button>
    </div>
  );
}

// ---------------- Levels ----------------
function LevelsTab({ token }: { token: string }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["adm-levels"], queryFn: () => A.listLevels(token) });
  const [editing, setEditing] = useState<A.Level | "new" | null>(null);
  const { error, run } = useErr();
  const refresh = () => qc.invalidateQueries({ queryKey: ["adm-levels"] });

  return (
    <div className="flex flex-col gap-3">
      <Toolbar title="Level" onAdd={() => setEditing("new")} />
      {error && <Err msg={error} />}
      <Table head={["Nama", "Slug", "Tier", "Urutan", "Status", "Track", ""]} loading={q.isPending}>
        {(q.data ?? []).map((l) => (
          <tr key={l.id} className="border-t border-border">
            <Td className="font-semibold">{l.name}</Td>
            <Td className="font-mono text-[12px] text-ink-muted">{l.slug}</Td>
            <Td>{A.num(l.requiredPlanTier)}</Td>
            <Td>{A.num(l.orderIndex)}</Td>
            <Td><Badge tone={l.published ? "success" : "neutral"} className="px-2 py-0.5 text-[11px]">{l.published ? "Terbit" : "Draf"}</Badge></Td>
            <Td>{A.num(l.trackCount)}</Td>
            <RowActions onEdit={() => setEditing(l)} onDelete={() => run(() => A.deleteLevel(token, l.id), refresh)} />
          </tr>
        ))}
      </Table>

      {editing && (
        <FormModal title={editing === "new" ? "Tambah level" : "Edit level"} onClose={() => setEditing(null)}
          onSave={(get) => run(async () => {
            const body: A.UpsertLevel = { name: get("name"), slug: get("slug") || undefined, requiredPlanTier: Number(get("tier") || 0), orderIndex: Number(get("order") || 0), published: get("published") === "on" };
            if (editing === "new") await A.createLevel(token, body); else await A.updateLevel(token, editing.id, body);
          }, () => { setEditing(null); refresh(); })}
          fields={[
            { name: "name", label: "Nama", value: editing === "new" ? "" : editing.name },
            { name: "slug", label: "Slug (opsional)", value: editing === "new" ? "" : editing.slug },
            { name: "tier", label: "Required tier (0–3)", value: editing === "new" ? "1" : String(A.num(editing.requiredPlanTier)), type: "number" },
            { name: "order", label: "Urutan", value: editing === "new" ? "0" : String(A.num(editing.orderIndex)), type: "number" },
            { name: "published", label: "Terbitkan", value: editing !== "new" && editing.published ? "on" : "", type: "checkbox" },
          ]} />
      )}
    </div>
  );
}

// ---------------- Tracks ----------------
function TracksTab({ token }: { token: string }) {
  const qc = useQueryClient();
  const levels = useQuery({ queryKey: ["adm-levels"], queryFn: () => A.listLevels(token) });
  const q = useQuery({ queryKey: ["adm-tracks"], queryFn: () => A.listTracks(token) });
  const [editing, setEditing] = useState<A.Track | "new" | null>(null);
  const { error, run } = useErr();
  const refresh = () => qc.invalidateQueries({ queryKey: ["adm-tracks"] });
  const levelName = (id: string) => levels.data?.find((l) => l.id === id)?.name ?? "—";

  return (
    <div className="flex flex-col gap-3">
      <Toolbar title="Track" onAdd={() => setEditing("new")} />
      {error && <Err msg={error} />}
      <Table head={["Nama", "Level", "Slug", "Urutan", "Modul", ""]} loading={q.isPending}>
        {(q.data ?? []).map((t) => (
          <tr key={t.id} className="border-t border-border">
            <Td className="font-semibold">{t.name}</Td>
            <Td>{levelName(t.levelId)}</Td>
            <Td className="font-mono text-[12px] text-ink-muted">{t.slug}</Td>
            <Td>{A.num(t.orderIndex)}</Td>
            <Td>{A.num(t.moduleCount)}</Td>
            <RowActions onEdit={() => setEditing(t)} onDelete={() => run(() => A.deleteTrack(token, t.id), refresh)} />
          </tr>
        ))}
      </Table>

      {editing && (
        <FormModal title={editing === "new" ? "Tambah track" : "Edit track"} onClose={() => setEditing(null)}
          onSave={(get) => run(async () => {
            const body: A.UpsertTrack = { levelId: get("levelId"), name: get("name"), slug: get("slug") || undefined, orderIndex: Number(get("order") || 0) };
            if (editing === "new") await A.createTrack(token, body); else await A.updateTrack(token, editing.id, body);
          }, () => { setEditing(null); refresh(); })}
          fields={[
            { name: "levelId", label: "Level", value: editing === "new" ? (levels.data?.[0]?.id ?? "") : editing.levelId, type: "select", options: (levels.data ?? []).map((l) => ({ value: l.id, label: l.name })) },
            { name: "name", label: "Nama", value: editing === "new" ? "" : editing.name },
            { name: "slug", label: "Slug (opsional)", value: editing === "new" ? "" : editing.slug },
            { name: "order", label: "Urutan", value: editing === "new" ? "0" : String(A.num(editing.orderIndex)), type: "number" },
          ]} />
      )}
    </div>
  );
}

// ---------------- Modules ----------------
function ModulesTab({ token }: { token: string }) {
  const qc = useQueryClient();
  const modules = useQuery({ queryKey: ["adm-modules-all"], queryFn: () => A.getAdminModules(token) });
  const [editing, setEditing] = useState<string | "new" | null>(null);
  const { error, run } = useErr();
  const refresh = () => qc.invalidateQueries({ queryKey: ["adm-modules-all"] });

  return (
    <div className="flex flex-col gap-3">
      <Toolbar title="Modul" onAdd={() => setEditing("new")} />
      {error && <Err msg={error} />}
      <Table head={["Judul", "Level", "Status", ""]} loading={modules.isPending}>
        {(modules.data ?? []).map((m) => (
          <tr key={m.id} className="border-t border-border">
            <Td className="font-semibold">{m.title}</Td>
            <Td>{m.levelName}</Td>
            <Td><Badge tone={m.published ? "success" : "neutral"} className="px-2 py-0.5 text-[11px]">{m.published ? "Terbit" : "Draf"}</Badge></Td>
            <RowActions onEdit={() => setEditing(m.id)} onDelete={() => run(() => A.deleteModule(token, m.id), refresh)} />
          </tr>
        ))}
      </Table>
      {editing && <ModuleEditor token={token} moduleId={editing === "new" ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); refresh(); }} />}
    </div>
  );
}

function ModuleEditor({ token, moduleId, onClose, onSaved }: { token: string; moduleId: string | null; onClose: () => void; onSaved: () => void }) {
  const tracks = useQuery({ queryKey: ["adm-tracks"], queryFn: () => A.listTracks(token) });
  const cats = useQuery({ queryKey: ["adm-cats"], queryFn: () => A.listCategories(token) });
  const tags = useQuery({ queryKey: ["adm-tags"], queryFn: () => A.listTags(token) });
  const existing = useQuery({ queryKey: ["adm-module", moduleId], queryFn: () => A.getModule(token, moduleId!), enabled: !!moduleId });
  const { error, run } = useErr();
  const [busy, setBusy] = useState(false);

  const [f, setF] = useState<A.ModuleInput | null>(null);
  useEffect(() => {
    if (moduleId && existing.data) {
      const m = existing.data;
      setF({ trackId: m.trackId, categoryId: m.categoryId, title: m.title, slug: m.slug, description: m.description, summary: m.summary, durationSeconds: A.num(m.durationSeconds), providerAssetId: m.providerAssetId, thumbnailUrl: m.thumbnailUrl, orderIndex: A.num(m.orderIndex), isPreview: m.isPreview, requiredPlanTier: A.num(m.requiredPlanTier), published: m.published, tagIds: m.tagIds });
    } else if (!moduleId && !f) {
      setF({ trackId: tracks.data?.[0]?.id ?? "", categoryId: null, title: "", slug: "", description: "", summary: "", durationSeconds: 300, providerAssetId: null, thumbnailUrl: null, orderIndex: 0, isPreview: false, requiredPlanTier: 1, published: false, tagIds: [] });
    }
  }, [moduleId, existing.data, tracks.data]); // eslint-disable-line react-hooks/exhaustive-deps

  const loading = (moduleId && existing.isPending) || tracks.isPending || !f;
  const set = (patch: Partial<A.ModuleInput>) => setF((s) => (s ? { ...s, ...patch } : s));

  async function save() {
    if (!f) return;
    setBusy(true);
    await run(async () => {
      if (moduleId) await A.updateModule(token, moduleId, f);
      else await A.createModule(token, f);
    }, onSaved);
    setBusy(false);
  }

  return (
    <Modal open onClose={onClose} title={moduleId ? "Edit modul" : "Tambah modul"} className="max-w-2xl"
      footer={<><Button variant="neutral" fullWidth onClick={onClose}>Batal</Button><Button fullWidth loading={busy} disabled={!f} onClick={save}>Simpan</Button></>}>
      {loading || !f ? (
        <div className="flex min-h-[200px] items-center justify-center"><Spinner size={20} /></div>
      ) : (
        <div className="flex max-h-[60vh] flex-col gap-3 overflow-y-auto pr-1">
          {error && <Err msg={error} />}
          <L label="Judul"><Input value={f.title} onChange={(e) => set({ title: e.target.value })} /></L>
          <L label="Slug (opsional)"><Input value={f.slug ?? ""} onChange={(e) => set({ slug: e.target.value })} /></L>
          <L label="Track">
            <Sel value={f.trackId} onChange={(v) => set({ trackId: v })} options={(tracks.data ?? []).map((t) => ({ value: t.id, label: t.name }))} />
          </L>
          <L label="Kategori (opsional)">
            <Sel value={f.categoryId ?? ""} onChange={(v) => set({ categoryId: v || null })} options={[{ value: "", label: "—" }, ...(cats.data ?? []).map((c) => ({ value: c.id, label: c.name }))]} />
          </L>
          <L label="Deskripsi"><textarea value={f.description} onChange={(e) => set({ description: e.target.value })} rows={3} className="rounded-sm border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-primary" /></L>
          <div className="grid grid-cols-2 gap-3">
            <L label="Durasi (detik)"><Input type="number" value={String(f.durationSeconds)} onChange={(e) => set({ durationSeconds: Number(e.target.value) })} /></L>
            <L label="Urutan"><Input type="number" value={String(f.orderIndex)} onChange={(e) => set({ orderIndex: Number(e.target.value) })} /></L>
            <L label="Required tier (0–3)"><Input type="number" value={String(f.requiredPlanTier)} onChange={(e) => set({ requiredPlanTier: Number(e.target.value) })} /></L>
            <L label="Bunny asset id (opsional)"><Input value={f.providerAssetId ?? ""} onChange={(e) => set({ providerAssetId: e.target.value || null })} /></L>
          </div>
          <div className="flex gap-5">
            <label className="flex items-center gap-2 text-[13px]"><input type="checkbox" checked={f.isPreview} onChange={(e) => set({ isPreview: e.target.checked })} /> Pratinjau gratis</label>
            <label className="flex items-center gap-2 text-[13px]"><input type="checkbox" checked={f.published} onChange={(e) => set({ published: e.target.checked })} /> Terbitkan</label>
          </div>
          <L label="Tag">
            <div className="flex flex-wrap gap-2">
              {(tags.data ?? []).map((t) => {
                const on = (f.tagIds ?? []).includes(t.id);
                return (
                  <button key={t.id} type="button" onClick={() => set({ tagIds: on ? (f.tagIds ?? []).filter((x) => x !== t.id) : [...(f.tagIds ?? []), t.id] })}
                    className={cn("rounded-full px-2.5 py-1 text-[12px] font-semibold", on ? "bg-primary text-primary-ink" : "bg-surface-2 text-ink-muted hover:bg-border")}>
                    #{t.name}
                  </button>
                );
              })}
            </div>
          </L>
          {moduleId && <ResourcesEditor token={token} moduleId={moduleId} />}
        </div>
      )}
    </Modal>
  );
}

function ResourcesEditor({ token, moduleId }: { token: string; moduleId: string }) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: ["adm-res", moduleId], queryFn: () => A.listResources(token, moduleId) });
  const [title, setTitle] = useState("");
  const [ref, setRef] = useState("");
  const [type, setType] = useState("Pdf");
  const refresh = () => qc.invalidateQueries({ queryKey: ["adm-res", moduleId] });

  return (
    <div className="flex flex-col gap-2 border-t border-surface-2 pt-3">
      <span className="text-[12.5px] font-bold">Materi</span>
      {(q.data ?? []).map((r) => (
        <div key={r.id} className="flex items-center justify-between gap-2 rounded-base border border-border px-3 py-2 text-[13px]">
          <span><span className="mr-2 rounded bg-surface-2 px-1.5 py-0.5 text-[10px] uppercase text-ink-muted">{r.type}</span>{r.title}</span>
          <button type="button" onClick={async () => { await A.deleteResource(token, r.id); refresh(); }} className="text-[12px] font-bold text-danger hover:underline">Hapus</button>
        </div>
      ))}
      <div className="flex flex-wrap items-center gap-2">
        <Sel value={type} onChange={setType} options={[{ value: "Pdf", label: "PDF" }, { value: "Link", label: "Link" }]} />
        <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Judul" className="flex-1 rounded-sm border border-border bg-surface px-2 py-1.5 text-[13px] outline-none focus:border-primary" />
        <input value={ref} onChange={(e) => setRef(e.target.value)} placeholder="URL / key" className="flex-1 rounded-sm border border-border bg-surface px-2 py-1.5 text-[13px] outline-none focus:border-primary" />
        <Button size="sm" disabled={!title || !ref} onClick={async () => { await A.createResource(token, moduleId, { type, ref, title }); setTitle(""); setRef(""); refresh(); }}>Tambah</Button>
      </div>
    </div>
  );
}

// ---------------- Categories / Tags ----------------
function NameSlugTab({ title, list, create, update, del, queryKey, showCount }: {
  title: string; queryKey: string; showCount?: boolean;
  list: () => Promise<{ id: string; name: string; slug: string; moduleCount?: number | string }[]>;
  create: (b: { name: string; slug?: string }) => Promise<unknown>;
  update: (id: string, b: { name: string; slug?: string }) => Promise<unknown>;
  del: (id: string) => Promise<unknown>;
}) {
  const qc = useQueryClient();
  const q = useQuery({ queryKey: [queryKey], queryFn: list });
  const [editing, setEditing] = useState<{ id: string; name: string; slug: string } | "new" | null>(null);
  const { error, run } = useErr();
  const refresh = () => qc.invalidateQueries({ queryKey: [queryKey] });

  return (
    <div className="flex flex-col gap-3">
      <Toolbar title={title} onAdd={() => setEditing("new")} />
      {error && <Err msg={error} />}
      <Table head={showCount ? ["Nama", "Slug", "Modul", ""] : ["Nama", "Slug", ""]} loading={q.isPending}>
        {(q.data ?? []).map((c) => (
          <tr key={c.id} className="border-t border-border">
            <Td className="font-semibold">{c.name}</Td>
            <Td className="font-mono text-[12px] text-ink-muted">{c.slug}</Td>
            {showCount && <Td>{A.num(c.moduleCount ?? 0)}</Td>}
            <RowActions onEdit={() => setEditing(c)} onDelete={() => run(() => del(c.id), refresh)} />
          </tr>
        ))}
      </Table>
      {editing && (
        <FormModal title={editing === "new" ? `Tambah ${title.toLowerCase()}` : `Edit ${title.toLowerCase()}`} onClose={() => setEditing(null)}
          onSave={(get) => run(async () => {
            const body = { name: get("name"), slug: get("slug") || undefined };
            if (editing === "new") await create(body); else await update(editing.id, body);
          }, () => { setEditing(null); refresh(); })}
          fields={[
            { name: "name", label: "Nama", value: editing === "new" ? "" : editing.name },
            { name: "slug", label: "Slug (opsional)", value: editing === "new" ? "" : editing.slug },
          ]} />
      )}
    </div>
  );
}

const CategoriesTab = ({ token }: { token: string }) =>
  <NameSlugTab title="Kategori" queryKey="adm-cats" showCount list={() => A.listCategories(token)} create={(b) => A.createCategory(token, b)} update={(id, b) => A.updateCategory(token, id, b)} del={(id) => A.deleteCategory(token, id)} />;
const TagsTab = ({ token }: { token: string }) =>
  <NameSlugTab title="Tag" queryKey="adm-tags" list={() => A.listTags(token)} create={(b) => A.createTag(token, b)} update={(id, b) => A.updateTag(token, id, b)} del={(id) => A.deleteTag(token, id)} />;

// ---------------- shared primitives ----------------
function Table({ head, loading, children }: { head: string[]; loading: boolean; children: React.ReactNode }) {
  return (
    <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
      {loading ? (
        <div className="flex min-h-[120px] items-center justify-center"><Spinner size={20} /></div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full min-w-[520px] border-collapse text-[13.5px]">
            <thead><tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">{head.map((h, i) => <th key={i} className="px-4 py-3">{h}</th>)}</tr></thead>
            <tbody>{children}</tbody>
          </table>
        </div>
      )}
    </div>
  );
}
const Td = ({ children, className }: { children: React.ReactNode; className?: string }) => <td className={cn("px-4 py-3", className)}>{children}</td>;
function RowActions({ onEdit, onDelete }: { onEdit: () => void; onDelete: () => void }) {
  return (
    <td className="px-4 py-3 text-right">
      <button type="button" onClick={onEdit} className="mr-3 text-[12.5px] font-bold text-primary hover:underline">Edit</button>
      <button type="button" onClick={() => { if (confirm("Hapus item ini?")) onDelete(); }} className="text-[12.5px] font-bold text-danger hover:underline">Hapus</button>
    </td>
  );
}
const Err = ({ msg }: { msg: string }) => <p className="rounded-base bg-danger-soft px-3 py-2 text-[12.5px] text-danger">{msg}</p>;
const L = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <div className="flex flex-col gap-1.5"><span className="text-[12.5px] font-semibold text-ink">{label}</span>{children}</div>
);
function Sel({ value, onChange, options }: { value: string; onChange: (v: string) => void; options: { value: string; label: string }[] }) {
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)} className="rounded-sm border border-border bg-surface px-3 py-2 text-[13px] outline-none focus:border-primary">
      {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
  );
}

interface Field { name: string; label: string; value: string; type?: string; options?: { value: string; label: string }[]; }
function FormModal({ title, fields, onClose, onSave }: { title: string; fields: Field[]; onClose: () => void; onSave: (get: (n: string) => string) => void }) {
  const [vals, setVals] = useState<Record<string, string>>(() => Object.fromEntries(fields.map((f) => [f.name, f.value])));
  const get = (n: string) => vals[n] ?? "";
  return (
    <Modal open onClose={onClose} title={title} className="max-w-md"
      footer={<><Button variant="neutral" fullWidth onClick={onClose}>Batal</Button><Button fullWidth onClick={() => onSave(get)}>Simpan</Button></>}>
      <div className="flex flex-col gap-3">
        {fields.map((f) => (
          <div key={f.name} className="flex flex-col gap-1.5">
            {f.type === "checkbox" ? (
              <label className="flex items-center gap-2 text-[13px]">
                <input type="checkbox" checked={get(f.name) === "on"} onChange={(e) => setVals((s) => ({ ...s, [f.name]: e.target.checked ? "on" : "" }))} /> {f.label}
              </label>
            ) : (
              <>
                <span className="text-[12.5px] font-semibold text-ink">{f.label}</span>
                {f.type === "select" ? (
                  <Sel value={get(f.name)} onChange={(v) => setVals((s) => ({ ...s, [f.name]: v }))} options={f.options ?? []} />
                ) : (
                  <Input type={f.type ?? "text"} value={get(f.name)} onChange={(e) => setVals((s) => ({ ...s, [f.name]: e.target.value }))} />
                )}
              </>
            )}
          </div>
        ))}
      </div>
    </Modal>
  );
}
