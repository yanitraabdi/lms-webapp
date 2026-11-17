"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { PageTitle } from "@/components/ui/Page";
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
      <PageTitle size="compact">Attempts</PageTitle>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-ink-muted">
          Test attempts. Review cases flagged by proctoring and reinstate them if the detection was wrong.
        </p>
        <label className="flex items-center gap-2 text-[13px] font-semibold">
          <input
            type="checkbox"
            checked={flaggedOnly}
            onChange={(e) => setFlaggedOnly(e.target.checked)}
            className="accent-primary"
          />
          Flagged only
        </label>
      </div>

      <div>
        {!token || attempts.isPending ? (
          <div className="flex min-h-[140px] items-center justify-center"><Spinner size={20} /></div>
        ) : attempts.isError ? (
          <div className="p-6">
            <ErrorState title="Couldn't load attempts"
              action={<Button variant="neutral" size="sm" onClick={() => attempts.refetch()}>Reload</Button>} />
          </div>
        ) : attempts.data.items.length === 0 ? (
          <p className="px-6 py-8 text-center text-sm text-ink-muted">
            {flaggedOnly ? "No flagged attempts." : "No attempts yet."}
          </p>
        ) : (
          <div className="overflow-x-auto">
            <table className="data-table min-w-[760px]">
              <thead>
                <tr>
                  <th>Learner</th>
                  <th>Test</th>
                  <th>Score</th>
                  <th>Prediction</th>
                  <th>Status</th>
                  <th className="num">Actions</th>
                </tr>
              </thead>
              <tbody className="text-[13.5px]">
                {attempts.data.items.map((a) => (
                  <tr key={a.id}>
                    <td>
                      <div className="font-semibold">{a.userName}</div>
                      <div className="text-[11.5px] text-ink-muted">{a.userEmail}</div>
                    </td>
                    <td>
                      <div>{a.assessmentTitle}</div>
                      <div className="text-[11.5px] text-ink-muted">
                        {a.kind === "Final" ? "Final assessment" : "Session test"} · {fmtDateTime(a.startedAt, "en-GB")}
                      </div>
                    </td>
                    <td>{num(a.totalScore)}/{num(a.maxScore)}</td>
                    <td>
                      {a.totalScaledScore != null
                        ? <span className="font-bold text-primary">{num(a.totalScaledScore)}</span>
                        : <span className="text-ink-muted">None</span>}
                    </td>
                    <td>
                      <div className="flex flex-wrap gap-1.5">
                        {a.submittedAt == null && (
                          <Badge tone="neutral" className="px-2 py-0.5 text-[10.5px]">In progress</Badge>
                        )}
                        {a.autoSubmitted && (
                          <Badge tone="warning" className="px-2 py-0.5 text-[10.5px]">Auto-submit</Badge>
                        )}
                        {a.proctorFlagged && (
                          <Badge tone="danger" className="px-2 py-0.5 text-[10.5px]">Flagged</Badge>
                        )}
                        {a.reinstated && (
                          <Badge tone="success" className="px-2 py-0.5 text-[10.5px]">Reinstated</Badge>
                        )}
                        {num(a.strikeCount) > 0 && (
                          <span className="text-[11px] text-ink-muted">{num(a.strikeCount)} strikes</span>
                        )}
                      </div>
                    </td>
                    <td>
                      <div className="flex justify-end">
                        <Button variant="neutral" size="sm" onClick={() => setOpenId(a.id)}>Review</Button>
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
    if (!confirm("Reinstate this attempt? The proctoring flag is cleared, but the event trail is kept.")) return;
    setBusy(true);
    try {
      await reinstateAttempt(token, attemptId);
      await q.refetch();
    } catch (e) {
      alert(e instanceof Error ? e.message : "Couldn't reinstate.");
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
      title="Review attempt"
      className="max-w-2xl"
      footer={
        <div className="flex w-full items-center justify-between gap-2">
          {a?.proctorFlagged ? (
            <Button variant="neutral" size="sm" onClick={reinstate} loading={busy}>Reinstate (clear flag)</Button>
          ) : <span />}
          <Button size="sm" onClick={onClose}>Close</Button>
        </div>
      }
    >
      {q.isPending || !a ? (
        <div className="flex min-h-[160px] items-center justify-center"><Spinner size={22} /></div>
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-x-6 gap-y-2 text-[13px]">
            <Row label="Learner" value={`${a.userName} (${a.userEmail})`} />
            <Row label="Test" value={a.assessmentTitle} />
            <Row label="Started" value={fmtDateTime(a.startedAt, "en-GB")} />
            <Row label="Submitted" value={a.submittedAt ? fmtDateTime(a.submittedAt, "en-GB") : "Not yet"} />
            <Row label="Raw score" value={`${num(a.totalScore)}/${num(a.maxScore)}`} />
            <Row label="Predicted score" value={a.totalScaledScore != null ? String(num(a.totalScaledScore)) : "None"} />
          </div>

          {sections.length > 0 && (
            <div>
              <h3 className="text-[13.5px] font-semibold text-ink-muted">Score by section</h3>
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
            <h3 className="text-[13.5px] font-semibold text-ink-muted">
              Proctoring trail ({q.data!.events.length})
            </h3>
            <p className="mt-1 text-[11.5px] text-ink-muted">
              Includes ignored events (under 2 seconds). The full trail is kept on purpose
              for dispute review.
            </p>
            {q.data!.events.length === 0 ? (
              <p className="mt-2 text-[13px] text-ink-muted">No events.</p>
            ) : (
              <ul className="mt-2 flex max-h-[220px] flex-col gap-1 overflow-y-auto">
                {q.data!.events.map((e) => (
                  <li key={e.id} className="flex items-start gap-2.5 rounded-base border border-border px-3 py-1.5">
                    <span className={
                      "mt-0.5 shrink-0 rounded px-1.5 py-0.5 text-[10.5px] font-bold " +
                      (e.kind === "Ignored" ? "bg-surface-2 text-ink-muted"
                        : e.kind === "AutoSubmitted" ? "bg-danger-soft text-danger"
                        : e.kind === "Warned" ? "bg-warning-soft text-warning"
                        : "bg-primary-soft text-primary")
                    }>
                      {e.kind}
                    </span>
                    <span className="min-w-0 flex-1 text-[12px] text-ink-muted">
                      <span className="block">{fmtDateTime(e.occurredAt, "en-GB")}</span>
                      <span className="block truncate font-mono text-[11px] text-ink-muted">{e.clientMeta}</span>
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
      <span className="text-[13.5px] font-semibold text-ink-muted">{label}</span>
      <span className="text-ink">{value}</span>
    </div>
  );
}
