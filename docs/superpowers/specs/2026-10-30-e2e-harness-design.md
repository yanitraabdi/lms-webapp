# Browser test harness (Playwright + axe) — design

Drafted 2026-10-30, queued by the user after the course-book revamp ("queue the test plan after").

## Problem
- The frontend has no automated tests. CI runs only `lint` and `build`.
- The course-book browser check found three layout bugs that lint, tsc and review missed:
  - a squeezed table-of-contents title;
  - facts crammed into four columns;
  - a 451px-wide header on a 375px phone.
- The CLAUDE.md critical E2E path (enrol → watch → pass gating test → final → score → certificate) has never been automated.
- Several golden rules are only checked by backend unit tests, never through the real browser and proxy:
  - GR-2: enrolment only via a verified webhook;
  - GR-8: the linear lock;
  - GR-11: the answer key never served;
  - GR-14: "prediction, not official".

## Goal
Add a Playwright suite under `frontend/e2e/` that runs locally and in CI against the real stack: `docker compose` with Postgres, the .NET API and the Next.js production build.

1. **Smoke + accessibility:** every live INVERTA page loads, has one `h1`, logs no console errors, and has zero axe violations of impact `serious` or `critical`. At 375px, no page scrolls horizontally.
2. **Journey:** one test drives a fresh learner from sign-up to a verified certificate, and asserts the golden rules on the way.

## Decisions
- **Isolated stack.**
  - `docker-compose.e2e.yml` is an override on `docker-compose.yml`, run as compose project `inverta-e2e` with its own volume.
  - A committed, test-only `e2e.env` passed with `--env-file`, so Compose never loads the deployed root `.env` (which may switch email to real SMTP). `Email__Provider` is pinned to `dev`.
  - Fresh volume on every `e2e:up`, so the seeded live session's date stays in the future.
  - Frontend on host port 3100, API on 8180, Postgres on 5532, so it never touches the deployed `academy` stack or its data.
  - `RateLimits__PermitLimit=1000`, because all browser traffic reaches the API from one proxy IP.
  - Explicit `DevAdmin__Email`/`DevAdmin__Password` test values. These are test-only credentials, like the CI Postgres password.
- **Email verification** sets `users.email_verified` via `docker compose exec postgres psql`, the precedent in `scripts/seed-test-learner.sh`. The dev email sender only logs the token, and the database stores only its hash.
- **Enrolment goes through the browser:** "Daftar sekarang", then dev-pay "Bayar sekarang (berhasil)". This exercises the real signed-webhook path (GR-2). Nothing writes enrolment rows directly.
- **Video watching is posted via the API** (`PUT …/progress` with `percent: 100`). The dev video is an external Mux HLS stream, so CI must not depend on the internet. The UI path is covered by the gating assertion.
- **Answer keys come from the database in the test harness only** (`questions.correct`). The API is never asked for them, and the journey asserts the learner-facing assessment response contains no `correct` field (GR-11).
- **Session 1's gating test runs through the UI.** Sessions 2–6 are completed through the API to keep the run short.
- **The live session (7)** is marked attended through the admin API, as the admin role would.
- **The final assessment runs through the UI.** Answers are partial, and the learner advances through all three sections and finishes. The test opens no second tab during the sitting (proctoring counts window blur).
- **axe policy:** `serious` and `critical` violations fail. Known violations found on the first run are fixed in the harness PR. A rule may only be disabled by a named entry in one `e2e/support/axe.ts` allow-list, with a reason.
- **Selectors** are roles and visible Indonesian text. No `data-testid` is added unless a role or text selector is impossible.
- **Dormant routes are out of scope:** `/catalog*`, `/modules/*`, `/pricing`, `/app/learn/*`, `/for-business`, `/style-guide`, `/feedback`.
- **No tsc hook.** CI already type-checks via `next build`. A local hook can come later.

## Out of scope
- Visual-regression screenshots.
- Cross-browser runs (Chromium only).
- Load testing.
- Retake and proctoring-strike E2E (backend tests already cover these).
