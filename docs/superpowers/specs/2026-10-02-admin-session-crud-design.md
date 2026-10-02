# Admin session management: edit, and a Bunny video picker — design

**Date:** 2026-10-02
**Status:** draft, awaiting review

---

## 1. Why

Videos are now in Bunny Stream and the API plays them, but an admin has no way to attach one to an
existing session.

The admin session list (`SessionManager`) offers **create, delete, reorder, attendance and the quiz
editor — but no edit.** `SessionForm` is wired only to "+ Tambah sesi". The update endpoint
(`PUT /api/admin/sessions/{id}`) and its client (`updateSession` in `lib/programs.ts`) both exist,
but nothing calls them, and no test exercises them either. So the "Bunny asset id" field is
reachable only when creating a brand-new session, and all six seeded sessions still carry the
placeholder `sample`.

Delete-and-recreate is not a workaround: it detaches the session's quiz, and `DeleteSessionAsync`
correctly refuses any session that already has learner progress (GR-7).

## 2. Scope

**In**

- Edit an existing session from the admin list.
- Pick a video from the Bunny library instead of pasting a GUID.
- Fix three traps in the existing update endpoint that an edit button would immediately trigger
  (§4).
- Two small fixes found on the way (§7).

**Out**

- **Uploading from the admin screen.** Bunny's own dashboard already handles uploads, resumable
  transfers and encoding progress, and is where the videos already are. Revisit if admins outside
  the team need to upload.
- **Deleting or renaming videos in Bunny** from our admin. The library is the source of truth for
  video files; we only reference them.
- Changing the linear lock, completion rules, or the quiz editor.

## 3. A correction this spec depends on

Earlier guidance claimed a session's "Durasi (menit)" must match the real video, or learners could
never reach the completion threshold. **That was wrong.** `VideoPlayer.tsx` computes progress as
`currentTime / duration` from the **video element's own duration**, and the server stores that
percentage (`SessionLearningService`). The stored `DurationSeconds` is display-only: it labels the
session in the learner list and the admin list, and nothing else.

So a wrong duration is a cosmetic bug, not a lock-breaking one. Auto-filling it from Bunny (§5) is
still worth doing, but for accuracy of the label, not for correctness of completion.

## 4. The update endpoint, as it stands, is unsafe to call

`UpdateSessionAsync` routes everything through `ApplySession`, which overwrites **every** field
from the request. Three of those overwrites are wrong for an edit:

| Field | Today | What goes wrong |
|---|---|---|
| `AssessmentId` | set from the request | A form that doesn't model the quiz sends `null` and **silently detaches it**. The session then completes on watching alone — the gate is gone and nothing says so. |
| `OrderIndex` | set from the request | A form holding a stale index either collides with `UNIQUE(program_id, order_index)` ("Urutan sesi bentrok") or quietly moves the session. |
| `Type` | set from the request | A Video can become a Live session after learners have watched it, leaving watch progress and completions attached to a session whose rules no longer match them. |

These are fixed **on the server**, not by careful form-building, because the endpoint is public API
and the next client to call it — or a hand-written request — would hit the same traps.

**Decision:** update keeps `AssessmentId`, `OrderIndex` and `Type` as stored and ignores them in the
request.

- `AssessmentId` already has its own endpoint (`PUT /api/admin/sessions/{id}/assessment`), used by
  the quiz editor.
- `OrderIndex` already has its own endpoint (`POST …/sessions/reorder`), used by the ↑ ↓ buttons.
- `Type` becomes immutable after creation. Changing a type means creating the new session and
  retiring the old one, which is rare and deliberately explicit.

`CreateSessionAsync` is unchanged: it still sets all three.

## 5. The Bunny video picker

### Backend

`GET /api/admin/video-library?search=&page=` — admin-only, rate-limited, returns:

```
{ items: [{ id, title, lengthSeconds, status, encodeProgress }], page, totalItems }
```

It calls Bunny's `GET https://video.bunnycdn.com/library/{libraryId}/videos` with the
`AccessKey` header, `itemsPerPage=50`, `orderBy=title`, passing `search` through.

