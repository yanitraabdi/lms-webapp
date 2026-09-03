# Learner programme page as 30/70 master-detail — design

**Date:** 2026-09-03
**Status:** approved, ready for planning
**Sub-project 3 of 6** — see §10.

---

## 1. Why

Working through the programme costs a round trip per session. `/app/program/{id}` is a single
`max-w-3xl` column listing sessions; each row's button navigates to `/app/session/{id}`, and the
only way to the next session is back to the list. Eight sessions means eight departures and eight
returns, and the learner loses sight of where they are in the programme every time.

The fix is a master-detail: the session list stays on screen while the session plays beside it.

## 2. Scope

**In:** a 30/70 split on desktop, the whole session rendered inline, the selected session in the
URL, and a phone layout that keeps today's behaviour.

**Out:**

- **The final assessment.** It keeps its own page — see §7. That is not a compromise; it is what
  the codebase already does, and what a 115-minute proctored exam needs.
- **Changing what a session *is*.** No change to playback, progress, gating tests, live details,
  scoring or the linear lock. This is a layout change; if a test that guards those has to change,
  something has gone wrong.
- **The exam's per-attempt UUID route**, which is sub-project 5.

## 3. The split

Above `md` (768px):

```
┌──────────────────────────────────────────────┐
│ AppHeader                                    │
├──────────────┬───────────────────────────────┤
│ programme    │                               │
│ title +      │   the selected session,       │
│ progress     │   in full                     │
│              │                               │
│ session list │   video / live details        │
│ 30%          │   + gating test               │
│              │   70%                         │
└──────────────┴───────────────────────────────┘
```

The left column keeps the programme heading, the progress bar and the ordered session list that
exists today, with the selected row marked. The right column renders the session.

The left column scrolls independently and sticks, so a learner on session 7 of 8 does not lose the
list while a 20-minute video plays.

## 4. `SessionView` becomes a shared component

`app/app/session/[id]/page.tsx` already contains `SessionView({ token, sessionId })`, which fetches
its own context and branches into video, live, final and gating-test sections. It moves to
`components/learn/SessionView.tsx` and gains two props:

```tsx
<SessionView
  token={string}
  sessionId={string}
  embedded?={boolean}                 // default false
  onSessionChanged?={() => void}      // default undefined
/>
```

**`embedded`** suppresses the parts the programme page already provides: the standalone route wraps
itself in `min-h-screen`, its own header with a back-link to the programme, and a `max-w-4xl`
container. Embedded, it renders the session body only.

**`onSessionChanged`** is called when the session's state changes in a way the *list* must reflect
— passing a gating test, or watch progress crossing the completion threshold. Without it the left
column would still show session 3 as locked after the learner just unlocked it, which reads as a
bug even though a refresh fixes it. See §8.

**The standalone route stays.** `/app/session/{id}` is linked from the dashboard, from the
assessment page's back-link, and from any bookmark a learner already has. It keeps working by
rendering the same component unembedded. Two entry points, one implementation.

## 5. The selected session lives in the URL

`/app/program/{id}?session={sessionId}`.

Three things follow, and the third is why it is not optional:

- Refreshing keeps your place.
- The browser back button steps between sessions rather than leaving the programme.
- A learner can bookmark or share where they are.

Without it, a mid-programme reload silently returns a learner to session one. That is the kind of
thing reported as lost progress even though nothing was lost.

**No `session` parameter** selects the next unlocked session — the one the learner would continue —
falling back to the first session if the programme is complete. `StudentProgramDto` already returns
`NextSessionId`, so this needs no backend change; the whole sub-project is frontend-only.

**An unknown or locked `sessionId`** falls back the same way rather than erroring. A stale bookmark
should land somewhere sensible, and the server refuses locked content regardless (`canAccess`), so
the client does not need to be the gate.

## 6. Below 768px: list, then navigate

The phone layout is the list alone. Tapping a session opens `/app/session/{id}` — the standalone
route, unchanged.

This is deliberately the smallest thing that works. An accordion or an in-place pane swap would
each be a second layout to build, style and keep correct, to reach a screen where a video player
plus a 15-question test has no room to be beside anything. The 30/70 is a desktop enhancement,
and on a phone the existing behaviour is already right.

## 7. The final assessment keeps its own page

A `FinalAssessment` session in the list navigates to `/app/assessment/{sessionId}`. It never
renders in the right pane.

This is not new: `nextHref` in the dashboard already routes final-assessment sessions there and
everything else to the session page. The reasons hold more strongly inside a split layout —

- It is a **115-minute proctored sitting** with a server-authoritative timer and soft proctoring
  that auto-submits on two strikes. A session list beside it is an invitation to click away
  mid-exam, and every click away is a proctor event.
- It is **one attempt by default**. A layout that makes leaving easy is a layout that costs
  somebody their attempt.

## 8. Keeping the list honest

The one genuinely new problem this layout creates.

Today, passing a gating test unlocks the next session and the learner then *navigates back*, which
refetches the list. Inline, nothing navigates, so the list would keep showing the next session as
locked until a manual refresh.

`SessionView` calls `onSessionChanged` after any completion-affecting event — a passed gating test,
or watch progress crossing the threshold — and the programme page invalidates its
`["student-program", id]` query. The completion rules themselves are untouched; the server still
decides, and the client only re-asks.

## 9. Testing

The frontend has no test runner, and this change adds no server behaviour — so there is nothing
here that a unit test could honestly assert that the existing 400 backend tests do not already
cover.

What must be verified, by loading the deployed app:

- Selecting each session type in turn renders the right pane correctly: video plays, live details
  appear, the gating test is present and gated on watch progress.
- Passing a gating test unlocks the next session **in the left list without a manual refresh** —
  the §8 problem.
- A locked session cannot be selected, and the server still refuses it if forced.
- `?session=` survives a reload; the back button steps between sessions; an unknown id falls back
  rather than erroring.
- At 375px the list shows alone and tapping navigates to the standalone page.
- The final assessment navigates away rather than embedding.

**One risk worth naming rather than testing around:** `SessionView` moving files is the kind of
change that silently drops a prop. The standalone route and the embedded pane must both be opened
after the move, because a build passing proves only that it compiles.

## 10. Sub-projects

1. **Logo, favicon and brand colour** — done, merged.
2. **Learner dashboard** — spec written, awaiting review.
3. **Programme page 30/70** — this document.
4. **Landing page density.** `/` redirects to `/program/toefl-preparation`, so the "homepage" is
   the programme page. Rebuilt from real content only.
5. **Final exam on a per-attempt UUID route**, 404 rather than 403 for an attempt that is not
   yours, and a lifetime ending when the sitting ends.
6. **Admin-editable instructor, outcome stats and testimonials.**
