# Upload video to Bunny from the admin screen — design

**Date:** 2026-10-05 · **Status:** approved in brainstorming, awaiting spec review
**Business feedback (2026-10-04):** "Upload video ke Bunny page nya udah ada?" It doesn't exist yet: videos are uploaded in the Bunny dashboard, and the admin only picks them.

## 1. Decisions (PO, 2026-10-05)

| # | Decision |
|---|---|
| D1 | Upload lives **inside the video picker** (`VideoPicker`), not on a separate library page. |
| D2 | The file goes **browser → Bunny directly** over TUS (resumable). It never passes through our API or the Cloudflare tunnel, whose free plan refuses request bodies over 100 MB. |
| D3 | The Bunny library API key stays server-side (GR-9). The browser only receives a short-lived, video-scoped upload signature. |
| D4 | A finished upload is **not** auto-selected. It becomes pickable when Bunny has finished encoding it, like every other video. |
| D5 | Abandoned uploads stay in Bunny, shown as "Unggahan tidak selesai" and not pickable. Delete and rename belong to a later "Pustaka Video" page. |

## 2. Bunny facts this relies on

- **Create a video:** `POST https://video.bunnycdn.com/library/{libraryId}/videos` with the header `AccessKey: {library API key}` and the body `{"title": "..."}`. It returns the video object, whose `guid` is the video id.
- **TUS upload endpoint:** `https://video.bunnycdn.com/tusupload`. It needs these request headers:
  - `AuthorizationSignature`: the lowercase hex SHA-256 of `libraryId + apiKey + expirationTime + videoId`, concatenated as strings;
  - `AuthorizationExpire`: `expirationTime` in Unix seconds;
  - `VideoId`;
  - `LibraryId`.
- **Upload metadata:** `filetype` (the file's MIME type) and `title`.
- **Video status:** `Created` (0) means no upload has finished yet. The other statuses are already handled by `BunnyVideoLibrary.StatusName`.

## 3. Server

- **Port.** `IVideoLibrary` gains two methods:
  - `CreateUploadAsync(string title, CancellationToken)`, which returns an `UploadTicketDto` or a refusal;
  - `RenewUploadAsync(string videoId, CancellationToken)`.

  The existing `BunnyVideoLibrary` implements both. `UnavailableVideoLibrary`, used by the dev provider, refuses both.
- **DTO.** `UploadTicketDto(string VideoId, string LibraryId, long ExpiresAt, string Signature, string Endpoint)`. `Endpoint` is `https://video.bunnycdn.com/tusupload`. It never contains the API key.
- **Signing.** A single pure static function: `BunnyUploadSigner.Sign(libraryId, apiKey, expiresAt, videoId)`. Tickets are valid for **2 hours** from issue.
- **Routes.** Both are admin-only, rate-limited with the existing "media" policy, and live next to `GET /api/admin/video-library`.
  - **`POST /api/admin/video-library/uploads`** with body `{ "title": string }`:
    - 200 returns `UploadTicketDto`;
    - 400 `Judul video wajib diisi (maksimal 200 karakter).` when the trimmed title is empty or over 200 characters;
    - 409 `Unggah video hanya tersedia saat Bunny aktif.` under the dev provider;
    - 502 when Bunny can't be reached or refuses. The message uses the same Indonesian wording style as `BunnyVideoLibrary`'s `Unavailable` reasons, e.g. `Bunny menolak kunci API. Periksa BUNNY_API_KEY.` or `Bunny tidak dapat dihubungi. Coba lagi.`
  - **`POST /api/admin/video-library/uploads/{videoId}/ticket`**: a fresh ticket for an existing video, used when a slow upload outlives its ticket. `videoId` must be a hyphenated GUID; anything else gets 400. It does not call Bunny, so no lookup is needed, and the signature is bound to that video id.
- **Audit.** `video_upload_started` with `{ videoId, title }` on create. Renewing isn't audited.

## 4. Frontend (`VideoPicker`)

- **Dependency:** `tus-js-client`, the only new one.
- **Button.** "Unggah video baru" sits above the search box. It's shown only when the library list is available, i.e. the page has no `Unavailable` reason. Under the dev provider the list reports Unavailable, so the button is hidden.
- **Preparing.** Choosing a file uses an `<input type="file" accept="video/*">`. A row then shows the file name and a title input, prefilled with the file name minus its extension, plus `Mulai unggah` and `Batal`.
- **Uploading.**
  - **Ticket.** `Mulai unggah` gets a ticket, then starts a `tus.Upload` with:
    - `endpoint` from the ticket;
    - `chunkSize: 50 * 1024 * 1024`;
    - `retryDelays: [0, 3000, 10000, 30000]`;
    - headers from the ticket;
    - metadata `{ filetype, title }`;
    - fingerprint-based resume, which is tus-js-client's default URL storage.
  - **Progress row.** It shows a bar plus `Mengunggah {pct}% · {sent} dari {total}` (sizes in MB), and a `Batalkan` button that aborts the upload.
  - **Leaving the page.** While an upload runs, `beforeunload` asks for confirmation. The rest of the form stays usable.
- **Expired ticket.** If the upload fails with 401 or 403 because the ticket expired, the picker calls the renew route once and resumes with the new headers.
- **Finished.** The row reads `Terunggah — Bunny sedang memproses video.` The library query refetches every 10 seconds while any listed video is processing or a just-uploaded one isn't listed as finished yet, then stops. There is no auto-select (D4).
- **Failed.** After retries are exhausted, the row reads `Unggahan terputus. Pilih file yang sama untuk melanjutkan.` Choosing the same file resumes the upload through the stored fingerprint.
- **List.** A video in status `Created` that isn't the upload running in this tab shows `Unggahan tidak selesai` and is disabled.
- **Accessibility.** The progress bar is a `<progress>` element (or `role="progressbar"`) with an accessible label. Status text sits in an `aria-live="polite"` region. Every button has visible focus.

## 5. Tests

**Server** (integration, with Bunny's HTTP faked the way `VideoLibraryTests` does):
- creating an upload returns a ticket whose signature equals SHA-256(libraryId+apiKey+expiresAt+videoId), with `expiresAt` about 2 hours ahead, and the response body does not contain the API key;
- the route is admin-only (a learner gets 403);
- an empty or 201-character title gets 400;
- Bunny returning 401 or 500 gives 502 with the Indonesian message;
- the dev provider gives 409;
- renewing returns a fresh signature for the same video, and a non-GUID id gets 400;
- a unit test of `BunnyUploadSigner.Sign` against a known vector.

**Frontend:** lint and build. The manual browser check uploads a small real file to the Bunny library, with the PO watching.

## 6. Out of scope

- Deleting or renaming videos (the "Pustaka Video" page).
- Thumbnails.
- Captions upload.
- Bulk upload.
