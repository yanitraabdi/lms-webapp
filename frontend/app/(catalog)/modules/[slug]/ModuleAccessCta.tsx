"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { CheckIcon, LockIcon, PlayIcon } from "@/components/ui";
import { fetchModule, type ModuleAccess } from "@/lib/catalog";

interface Props {
  slug: string;
  moduleId: string;
  initialAccess: ModuleAccess;
  levelName: string;
  durationLabel: string;
  resourceCount: number;
}

export function ModuleAccessCta({
  slug,
  moduleId,
  initialAccess,
  levelName,
  durationLabel,
  resourceCount,
}: Props) {
  const { accessToken } = useAuth();

  // When signed in, re-resolve access with the bearer token (entitlement is
  // server-evaluated). Anonymous render uses the SSR access state.
  const { data } = useQuery({
    enabled: !!accessToken,
    queryKey: ["module", slug, true],
    queryFn: ({ signal }) => fetchModule(slug, { token: accessToken, signal }),
  });

  const access: ModuleAccess = accessToken && data ? data.access : initialAccess;
  const locked = access === "Locked";

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-[22px] shadow-sm">
      {/* access banner */}
      <div
        className={
          "flex items-start gap-3 rounded-base p-3.5 " +
          (locked ? "border border-[#BFDBFE] bg-[#DBEAFE]" : "border border-[#B6EDD0] bg-success-soft")
        }
      >
        <span className="inline-flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-lg bg-surface">
          {locked ? (
            <LockIcon size={18} className="text-info" />
          ) : (
            <CheckIcon size={18} strokeWidth={2.4} className="text-success" />
          )}
        </span>
        <div>
          <div className={"text-sm font-extrabold " + (locked ? "text-info" : "text-success")}>
            {locked ? "Modul terkunci" : access === "Preview" ? "Pratinjau gratis" : "Anda punya akses"}
          </div>
          <div className="text-[12.5px] leading-snug text-ink-muted">
            {locked
              ? "Tingkatkan paket untuk menonton & dapatkan sertifikat."
              : access === "Preview"
                ? "Tonton pratinjau modul ini secara gratis."
                : "Lanjutkan dari posisi terakhir Anda."}
          </div>
        </div>
      </div>

      {/* CTA */}
      {locked ? (
        <Link
          href="/pricing"
          className="inline-flex items-center justify-center gap-2 rounded-base bg-info px-4 py-3.5 text-[15px] font-bold text-white shadow-sm hover:opacity-95"
        >
          <LockIcon size={17} strokeWidth={2.2} />
          Tingkatkan untuk menonton
        </Link>
      ) : (
        <Link
          href={`/app/learn/${moduleId}`}
          className="inline-flex items-center justify-center gap-2 rounded-base bg-primary px-4 py-3.5 text-[15px] font-bold text-primary-ink shadow-sm hover:bg-primary-hover"
        >
          <PlayIcon size={17} />
          {access === "Preview" ? "Tonton pratinjau" : "Mulai menonton"}
        </Link>
      )}

      {locked && (
        <div className="text-center text-[12.5px] text-ink-muted">
          Termasuk dalam paket <strong className="text-info">{levelName}</strong> ke atas
        </div>
      )}

      {/* facts */}
      <div className="flex flex-col gap-2.5 border-t border-surface-2 pt-4">
        <Fact>Durasi {durationLabel}</Fact>
        <Fact>Caption Bahasa Indonesia</Fact>
        {resourceCount > 0 && <Fact>{resourceCount} materi unduhan tersedia</Fact>}
        <Fact>Berkontribusi ke sertifikat</Fact>
      </div>
    </div>
  );
}

function Fact({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-2.5 text-[13px] text-ink-muted">
      <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-ink-subtle" />
      {children}
    </div>
  );
}
