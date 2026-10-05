# Separate question banks — design

**Date:** 2026-10-05 · **Status:** approved in brainstorming, awaiting spec review
**Business feedback (2026-10-04):** "Bank Soal untuk Simulasi TOEFL dan Test Sesi dibedakan agar mudah diatur."

## 1. Why

Today there is one question bank (183 questions locally), labelled only by TOEFL section. Every picker shows all of it, so an admin composing a short session test scrolls past about 150 simulation questions. Nothing stops a simulation question being used in a session test, which would show learners simulation content in advance.

## 2. Decisions (PO, 2026-10-05)

| # | Decision |
|---|---|
| D1 | Every question belongs to **exactly one** bank: **Simulasi TOEFL** (`Simulation`) or **Tes Sesi** (`SessionTest`). |
| D2 | A test only takes questions from its own bank: Final → `Simulation`, Gating → `SessionTest`. |
| D3 | No copies are made of existing questions. Questions a test already holds stay in it, even if they are from the other bank, so past attempts and their reviews are untouched. |

## 3. Data

**New column `questions.bank`.**
- Type: text enum `QuestionBank { Simulation, SessionTest }`, stored as a string like the other enums.
- It is `NOT NULL`. The migration adds it with a default of `'SessionTest'` and the backfill below sets the real values. The model has no default, so the app must always set the bank explicitly.
- Index on `(bank, section)`, because the list filters by both.

**Backfill (a data step in the same migration, idempotent).** It runs once, by where each question is used today.

| Used in | Bank |
|---|---|
| Only Final assessments | `Simulation` |
| Only Gating assessments | `SessionTest` |
| Both (locally: 10 questions shared by the placeholder simulation and the seeded sample test "Contoh Tes TOEFL ITP (15 soal)", which has 4 attempts) | `Simulation` |
| Unused | `SessionTest` |

**Seeders.**
- `PlaceholderFinalExamSeeder` and the final assessment in `ProgramSeeder` create `Simulation` questions.
- `SessionQuizSeeder` creates `SessionTest` questions.
- `SampleTestSeeder` is a Gating test, so it creates `SessionTest` questions. That only matters on a fresh database, where it has nothing to share with.

## 4. Rules (server-side)

1. **Composing a test** (`PUT /api/admin/assessments/{id}/questions`). Every question id must be in the test's bank (Final → `Simulation`, Gating → `SessionTest`), **except** ids the test already held before this call (D3). Otherwise the call is refused with 400 `Soal dari bank lain tidak bisa dipakai di tes ini.`
2. **Creating a question.** `bank` is required. If it is missing or unknown, the call is refused with 400 `Pilih bank soal.`
3. **Updating a question** (`PUT /api/admin/questions/{id}`) never changes its bank.
4. **Moving a question** (`POST /api/admin/questions/{id}/move` with body `{ "bank": "Simulation" | "SessionTest" }`) is refused while the question is used by a test that would no longer match its bank (moving to `SessionTest` while any Final assessment uses it, or to `Simulation` while any Gating test uses it): 409 `Soal ini masih dipakai di {tes akhir | tes sesi}. Lepas dari tes tersebut terlebih dahulu.` Moving to the bank it is already in does nothing and succeeds. Every move is audit-logged (`question_moved`).
5. **Listing** (`GET /api/admin/questions?bank=…&section=…&search=…`) filters by bank when one is given. The response's `AdminQuestionDto` gains a `Bank` field.
6. **Bulk import** (`POST /api/admin/questions/import/preview` and `/import`):
   - Takes a required `bank` form field. If it is missing, the call is refused with 400 `Pilih bank soal tujuan impor.`
   - Every created row goes into that bank.
   - A row whose external ID matches an existing question in the **other** bank is reported as a row error: `ID {ExternalId} sudah ada di bank {other}.` It is not moved or updated.
   - The sheet format is unchanged.

## 5. Admin UI

**`/admin/questions`.**
- Two tabs at the top, **Simulasi TOEFL** and **Tes Sesi**, each with its count. The section filter and the search apply within the tab.
- "Tambah soal" creates the question in the active tab's bank. The form shows the bank as a read-only label.
- Each question row has a **Pindahkan ke bank lain** action. It shows the server's message when refused.
- The tab is kept in the URL (`?bank=`), so a reload and the back button keep it.

**Pickers.**
- **`QuestionPicker`, used by the session-test editor,** lists `SessionTest` only. Already-attached questions from the other bank show a `dari bank simulasi` badge and can be removed but not re-added.
- **The simulation composer (`/admin/assessments/[id]`)** lists `Simulation` only, with the mirror badge `dari bank tes sesi`.

**`/admin/questions/import`.**
- A required choice, "Impor ke bank: Simulasi TOEFL / Tes Sesi", with no default. Upload stays disabled until a bank is chosen.
- The preview shows the chosen bank.

## 6. Tests

- **Migration backfill:** the four assignment rules, and running it twice changes nothing.
- **Listing:** a bank filter returns only that bank, and no filter returns all.
- **Composing:**
  - a Gating test refuses a `Simulation` question with the exact message;
  - it accepts a `Simulation` question it already held;
  - a Final test refuses a `SessionTest` question.
- **Creating:** a question without a bank is refused.
- **Moving:** refused while used by a test of the other kind; succeeds when it is not; moving to the same bank does nothing.
- **Import:**
  - missing bank is refused;
  - rows land in the chosen bank;
  - an external ID that exists in the other bank gives a row error and the question is unchanged.
- **Answer key:** student DTOs never gain `bank` or the answer key (no change expected; the existing GR-11 tests stay green).

## 7. Out of scope

- Per-bank permissions.
- A third bank (for example a placement test). Adding one later is a new enum value plus its kind mapping.
- Copying questions between banks.