- **The API key never reaches the browser.** It is a full-control credential for the library —
  it can delete videos — so it lives in server config only (GR-9), alongside the token key.
- `status` is mapped from Bunny's integers to names (`Finished`, `Processing`, `Error`, …) so the
  frontend never hard-codes Bunny's numbering.
- Under `Video:Provider = "dev"` the endpoint returns an empty list rather than calling Bunny, so
  local development and the test suite need no credentials.
- New port `IVideoLibrary` in Application, implemented by `BunnyVideoLibrary` in Infrastructure with
  a typed `HttpClient`. A dev implementation returns nothing.

### Config

New setting `Video:ApiKey`, passed through compose as `BUNNY_API_KEY`. **Optional at startup** —
playback works without it, and the picker shows "Daftar video tidak tersedia; isi ID secara manual"
when it is missing. Making it required would take the whole API down because a convenience feature
is unconfigured.

**Note:** `BUNNY_API_KEY` is not in `.env` yet. This is the **library's** API key (Stream → library
→ API), not the account API key and not the token authentication key.

### Frontend

In the session form, for Video sessions, the "Bunny asset id" text field becomes a picker:

- A searchable list of library videos by title, each showing its length and status.
- Choosing one fills the asset id **and** the duration.
- Videos not in `Finished` status are shown but disabled, labelled "Masih diproses (63%)" or
  "Gagal diproses". Attaching a video that is still encoding produces a playlist that 404s for
  learners.
- A "Masukkan ID manual" fallback keeps the plain text field, for when the API key is missing or
  Bunny is unreachable. Duration stays editable either way.

## 6. Edit in the admin list

- Each session row gets an **"Ubah"** button that opens `SessionForm` in edit mode, prefilled.
- `SessionForm` takes an optional `session` prop. Present → edit mode: title "Ubah sesi", type
  shown read-only (§4), saves through `updateSession`. Absent → today's create behaviour.
- After saving, the session list and the learner-facing program query are invalidated, as create
  does today.

## 7. Two smaller fixes

**`SessionForm` defaults the asset id to `"sample"`.** A new Video session saved without touching
that field silently gets a placeholder that under Bunny returns 403. The default becomes empty, and
the server refuses a Video session with no asset id — but only when **saving under the Bunny
provider**, so dev seeding and tests are unaffected.

**Rescheduling a live session never re-sends its reminder.** `LiveSessionReminder` claims a session
by stamping `ReminderSentAt` and never revisits it. When an edit changes `ScheduledAt`,
`ReminderSentAt` is reset to null so the H-1 sweep picks up the new time. Without this, learners
get a reminder for the old slot and none for the new one.

## 8. Not doing, and why

- **Validating on save that the asset id exists in Bunny and has finished encoding.** Attractive,
  but it makes every session save depend on Bunny being reachable — a Bunny outage would block
  editing a typo in a title. The picker already steers admins away from unfinished videos; the
  manual field is a deliberate escape hatch. Revisit if wrong ids turn up in practice.
- **Caching the library list.** Admin traffic is a handful of requests a day.

## 9. Testing

Backend, integration:

- Update preserves `AssessmentId` when the request sends `null` — **the quiz stays attached.**
  This is the regression that matters most.
- Update ignores `OrderIndex` and `Type` in the request.
- Update that changes `ScheduledAt` resets `ReminderSentAt`; one that doesn't, doesn't.
- Saving a Video session with no asset id is refused under the Bunny provider, allowed under dev.
- The video-library endpoint is admin-only (learner → 403, anonymous → 401).
- `BunnyVideoLibrary` maps Bunny's response — including each status integer — correctly, tested
  against a captured Bunny response body rather than a live call.
- The endpoint returns an empty list under the dev provider without touching the network.

Frontend: typecheck and lint. The picker and the edit flow need a manual pass by someone who can
sign in as admin, against the real library.

## 10. Rollout

1. Merge and deploy. Nothing changes for learners.
2. Add `BUNNY_API_KEY` to `.env` and restart the API.
3. In Admin → Program → Sesi, use "Ubah" on each of the six video sessions and pick its video.
4. Play each session once as a test learner.
