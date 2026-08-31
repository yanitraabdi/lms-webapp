# Question-bank bulk import — design

**Date:** 2026-08-31
**Status:** approved, ready for planning

---

## 1. Why

A TOEFL ITP final assessment needs **140 questions** (50 Listening / 40 Structure / 50 Reading).
The admin UI authors them one at a time through a modal, which makes assembling a real bank the
practical bottleneck between a working build and a sellable product. KAK §17 item 4 records the
bank as owed by the business; this removes the engineering obstacle to delivering it.

The content exists as prose — Word documents, PDFs, textbooks — so somebody has to transcribe it.
That single fact drives most of the design: the format is a **transcription target for a
non-technical author**, not an interchange format between systems.

Two consequences follow, and they rule out the obvious approach:

1. **Listening prompts contain line breaks.** A short conversation is three lines: two speakers
   and the question. Clipboard-paste of TSV — the mechanism the score-band screen uses — cannot
   carry an embedded newline, so it is not an option here.
2. **A Reading passage serves about ten questions.** `Question.PassageRef` holds the *full passage
   text*, not a reference, so a naive row-per-question sheet would make the author retype an
   800-character passage ten times, and correct it in ten places.

## 2. Scope

**In:** an `.xlsx` (and CSV) importer for questions, a downloadable template, a validating preview,
upsert on an author-assigned id, and a bulk audio upload that connects Listening questions to their
recordings.

**Out:**

- **Importing anything but questions.** Programs, sessions, assessments and score bands each have
  their own admin surface already.
- **Parsing the source documents.** Reading a Word file or a PDF and inferring questions from prose
  is a different and far less reliable project. A human transcribes.
- **Assembling an assessment.** Import fills the *bank*; the existing composer chooses which
  questions go into a final assessment. Keeping these separate means a bad import cannot disturb a
  live exam.

## 3. The workbook

### Sheet `Questions`

| Column | Required | Notes |
|---|---|---|
| `id` | yes | Author-assigned, unique within the file. `L01`, `S14`, `R07`. Drives upsert. |
| `section` | yes | `Listening`, `Structure`, `Reading`, `Vocabulary`, `General`. Case-insensitive. |
| `prompt` | yes | May contain line breaks. |
| `choice_a`…`choice_d` | a, b | At least two non-blank, matching the existing rule in `QuestionBankService`. |
| `answer` | yes | A single letter — `A`, `B`, `C`, `D`. |
| `passage_id` | no | References the `Passages` sheet. |
| `audio_file` | no | A plain filename, `L01.mp3`. |
| `tags` | no | Comma-separated. |

**`answer` is a letter, never an index.** A transcriber will reliably write `C` and will
unreliably write `2` for the third option. The importer converts to the 0-based index the
`Correct` column stores.

**Exactly one answer per question.** `Question.Correct` is an array, but a student submits a single
int (`Answers` is `Dictionary<string, int>`) and scoring is `Correct.Contains(picked)` — so the
array means "any of these is accepted", not "select all that apply". TOEFL ITP has one correct
answer per item, and the admin UI offers a radio button, so the importer accepts one letter and
rejects `A,C`. Allowing a list would imply a capability the runtime does not have.

### Sheet `Passages`

`passage_id` | `text`. The text is copied onto every question that references it, which is exactly
what `PassageRef` holds today — so no schema change, and the runtime is untouched.

### Sheet `Instructions`

Valid section names, the answer-letter convention, which columns are required, and one filled
example row per section. Generated as part of the template, so the author never starts from a blank
sheet and never has to be sent a separate document.

### CSV fallback

A single CSV maps to the `Questions` sheet only. Passages must then be written inline per row, and
there is no instructions sheet. Accepted because it was asked for, but `.xlsx` is the supported
path and the template is only offered as `.xlsx`. **This is the one part of the design I argued
against:** it doubles the parsing surface for a workflow that will settle on one format, and a
stray quote in transcribed prose corrupts a CSV row in ways that are hard to explain to a
non-technical author. Recorded here so the trade is visible rather than forgotten.

## 4. Flow

Upload → parse → validate **every** row → preview with all errors at once → commit only if clean.

**All-or-nothing.** A partial import leaves the author unsure what landed, which is worse than a
clean refusal when the fix is "correct the sheet and upload it again". This matches the
score-band screen, where the same reasoning applied.

The preview shows what will be **created** and what will be **updated**, counted per section, plus
every error with its row number and column. Errors are in Bahasa Indonesia and name the cell.

## 5. Upsert

Questions match on `id` → `Question.ExternalId`.

- Present in the bank → update in place.
- Absent → create.

This is what makes "fix one row, re-upload the whole sheet" safe, and it is the reason the feature
is worth building rather than a paste box that always inserts.

