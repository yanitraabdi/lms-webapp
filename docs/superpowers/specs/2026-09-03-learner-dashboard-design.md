# Learner dashboard — design

**Date:** 2026-09-03
**Status:** approved, ready for planning
**Sub-project 2 of 6** — see §8.

---

## 1. Why

`/app/dashboard` is a narrow single column: a greeting, one programme card listing sessions, and a
certificates teaser. A learner cannot see how they scored, which TOEFL section is weakest, or
whether retrying a session test improved anything. The one number the product exists to move — the
predicted ITP score — appears nowhere on the page a learner opens most.

The data to answer all of that already exists. `attempts.section_scores` holds a real per-section
breakdown (`{"Reading":4,"Listening":2,"Structure":4}`), and certificates already carry the scaled
ITP total, the band and per-section scaled scores. It is simply not reachable from this page.

## 2. Scope

**In:** four dashboard blocks (progress, final exam result, session-test history, certificates),
one new learner endpoint for the only data with no route, and Recharts for the two charts.

**Out:**

- **Cohort comparison and percentiles** ("you beat X% of learners"). There is no cohort — zero
  final attempts exist — so it would be fabricated. Given golden rule 14 constrains what INVERTA
  may claim about TOEFL outcomes, an invented percentile is a claims risk, not just bad taste.
- **Predicted-score trajectory.** One attempt is allowed by default, so there is no series to
  project from and no model to project with.
- The other sub-projects in §8.

## 3. What already has an endpoint

Checked rather than assumed. Three of the four blocks need **no backend work**:

| Block | Data source | Exists? |
|---|---|---|
| 1. Progress | `GET /api/me/enrollments`, `GET /api/me/programs/{id}` | yes |
| 2. Final exam result | `GET /api/me/program-certificates` | yes |
| 3. Session-test history | — | **no** |
| 4. Certificates | `GET /api/me/program-certificates` | yes |

`ProgramCertificateDto` already returns `TotalScore` (the scaled ITP total, 310–677),
`PredictedBand`, and `SectionScores` as scaled per-section values. That is exactly what block 2
needs, so block 2 reads the certificate rather than the attempt.

**Why the certificate and not the attempt:** the attempt stores *raw* section scores; the scaled
conversion lives in `IScoreBandService` and is materialised onto the certificate. Serving scaled
scores from a new attempt endpoint would mean re-running that conversion in a second place — two
implementations of the number a learner is sold. Reading the certificate keeps one.

**Accepted consequence:** a learner who submitted the final exam but received no certificate — the
unmapped-score path, which `An_unmapped_score_fails_loudly_and_issues_no_certificate` covers — sees
block 2's empty state rather than a result. That path is already an admin-visible failure and the
score bands are currently complete (51 Listening / 41 Structure / 51 Reading, no gaps), so this is
a rare state that should be fixed at its source, not papered over on the dashboard.

## 4. The one new endpoint

```
GET /api/me/session-results  →  IReadOnlyList<SessionAttemptDto>
```

```csharp
public record SessionAttemptDto(
    Guid AttemptId,
    Guid SessionId,
    string SessionTitle,
    int OrderIndex,
    DateTimeOffset SubmittedAt,
    int Score,
    int MaxScore,
    bool Passed);
```

Submitted gating attempts for the calling learner, ordered by `SubmittedAt`. Nothing else needed —
the chart plots score against attempt, grouped by session.

**Three constraints, each load-bearing:**

1. **Scoped to `user.UserId()`, never a parameter.** There is no `userId` in the route or query, so
   there is no way to ask for someone else's attempts. Today the only listing of attempts is
   `/api/admin/attempts`, which is admin-only; this must not become a learner-accessible version
   of it.
2. **Scores only — no answers, no question text, no `Correct`.** Golden rule 11 applies even to a
   learner's own attempt, because gating tests allow unlimited retakes: handing back the key would
   let a learner pass the next attempt without learning anything, which defeats the gate.
3. **Gating attempts only.** The final attempt is served through the certificate (§3). Mixing kinds
   in one list would invite a client to render a 140-question exam beside a 15-question test as if
   they were comparable.

