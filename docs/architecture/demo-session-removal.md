# Demo Session — removal checklist

**This is temporary code for one presentation.** It adds a 5-minute / 3-question / 6-credit
interview so the whole system can be demonstrated to a review board inside a few minutes. It is not
a product feature and must come out once the defence is over.

Written at the same time as the code, so removal is a checklist rather than archaeology.

---

## 1. Find every trace

Every demo block is wrapped in a matched pair of markers:

```
// DEMO-SESSION: remove after defence — start
// DEMO-SESSION: remove after defence — end
```

(In Razor the same text appears as `@* … *@`; in YAML as `#`.)

One command finds all of them:

```bash
grep -rn "DEMO-SESSION" source/ infra/ docs/
```

**After removal that command must return nothing except this file.** Delete this file last.

---

## 2. What to remove

### Delete outright (whole file, no surgery needed)

| File | Contains |
|---|---|
| `source/InterviewAPI/Helpers/DemoSession.cs` | Duration/count/cost constants + `IsEnabled` / `IsDemoDuration` |
| `source/WebClient/Helpers/DemoSession.cs` | Duration/cost constants + `IsEnabled` for the view |
| `docs/architecture/demo-session-removal.md` | this file — delete last |

### Remove the marked block, keep the file

| File | What the block does | After removal |
|---|---|---|
| `source/InterviewAPI/Helpers/CreditConfig.cs` | One `{ 5, 6 }` entry in `DurationToCreditCost` | Dictionary is back to 15/30/45/60 |
| `source/InterviewAPI/Services/SessionService.cs` — `ConfigDurationAsync` | A guard `if` in front of the `ValidDurations` check | Check reads `if (!ValidDurations.Contains(req.DurationMinutes))` again |
| `source/InterviewAPI/Services/SessionService.cs` — `InitAsync` | Trims the RAG plan to 3 questions | Full ai-service plan is used, as before |
| `source/WebClient/Views/Interview/Index.cshtml` | Extra `<option>` in the duration dropdown | Dropdown renders only `CreditPricing.DurationToCreditCost` |
| `infra/docker-compose.yml` | `DEMO_SESSION_ENABLED` env on `interviewapi` and `webclient` | — |
| `infra/.env.docker.example` | Documented default | — |

**Every block is safe to delete wholesale** — cut everything from the `start` marker to the `end`
marker inclusive and what remains is the original code. The `ConfigDurationAsync` block is written
as a guard `if` placed in front of the real check specifically so that this holds there too; do not
"tidy" it into an `&&` clause, which would make a blind deletion delete the real check with it.

⚠️ **`SessionService.cs` has two separate blocks.** Removing one and not the other leaves either a
duration nobody can select, or a selectable duration that yields 8 questions. The grep above
catches both — do not remove by memory.

⚠️ **Also unset `DEMO_SESSION_ENABLED` in `infra/.env.docker`** (untracked, so it is not in the
table above and grep will not find it on a clean clone — but it will exist on the machine that ran
the demo).

---

## 3. Verify after removal

Run all of these. The first two prove the demo is gone; the rest prove nothing normal broke.

```bash
# 1. No trace left anywhere.
grep -rn "DEMO-SESSION" source/ infra/ docs/          # -> no output
grep -rn "DemoSession" source/                         # -> no output

# 2. It still compiles (both helpers were referenced from four places).
dotnet build source/DevPrep.sln
```

Then, with the stack running:

| # | Check | Expected |
|---|---|---|
| 3 | Start-interview dropdown | Shows exactly four options: 15 / 30 / 45 / 60. No "(demo)" entry. |
| 4 | **A normal 15-minute session runs end to end** | Starts, deducts **30** credits, serves the full planned question set, timer counts 15:00 down, grading produces a report. |
| 5 | Repeat for 30 / 45 / 60 if time allows | Credits 60 / 90 / 120; question counts unchanged from before the demo existed. |
| 6 | `PUT /api/interview/sessions/config/duration` with `{"durationMinutes": 5}` | **400** — `"Invalid duration. Must be 15, 30, 45, or 60."` |
| 7 | `SELECT DurationMinutes, TotalQuestions, CreditCost FROM InterviewSessions ORDER BY CreatedAt DESC` | New rows only ever 15/30/45/60. Old demo rows remain and are fine — they are real completed sessions, not corrupt data. |

Check 4 is the important one. The demo touched the duration whitelist, the credit table and the
question-planning path — all three are on the normal session's critical path, so a normal session
running correctly is the real proof the removal was clean.

---

## 4. Data left behind

Demo sessions written during the presentation stay in the database as ordinary rows
(`DurationMinutes = 5`, `TotalQuestions = 3`, `CreditCost = 6`). They are **not** orphaned or
malformed:

- The plan is trimmed **before** entities are created, so `SessionQuestions` holds exactly 3 rows
  per demo session. The 5 discarded questions never reached the database.
- Nothing references the removed constants at runtime, so old rows keep rendering normally in
  history and reports.

Deleting them is optional. If you want a clean dataset for the final submission, remove them by
`SessionId` — do not delete by `DurationMinutes = 5` alone without checking, in case the value is
ever reused.
