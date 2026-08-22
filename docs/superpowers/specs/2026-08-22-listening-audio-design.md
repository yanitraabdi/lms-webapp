# Listening audio: storage, signed delivery, authoring — design

**Date:** 2026-08-22
**Status:** approved, ready for planning
**Supersedes:** nothing. Closes a gap left open by A3 (admin content authoring).

---

## 1. Why

A3 gave operations a browser path to author a program end to end, gated by a readiness
check that refuses to publish an incomplete program. One hole remains, and it is the kind
the readiness check exists to prevent: **a final assessment composed entirely in the admin
UI has a Listening section that fails mid-exam.**

Three separate facts combine to produce that:

1. `frontend/app/admin/questions/page.tsx:158` hardcodes `audioRef: null` on every question
   it creates or updates. The backend contract (`UpsertQuestionRequest.AudioRef`) supports
   audio; the form never exposes it. No admin action can produce a Listening question with
   audio.
2. `IObjectStorage` (`backend/src/Application/Abstractions/Ports.cs:48`) is an **empty
   interface** — zero methods, no implementation, never registered in DI, never injected.
   Nothing stores an audio file.
3. `FinalAssessmentService.GetAudioUrlAsync` returns `$"/media/audio/{key}"` — an unsigned
   relative path. Nothing in the backend serves that path: no static-file middleware, no
   `wwwroot`, no volume mount. The comment above it claims "Signed, short-TTL, minted per
   play (GR-3)"; that describes infrastructure that was never built.

So a learner reaching a Listening question gets HTTP 400 (`"Soal ini tidak memiliki
audio."`) if `AudioRef` is null, and a dead URL if it is not. Readiness reports the program
ready either way.