## 5. The four blocks

Replacing the current `max-w-3xl` single column with a wider two-column grid on desktop —
**block 1 full width across the top, blocks 2 and 3 side by side beneath it, block 4 full width
below** — stacking to a single column in blocks 1-2-3-4 order under 768px.

**1. Progress header.** Greeting, a ring showing sessions complete out of the programme's total,
and the continue-CTA pointing at the next unlocked session. Absorbs what the current programme card
does, in less vertical space.

**2. Final exam result.** Three horizontal bars — Listening, Structure, Reading — from the
certificate's scaled `SectionScores`, with the predicted total and band beside them, and a link to
the certificate.

**3. Session-test history.** Score per session test across attempts, from §4. This is the only
block with a genuine series, because gating tests are unlimited (`retakeCap: null` on the seeded
test) while the final allows one.

**4. Certificates.** Cards from `/api/me/program-certificates`: programme, predicted score, issue
date, verification code, and a link to the public verify page.

## 6. Empty states are the common case

This is the part that matters most, and the part a chart-first design gets wrong.

There are **zero final attempts** in the database today, and a new learner has none either. A
learner reaches the final exam only after completing every earlier session — so for most of their
time in the product, block 2 has nothing to draw.

| Block | When empty | Renders |
|---|---|---|
| 2. Final exam | No certificate yet | What the exam is, that it unlocks after every session is complete, how long it takes (115 minutes), and how many sessions remain |
| 3. Session tests | No submitted gating attempt | A line explaining that session tests unlock the next session, with the continue-CTA |
| 4. Certificates | None issued | Nothing at all — the block is omitted rather than showing an empty shelf |

Block 3 after exactly one attempt shows a single bar, not a one-point line. A line chart through
one point is noise pretending to be analysis.

## 7. Recharts

Added to `frontend/package.json`. This is a deliberate exception to the frontend's lean dependency
list — currently only `@tanstack/react-query`, `driver.js`, `hls.js`, `next`, `next-intl`, `react`,
`react-dom` — taken because it brings tooltips, responsive containers and accessible defaults that
a hand-rolled SVG would have to grow over time.

Colours come from the CSS custom properties, not hardcoded hex. Sub-project 1 retokens
`--color-primary` to `#7F00FF`; if the charts hardcode the current blue they will silently
contradict the new brand the moment that ships.

Both charts must be responsive and readable at 375px, since a learner checking their score is
likelier to be on a phone than a desktop.

## 8. Sub-projects

1. **Logo, favicon and brand colour** — spec written, `2026-09-03-logo-favicon-brand-colour-design.md`.
2. **Learner dashboard** — this document.
3. **Learner programme page as 30/70 master-detail.** Sessions left, the whole session inline
   right. `SessionView({token, sessionId})` is already self-contained, which makes it contained.
4. **Landing page density.** `/` redirects to `/program/toefl-preparation`, so the "homepage" is
   the programme page. Rebuilt from real content only — hero with price above the fold, the
   8-session syllabus, what is included, the ITP format, the 12 seeded FAQs.
5. **Final exam on a per-attempt UUID route**, 404 rather than 403 for an attempt that is not
   yours, and a lifetime ending when the sitting ends. Separate because it touches a paid exam.
6. **Admin-editable instructor, outcome stats and testimonials.** Three tables, three admin
   screens. Last, because those blocks render as nothing until content exists, so #4 ships without
   it.

## 9. Testing

**The endpoint gets integration tests for the two things that can actually hurt:**

- Another learner's attempts never appear in your results. Two learners each with a submitted
  gating attempt; each sees exactly their own.
- No answer key reaches the client. Assert the serialised response contains no `correct`,
  `answerKey` or `isCorrect` — the same shape as the existing
  `Student_assessment_never_exposes_the_answer_key`.
- An anonymous request is refused.
- A learner with no attempts gets an empty list and a 200, not a 404 — the empty state is a normal
  state, not an error.

**The charts are visual** and are verified by loading the page: at 375px and at desktop width, with
a learner who has attempts and one who has none.
