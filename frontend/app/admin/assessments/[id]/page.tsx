"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "@/components/auth/AuthProvider";
import { Button, Spinner, ErrorState, ChevronRightIcon } from "@/components/ui";
import { PageTitle } from "@/components/ui/Page";
import { SectionComposer } from "@/components/admin/SectionComposer";
import { getAssessment, setAssessmentQuestions, updateAssessment, num } from "@/lib/sessions";

export default function AssessmentComposerPage() {
  const token = useAuth().accessToken;
  const id = useParams<{ id: string }>().id;

  const q = useQuery({
    queryKey: ["admin-assessment", id],
    queryFn: () => getAssessment(token!, id),
    enabled: !!token,
    retry: false,
  });

  // sectionName -> selected question ids, seeded from what is already composed.
  const [bySection, setBySection] = useState<Record<string, string[]>>({});
  // sectionName -> whole-section recording, seeded from the stored config.
  const [audioBySection, setAudioBySection] = useState<Record<string, string | null>>({});
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!q.data) return;
    const seeded: Record<string, string[]> = {};
    const audio: Record<string, string | null> = {};
    for (const s of q.data.config.sections ?? []) {
      seeded[String(s.section)] = [];
      audio[String(s.section)] = s.audioRef ?? null;
    }
    setAudioBySection(audio);
    for (const question of q.data.questions) {
      (seeded[question.section] ??= []).push(question.id);
    }
    setBySection(seeded);
  }, [q.data]);

  if (!token || q.isPending) {
    return <div className="flex min-h-[300px] items-center justify-center"><Spinner size={24} /></div>;
  }
  if (q.isError) {
    return <ErrorState title="Test not found" message="Check the link and try again." />;
  }

  const sections = q.data.config.sections ?? [];
  const totalSelected = Object.values(bySection).reduce((n, ids) => n + ids.length, 0);
  const totalRequired = sections.reduce((n, s) => n + num(s.questions ?? 0), 0);

  async function save() {
    // Replacing the set on a test people have already sat is destructive enough to confirm.
    if (num(q.data!.attemptCount) > 0 &&
        !confirm("Learners have already taken this test. Replace the question set?")) return;

    setBusy(true); setError(null); setSaved(false);
    try {
      // The API replaces the whole set, so flatten in section order — the same order the
      // runtime uses when it filters questions per section.
      const ordered = sections.flatMap((s) => bySection[String(s.section)] ?? []);
      await setAssessmentQuestions(token!, id, ordered);
      // The whole config is replaced on PUT, so carry the stored one through and change
      // only audioRef — rebuilding it here would silently drop fields this page ignores.
      await updateAssessment(token!, id, {
        kind: q.data!.kind,
        title: q.data!.title,
        config: {
          ...q.data!.config,
          sections: sections.map((s) => ({ ...s, audioRef: audioBySection[String(s.section)] ?? null })),
        },
      });
      await q.refetch();
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Couldn't save the question set.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <Link href="/admin/programs" className="inline-flex items-center gap-1.5 text-[12.5px] font-bold text-ink-muted hover:text-ink">
            <ChevronRightIcon size={15} className="rotate-180" /> Program
          </Link>
          <PageTitle size="compact">{q.data.title}</PageTitle>
          <p className="text-[12.5px] text-ink-subtle">
            {totalSelected} of {totalRequired} questions composed
            {num(q.data.attemptCount) > 0 && ` · ${num(q.data.attemptCount)} attempts recorded`}
          </p>
        </div>
        <div className="flex items-center gap-3">
          {saved && <span className="text-[13px] font-bold text-success">Saved</span>}
          <Button onClick={save} loading={busy}>Save question set</Button>
        </div>
      </div>

      {num(q.data.attemptCount) > 0 && (
        <div className="rounded-base border border-[#F5D9A8] bg-warning-soft px-4 py-3 text-[13px] text-ink">
          Learners have already taken this test. Changing the question set does not alter results
          already recorded, but it applies to later attempts.
        </div>
      )}

      {error && <div className="rounded-base bg-danger-soft px-4 py-3 text-sm font-semibold text-danger">{error}</div>}

      {sections.length === 0 ? (
        <p className="py-8 text-center text-sm text-ink-muted">
          This test has no section configuration yet.
        </p>
      ) : (
        sections.map((s) => {
          const name = String(s.section);
          return (
            <SectionComposer
              key={name}
              token={token}
              section={name}
              required={num(s.questions ?? 0)}
              selected={bySection[name] ?? []}
              attached={q.data.questions}
              onChange={(ids) => { setSaved(false); setBySection((b) => ({ ...b, [name]: ids })); }}
              {...(name === "Listening"
                ? {
                    audioRef: audioBySection[name] ?? null,
                    onAudioChange: (key: string | null) => {
                      setSaved(false);
                      setAudioBySection((a) => ({ ...a, [name]: key }));
                    },
                  }
                : {})}
            />
          );
        })
      )}
    </div>
  );
}
