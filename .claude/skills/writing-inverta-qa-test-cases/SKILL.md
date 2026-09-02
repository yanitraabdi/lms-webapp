---
name: writing-inverta-qa-test-cases
description: Use when asked to write, update, or extend QA or UAT test cases for INVERTA — including "make test cases for X", "add cases for the new feature", or updating docs/INVERTA_UAT_Test_Cases_*.xlsx
---

# Writing INVERTA QA Test Cases

## Overview

Test cases here are run by a junior QA engineer, fresh out of university, who has never seen this
product and cannot ask anyone questions. A case they cannot execute alone is a case that gets
marked Fail against working software, or Blocked, and either way costs a day.

**Two deliverables, always.** Every request for test cases produces **two sheets**: API and UI.
Never one combined sheet.

## The Split

A case belongs to exactly one sheet. Decide with one question:

**"Can a person see this by looking at the screen and clicking?"**

- Yes → **UI sheet**
- No → **API sheet**

| Goes in API | Goes in UI |
|---|---|
| Status codes, response bodies, headers | Anything on screen: text, buttons, badges, layout |
| Anything requiring dev tools or the Network tab | Clicking, typing, navigating, uploading |
| Authorization rules (401 vs 403 vs 200) | Whether a control is present, absent or disabled |
| "The answer key is never sent" | "The score is shown as a prediction, not an ETS result" |
| Signed URLs, expiry, tampering | Error messages the user reads |
| Validation messages returned by the server | Validation messages rendered in the form |

If a requirement has both an API and a UI half — access control usually does — write **two cases,
one per sheet**, and cross-reference them by ID. Do not write one case that spans both.

**Never put dev tools, the Network tab, a copied request URL, or "inspect the response payload"
into a UI case.** That is an API case wearing a costume, and a junior QA will not run it reliably.

## An API case is exactly these fields

Fill every one. A junior QA runs these with a REST client or by pasting the curl.

| Field | Rule |
|---|---|
| **ID** | `API-<area>-<nn>`, e.g. `API-AUTH-03` |
| **Description** | One sentence, the rule being checked |
| **Preconditions** | Name the exact account and how to get its token, or the case that creates the state |
| **Method + URL** | Full URL, e.g. `POST https://inverta.recosta.id/api/auth/login` |
| **Headers** | Every header, including `Authorization: Bearer <token>` and `content-type` |
| **Body** | The exact JSON to send, or "none" |
| **Expected status** | One number. `401`, not "401 or 403" |
| **Expected body** | The exact field and value to look for, e.g. `"title" contains "Validasi gagal."` |
| **Ready-to-run** | A complete `curl` line the tester can paste |

Example of the last two fields done right:

```
Expected status:  400
Expected body:    errors.Password contains "at least 8 characters"
Ready-to-run:     curl -i -X POST https://inverta.recosta.id/api/auth/register \
                    -H 'content-type: application/json' \
                    -d '{"name":"QA","email":"qa-new@test.local","password":"abc"}'
```

## A UI case is exactly these fields

| Field | Rule |
|---|---|
| **ID** | `UI-<area>-<nn>`, e.g. `UI-EXAM-05` |
| **Description** | One sentence, in plain language |
| **Preconditions** | The account, and the exact case ID that puts the app in this state |
| **Steps** | Numbered. Each step is one action. Start with the full URL to open |
| **Expected Result** | What is on the screen, with on-screen text in "quotes" |

**Steps must name what to click, in quotes, as it appears.** `Click "Kirim jawaban"`, not
"submit the test".

## Rules that make a case runnable

Every one of these came from a case a tester could not run.

1. **One outcome, not a menu.** "Access is denied (redirected, or a 403, or a locked message)"
   is three different results; the tester cannot judge Pass. Pick the one that actually happens.
2. **No "if visible", no "if shown", no "e.g.".** If you are unsure what the screen says, open
   the code or the running app and find out before writing the step.
3. **Never write a step the app prevents.** Check the component first. If the form always keeps a
   radio selected, do not ask QA to unmark it — test that it *stays* selected instead, and say why.
4. **The Description must match the Expected Result.** If the title promises "answers survive a
   refresh" and the expectation only asks that no attempt was consumed, the case is unanswerable.
5. **Preconditions must be reachable.** Name the case that produces the state (`Run UI-EXAM-02
   first`), or give the steps. "Has completed every earlier session" is not a precondition, it is
   a wish.
6. **Mark irreversible cases, and say when to run them.** Passing a test is permanent. Submitting
   the only attempt is permanent. Say `Run before UI-EXAM-07` and `This cannot be repeated on the
   same account`.
7. **Say how long it takes** when a case needs a real wait. A 55-minute section expiry is a case
   the tester schedules, not one they stumble into.
8. **A case needing a clock change, a database edit, or a code change is not a QA case.** It is an
   automated test. Leave it out and say so in the Info sheet.

## Language

The app is in Bahasa Indonesia; the testers read both. Every case carries **EN and ID** for
Description, Preconditions, Steps and Expected Result. On-screen text is quoted in Indonesian
exactly as it appears — never translated in the step.

## Workbook structure

`docs/INVERTA_UAT_Test_Cases_v<version>.xlsx`

| Sheet | Contents |
|---|---|
| `Info` | Accounts, how to run a case, how to get a token, what is NOT in this build, counts |
| `API Test Cases` | Every API case |
| `UI Test Cases` | Every UI case |
| `Answer key` | Correct answers for seeded tests, so QA can pass a test on purpose |

The **Info sheet must list what is out of scope for this build**, with the reason. Without it, QA
files known gaps as bugs — this has already happened once here.

The **Info sheet must also carry the token recipe**, because every API case needs one and a junior
QA will not invent it. Give it as a runnable pair:

```
1. Sign in and copy "accessToken" from the response:
   curl -s -X POST https://inverta.recosta.id/api/auth/login \
     -H 'content-type: application/json' \
     -d '{"email":"siswa@test.local","password":"<ask your lead>"}'

2. Paste it into every case that shows <token>. It expires after 15 minutes — repeat step 1.
```

Never put a real password in the workbook. Write `<ask your lead>` and say so.

## Before you write a single case

Read the actual behaviour. Do not write cases from a feature description.

- Endpoint, status codes and messages: `backend/src/Api/Endpoints/`
- What the screen really says: the relevant `frontend/app/**/page.tsx` or component
- Seeded accounts and data: `backend/src/Infrastructure/**/Seeder*.cs`
- What is already proven automatically: `backend/tests/` — if 380+ tests cover it, say so in the
  case rather than duplicating machine work as manual clicking

## Common Mistakes

| Mistake | Why it fails | Instead |
|---|---|---|
| One sheet mixing both kinds | Different tools, different testers, one verdict for two kinds of evidence | Two sheets, cross-referenced |
| "Open dev tools and check the payload" in a UI case | Junior QA will not do this reliably | Move it to the API sheet with a curl |
| "Expected: 401 or 403" | Cannot be judged | One status |
| Step asks for a state the UI blocks | Marked Fail against working software | Test the block itself |
| Precondition assumes hours of prior work | Case gets skipped | Name the case that sets it up |
| No ordering note on a one-shot case | Later cases silently become impossible | `Run before X`, `cannot be repeated` |

## Real-World Impact

The first version of this workbook had 60 cases and shipped four that were impossible to execute
(the UI prevented the state) plus one whose title contradicted its own expected result. QA reported
two of them as product bugs. Both were correct behaviour.