The unsigned path is also a standing **GR-3 violation** ("video *and listening-audio* URLs
are minted server-side, short-TTL, after an access check"), independent of this feature.

### What already works, and is not touched

The server-side play limit is real and correct: `FinalAssessmentService.GetAudioUrlAsync`
counts plays per question in `AttemptState.AudioPlays` (a `jsonb` bag on `Attempt.State`),
increments and persists before returning, and enforces `AssessmentConfig.AudioPlayLimit`.
Attempt ownership, deadline, submitted-state, and active-section checks all run first. This
design reuses that machinery unchanged.

---

## 2. Scope

**In:** a real `IObjectStorage` with a local-disk implementation; HMAC-signed, short-TTL
audio delivery through the API; admin upload; support for both per-question clips and one
whole-section recording; a blocking readiness check.

**Out, deliberately:**

- **Video storage.** Every session currently resolves to the same public Mux test clip —
  `DevVideoProvider` mixes `assetId` into the HMAC payload but never uses it to select a
  file, and no `BunnyVideoProvider` exists. Video needs transcoding, HLS packaging and
  adaptive bitrate to be watchable; self-hosting it from a docker volume would be
  substantial work thrown away when Bunny lands. **Video stays in A1.**
- **Proctoring hardening.** Fullscreen enforcement, `beforeunload`, copy/paste blocking and
  the tab-close blind spot get their own spec.
- **R2.** The local implementation is the dev-sim; R2 is a later config swap, same as the
  other providers in this repo.
- **Deleting stored objects.** See §3.

---

## 3. Storage port

`IObjectStorage` gains exactly two methods:

```csharp
public interface IObjectStorage
{
    /// <summary>Stores the stream under <paramref name="key"/>, overwriting any existing object.</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Opens the object for reading, or null when the key does not exist.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct = default);
}
```

**No `Delete` and no `Exists`.** Replacing an upload orphans the previous file, which costs
disk and nothing else; a delete would risk removing a file another question still
references, since keys are free-form strings with no reference counting. `OpenReadAsync`
returning `null` covers the existence question at the only point that asks it. Add deletion
when there is a concrete reason to, not before.

### `LocalObjectStorage`

Writes beneath a configured root, following the dev-sim pattern the other providers use.

- Config: `Storage:Provider` (default `"local"`), `Storage:Root` (default `/data/media`).
- Keys are generated server-side as `audio/{uuidv7}{ext}` — the client never chooses a key.
- **Path traversal is refused.** A key must match `^[a-z0-9]+/[A-Za-z0-9._-]+$`; anything
  containing `..`, a leading `/`, a backslash, or a rooted path is rejected before it
  reaches the filesystem. This is a trust boundary — the serve route takes a key from a URL.
- DI registers it by config the same way `VideoOptionsFactory` is wired, so R2 later is a
  config swap rather than a code change.
- Docker: a named volume mounted at `/data/media` in both `docker-compose.yml` and
  `docker-compose.tunnel.yml`, so uploads survive a redeploy.

---

## 4. Signed delivery

### The route

`GET /api/media/{**key}?exp={unix}&sig={hex}` — **anonymous** (the signature is the
credential; an `<audio>` element cannot send an Authorization header), rate-limited under
the existing `playback` policy.

It verifies the HMAC before touching storage, rejects an expired `exp`, then streams via
`OpenReadAsync` with **range processing enabled** so the player can seek within a long
recording. An unknown key is a 404. A bad or missing signature is a 404, not a 403 — a
404 does not confirm that the key exists.

### Signing

Mirrors `DevVideoProvider`: HMAC-SHA256 over `$"{key}.{exp}"`, hex-encoded, using
`Media:SigningKey`. A dedicated key rather than reusing `Video:SigningKey`, so rotating one
does not invalidate the other. Comparison uses a fixed-time comparison, not `==`.

### TTL

`Media:UrlTtlMinutes`, **default 120**.

This is deliberately not the five minutes a click-to-play clip would want. A section-length
recording can run 35 minutes, and every seek re-requests the URL; a short TTL would break
playback partway through. Two hours comfortably exceeds the longest ITP section (Reading,
55 minutes) while still bounding how long a leaked URL stays useful. The URL grants access
to one audio object, nothing else.

### What this replaces

`GetAudioUrlAsync` stops returning `/media/audio/{key}` and returns a signed
`/api/media/{key}?exp=&sig=` instead. The misleading "Signed, short-TTL" comment becomes
true and stays. GR-3 is satisfied.

---

## 5. Two audio shapes

Content may arrive either as one clip per Listening question or as a single recording for
the whole section — the business has not settled which, and both must work.

### Where each lives

| Shape | Stored on | Migration |
|---|---|---|
| Per question | `Question.AudioRef` (exists today, `text` nullable) | none |
| Per section | `AssessmentSectionConfig.AudioRef` (new field) | **none** — the config is a `jsonb` bag |

The section case costs no schema change, which is why supporting both now is cheap.

### Resolution order

`GetAudioUrlAsync` resolves, in order:

1. The question's own `AudioRef`, when set.
2. Otherwise the `AudioRef` on the config entry for the question's section.
3. Otherwise the existing 400 (`"Soal ini tidak memiliki audio."`), unchanged.

A per-question clip therefore overrides a section recording, which is the useful precedence:
it lets one item be re-recorded without replacing the whole section file.

### Play counting

`AttemptState.AudioPlays` is a `Dictionary<string,int>`; only the key changes.

- Question audio counts under the question id, exactly as today.
- Section audio counts under the section name, so replaying a section recording from three
  different questions consumes **one shared allowance** rather than one per question.

That is the intended behaviour for a single recording: the limit models "how many times may
this learner hear this audio", and there is one audio.

`GetStateAsync` already surfaces `audioPlaysLeft` keyed by question id for the player. For
section audio it reports the shared section allowance against every question in that
section, so the UI shows a consistent count wherever the learner is.

### The student DTO must reflect the fallback

The player renders its audio button on `q.hasAudio`
(`frontend/app/app/assessment/[id]/page.tsx:275`). That flag is currently derived from the
question's own audio alone, so **section audio would be invisible to the learner** — the
audio would exist, be served, and never be offered. `hasAudio` must therefore be true when
the question resolves to audio by *either* route, computed server-side with the same
precedence as §5.

This stays inside GR-10: `hasAudio` is a boolean about availability. Neither the storage key
nor the signed URL appears in the student DTO — the URL is minted only by the existing
per-play endpoint, after its access, deadline and limit checks.

---

## 6. Admin upload

`POST /api/admin/media/audio` — admin-only, `multipart/form-data`, one file, returns
`{ "key": "audio/…" }`.

**Validation, all server-side:**

- Content type in `audio/mpeg`, `audio/mp4`, `audio/x-m4a`, `audio/wav`, `audio/ogg`.
- Size ≤ **100 MB** (a 35-minute MP3 is roughly 30 MB), enforced by request-size limit as
  well as by check, so an oversize body is rejected before it is buffered.
- Extension derived from the content type, never from the client-supplied filename.

Rejections are RFC-7807 problem details with a message naming the actual limit, in Bahasa
Indonesia, so the admin can act on it.

### Where it appears

Per the agreed UX, upload sits where the thing it belongs to is already edited:

- **Question form** (`app/admin/questions/page.tsx`) — an upload control shown when the
  section is `Listening`, replacing the hardcoded `audioRef: null`. Shows the current key
  when one is set, and allows clearing it.
- **Final-assessment composer** (`app/admin/assessments/[id]/page.tsx`) — an upload control
  on the Listening section panel, writing `AudioRef` into that section's config.

Uploading replaces whatever that question or section currently points at. No separate media
library: the same clip being reused across questions is not a pattern this content has, and
a library would add a picker, a listing, and a delete-safety question for no present gain.

---

## 7. Readiness check

A new **blocking** check, `listening_audio_present`, alongside the five that exist.

**Passes when** the program's final assessment has no Listening section, **or** that
section's config carries an `AudioRef`, **or** every Listening question attached to the
assessment has a non-null `AudioRef`.

**Fails otherwise**, with a detail naming how many questions are missing audio, so the
admin knows whether to upload a section recording or fill in the gaps.

Mixed coverage — a section recording plus some per-question overrides — passes on the
section branch, which is correct: every question can be served.

This is what makes the A3 promise true. A program whose Listening section would 400 mid-exam
can no longer be published.

---

## 8. Testing

Backend, in the existing suite (Testcontainers PostgreSQL, currently 206 tests):

**Storage**
- `PutAsync` then `OpenReadAsync` round-trips the bytes.
- `OpenReadAsync` returns null for an unknown key.
- A traversal key (`../../etc/passwd`, a rooted path, a backslash) is refused and nothing is
  written or read outside the root.

**Signing and delivery**
- A valid signature streams the object.
- An expired `exp` is refused.
- A tampered `sig`, a tampered `key`, and a signature minted with a different key are all
  refused.
- An unknown key returns 404, and so does a bad signature — the two are indistinguishable.
- A range request returns 206 with the requested slice.

**Resolution and play limits**
- A question with its own `AudioRef` resolves to that object.
- A question without one falls back to its section's `AudioRef`.
- A question with neither still returns the existing 400.
- Section audio shares one allowance: playing from two different questions in the same
  section consumes two of the shared limit, not one each.
- The limit still auto-refuses past the cap, as today.

**Upload**
- An admin uploads a valid MP3 and gets a key that then serves.
- Wrong content type is refused.
- Oversize is refused.
- A non-admin is refused.

**Readiness**
- A final assessment whose Listening questions have no audio fails `listening_audio_present`
  and blocks publish, with the count in the detail.
- Section-level audio alone satisfies it.
- Per-question audio on every Listening question satisfies it.
- An assessment with no Listening section is unaffected.

Frontend has no test runner and one is not being added; the upload controls are covered by
`npm run build` plus the browser walkthrough below.

### Verification

Beyond the suite: upload a real MP3 through the admin UI, publish a program whose Listening
section it covers, and play it in a learner's sitting in a browser — including a seek, to
prove range requests work through the signed URL. A dead audio path is precisely the failure
that unit tests did not catch last time.

---

## 9. Risks

- **Orphaned files accumulate.** Accepted; see §3. Disk is cheap and a delete is unsafe
  without reference counting.
- **A leaked signed URL is replayable for up to two hours.** Bounded by design: it grants one
  audio object and nothing else, and the play limit is enforced when the URL is *minted*, not
  when it is fetched — so a leaked URL cannot burn a learner's remaining plays, but neither
  does re-fetching it consume one. This is the same trade the video ticket already makes.
- **The volume must be mounted in the tunnel compose file too**, or uploads vanish on
  redeploy in the deployment that actually matters.
- **`Media:SigningKey` must not ship with its default value.** `VideoOptions.SigningKey`
  currently defaults to `"dev-video-signing-key-change-me"` and is overridden nowhere; do not
  repeat that. The key belongs in server-side config, never the repo.