**Schema change:** a new nullable `Question.ExternalId` (`text`) with a **filtered unique index**
where the value is not null — questions authored by hand have no external id, and several of them
must be allowed to coexist. One additive migration; nothing is dropped.

**A question already used in an assessment is still updatable.** Editing a question's text does not
invalidate an attempt: attempts store the chosen indices, and `attempts`, `proctor_events` and
`certificates` are never touched by an import. Deleting is not something import does at all — a row
removed from the sheet is left alone in the bank, because silently deleting a question that a
learner has already answered would violate the retention rule (GR-7).

## 6. Audio

A bulk upload control accepts many audio files at once. Each is stored under a **deterministic key
derived from its filename**: `L01.mp3` → `audio/l01.mp3`. The importer computes the same key from
the sheet's `audio_file` column.

This means no filename-to-key mapping table, and the two steps are order-independent — import
first or upload first, whichever the author does. Re-uploading a file overwrites it, which is the
correct behaviour for a correction.

**The extension still comes from the content type, never the client filename** — the existing rule
in `MediaUpload`. Only the *basename* is taken from the filename, lowercased and stripped to
`[a-z0-9._-]`, so it satisfies `LocalObjectStorage.IsValidKey` and cannot escape the storage root.

A referenced audio file that has not been uploaded is **not** an import error: the question imports
with its `AudioRef` set, and the existing `listening_audio_present` readiness check refuses to
publish until the object actually exists. That check already distinguishes "no audio configured"
from "configured but missing from storage", so the failure is reported where an admin will act on
it, rather than blocking a bank import for a file that may arrive an hour later.

## 7. Dependency

Reading `.xlsx` by hand means unzipping the package and parsing `sharedStrings.xml`, cell types and
inline strings — roughly 200 lines before writing the template is even considered. **ClosedXML
0.105.1 (MIT)** is taken instead, matching the licence care behind the PDFsharp choice.
Verified: restores on .NET 10, builds with `TreatWarningsAsErrors` at 0 warnings, no vulnerability
advisories, five transitive packages (`ClosedXML.Parser`, `ExcelNumberFormat`, `RBush.Signed`,
`SixLabors.Fonts`, `System.IO.Packaging`).

Explicitly **not EPPlus**, which is commercially licensed for non-personal use.

This is the first new backend dependency on the project and a deliberate exception to the standing
"no new NuGet" rule, taken because the alternative is a hand-rolled parser for a format that is
famously full of edge cases.

## 8. Surface

- `GET  /api/admin/questions/import/template` — the `.xlsx` template.
- `POST /api/admin/questions/import/preview` — parse and validate; returns the plan and errors,
  writes nothing.
- `POST /api/admin/questions/import` — commit a validated file.
- `POST /api/admin/media/audio/bulk` — many files, deterministic keys, returns key per filename.

All admin-only. Preview and commit parse the same file the same way; commit re-validates rather
than trusting a client-supplied plan, so a preview cannot be replayed against a changed bank.

Frontend: a new `/admin/questions/import` page — template download, file picker, audio picker,
preview table, commit button. Linked from the existing question-bank page.

## 9. Testing

**Parsing (pure, `Application.Tests`)** — the parser takes rows and returns items plus errors, with
no file or database involved:

- a letter answer maps to the right index (`C` → `2`)
- an invalid answer (`E`, `2`, `A,C`, blank) is an error naming the row and column
- fewer than two non-blank choices is an error
- an unknown section is an error listing the valid names
- a `passage_id` with no matching passage row is an error
- duplicate `id` values within one file are an error
- a missing required column is one clear error, not one per row
- line breaks inside a prompt survive intact

**Import (`Integration.Tests`, real API):**

- a clean file creates the expected questions, with passage text copied onto each
- re-importing the same file updates in place and creates nothing
- re-importing with one row changed updates exactly that question
- an invalid row rejects the **whole** import — the bank is unchanged
- a hand-authored question with no external id is never touched
- a question already used in an assessment updates without disturbing attempts
- a non-admin is refused

**Audio:**

- bulk upload stores each file under the key the importer will compute
- a filename needing sanitisation still produces a valid, non-escaping key
- an `audio_file` with no uploaded object imports fine and fails readiness, not import

## 10. Risks

- **The dependency.** First on the project; ClosedXML is actively maintained and MIT, but it is
  another thing to keep patched. It is confined to Infrastructure and touched only by the importer.
- **Author-assigned ids.** If an author reuses an id for a different question, the import silently
  overwrites the original. The preview names every row that will be *updated* rather than created,
  which is the point at which a human can notice.
- **Two formats.** See §3 — accepted deliberately, cost recorded.
- **Excel type coercion.** A cell like `A,C` is text, but an id like `01` may be read as the number
  1. The parser reads every cell as a formatted string and trims, rather than trusting cell types.
