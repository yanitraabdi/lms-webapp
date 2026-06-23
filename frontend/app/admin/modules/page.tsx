"use client";

import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Badge, Button, Spinner, ErrorState, SearchIcon, type Tier } from "@/components/ui";
import { getAdminModules, num, setModulePublished, type AdminModule } from "@/lib/admin";
import { QuizEditor } from "@/components/admin/QuizEditor";

const TIER_BADGE: Record<number, Tier> = { 0: "free", 1: "beginner", 2: "intermediate", 3: "advanced" };

export default function AdminModulesPage() {
  const token = useAuth().accessToken;
  const qc = useQueryClient();
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [quizFor, setQuizFor] = useState<AdminModule | null>(null);

  useEffect(() => {
    const t = setTimeout(() => setSearch(searchInput.trim()), 300);
    return () => clearTimeout(t);
  }, [searchInput]);

  const modules = useQuery({
    queryKey: ["admin-modules", search],
    queryFn: () => getAdminModules(token!, search),
    enabled: !!token,
  });

  async function toggle(m: AdminModule) {
    if (!token) return;
    qc.setQueryData<AdminModule[]>(["admin-modules", search], (old) =>
      (old ?? []).map((x) => (x.id === m.id ? { ...x, published: !x.published } : x)));
    try {
      await setModulePublished(token, m.id, !m.published);
    } finally {
      qc.invalidateQueries({ queryKey: ["admin-modules", search] });
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs text-ink-subtle">Terbitkan/sembunyikan modul dengan cepat. Edit lengkap ada di Kurikulum.</p>
        <div className="relative">
          <SearchIcon size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-ink-subtle" />
          <input
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Cari modul…"
            className="w-[220px] rounded-sm border border-border bg-surface py-2 pl-8 pr-3 text-[13px] outline-none focus:border-primary"
          />
        </div>
      </div>

      <div className="overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
        {!token || modules.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : modules.isError ? (
          <div className="p-6"><ErrorState title="Gagal memuat modul" action={<Button variant="neutral" size="sm" onClick={() => modules.refetch()}>Muat ulang</Button>} /></div>
        ) : modules.data.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">Tidak ada modul.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[560px] border-collapse">
              <thead>
                <tr className="bg-surface-2 text-left text-xs font-bold text-ink-muted">
                  <th className="px-5 py-3">Modul</th>
                  <th className="px-3 py-3">Level</th>
                  <th className="px-3 py-3">Status</th>
                  <th className="px-3 py-3">Kuis</th>
                  <th className="px-5 py-3 text-right">Publikasi</th>
                </tr>
              </thead>
              <tbody className="text-[13.5px]">
                {modules.data.map((m) => (
                  <tr key={m.id} className="border-t border-border">
                    <td className="px-5 py-3.5 font-semibold">{m.title}</td>
                    <td className="px-3 py-3.5">
                      <Badge tier={TIER_BADGE[num(m.levelTier)]} className="px-2 py-0.5 text-[10.5px]">{m.levelName}</Badge>
                    </td>
                    <td className="px-3 py-3.5">
                      <Badge tone={m.published ? "success" : "neutral"} className="px-2.5 py-0.5 text-[11.5px]">{m.published ? "Terbit" : "Draf"}</Badge>
                    </td>
                    <td className="px-3 py-3.5">
                      <Button variant="neutral" size="sm" onClick={() => setQuizFor(m)}>Kelola</Button>
                    </td>
                    <td className="px-5 py-3.5">
                      <div className="flex justify-end">
                        <button
                          type="button"
                          role="switch"
                          aria-checked={m.published}
                          aria-label={m.published ? "Sembunyikan" : "Terbitkan"}
                          onClick={() => toggle(m)}
                          className={"relative h-[26px] w-[46px] rounded-full transition-colors " + (m.published ? "bg-primary" : "bg-[#C9D4E2]")}
                        >
                          <span className={"absolute top-[3px] h-5 w-5 rounded-full bg-white shadow transition-all " + (m.published ? "left-[23px]" : "left-[3px]")} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {quizFor && token && (
        <QuizEditor
          token={token}
          moduleId={quizFor.id}
          moduleTitle={quizFor.title}
          open={!!quizFor}
          onClose={() => setQuizFor(null)}
        />
      )}
    </div>
  );
}
