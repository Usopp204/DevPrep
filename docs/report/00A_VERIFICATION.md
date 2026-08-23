# 00A — Targeted Verification (Phase 0.5)

Status: read-only verification pass, answering 8 specific questions raised while auditing Phase 0
evidence. Every claim below was produced by direct `grep`/`Read` of the cited file, not by
inference. Findings that change or sharpen a Phase 0 claim are marked **[UPDATES 00_EVIDENCE_MAP]**.

---

## Q1 — Is the Gemma-2B LoRA interviewer on the live execution path?

**Answer: NO. `GemmaChat` is unreachable from the deployed product.** [UPDATES 00_EVIDENCE_MAP §D, §F]

Evidence chain:
1. `GemmaChat()` is instantiated in exactly one place in the entire codebase:
   `ai-service/app/services/interviewer.py:430`, inside `create_session(config)` (`interviewer.py:423-430`).
2. `create_session` is called from exactly one place: `ai-service/app/api/interview.py:23,75` —
   the handler for `POST /interview/start` (the stateful, in-memory session router).
3. The `/interview/*` router (`app/api/interview.py`) has **no auth check** (confirmed in Phase 0
   §B.3) and is registered at `app/main.py:56`.
4. Grepping the entire repository (not just InterviewAPI) for `/interview/start`,
   `interviewer.`, `GemmaChat`, `build_interview_plan` found matches only inside `ai-service/`
   itself (`app/api/internal.py`, `app/services/interviewer.py`, `requirements.txt`) and inside
   pre-existing `docs/**` design artifacts. **Zero matches in any `source/**` .NET or WebClient
   code.** `source/InterviewAPI/Clients/AiServiceClient.cs` — the sole bridge InterviewAPI uses to
   call ai-service — was grepped directly for `/interview/` and its only match is
   `AiServiceClient.cs:59`, `PostAsJsonAsync("/interview/tts", ...)`. It never calls
   `/interview/start`, `/interview/{id}/answer`, `/hint`, `/skip`, `/end`, `/report`, or
   `/transcript`.
5. Therefore: the Gemma-2B LoRA fine-tune (`ai-service/models/gemma-2b-devprep-sft/`, base
   `google/gemma-2b-it`) is loaded and functional **only** if a caller hits ai-service's own
   unauthenticated `/interview/*` endpoints directly — something no component in the deployed
   .NET/WebClient stack does. In the actual product flow, this model is dead weight: trained,
   checked into `ai-service/models/`, wired into a router, but never invoked by anything the
   browser or gateway can reach.

**Which model serves `/internal/questions/plan` (the endpoint InterviewAPI actually calls)?**
None — it is not an LLM call at all.
- `POST /internal/questions/plan` → `plan_questions` (`app/api/internal.py:49-63`) → calls
  `interviewer.build_interview_plan(cfg, chroma_store)` (`internal.py:56`).
- `build_interview_plan` (`interviewer.py:366-420`), read in full, does not construct `GemmaChat`,
  does not call `.generate()`, `.__call__()`, or any model object — it is pure Python composition
  logic over `store.build_question_pool(...)` (`interviewer.py:377,379,388,392,398`), which is
  itself an exact-match ChromaDB metadata filter (`chroma_store.py:219-251`, confirmed in Phase 0
  §D), not a semantic/generative retrieval.
- **Conclusion: the question set a candidate actually receives is assembled by deterministic
  metadata-filtered retrieval from the pre-indexed question bank — zero LLM text generation is
  involved in question planning on the live path.** This must replace the Phase 0 framing that
  implicitly grouped "question planning" under the RAG+LLM pipeline; it is RAG retrieval with no
  generation stage.

**Report-writing implication (for G1 and the AI catalogue, Phase 7):** the interviewer LLM
(`GemmaChat`) must be documented with `Status: Implemented but not reachable from the deployed
system` and its own dedicated evidence trail, separate from question planning (which uses no LLM)
and from grading/hint (which uses the Gemini judge, confirmed working end-to-end via
`/internal/evaluate-answer` and `/internal/hint`, both called by .NET). This is arguably the most
consequential finding of Phase 0.5 for the AI deep-dive chapters: a fully trained, LoRA-fine-tuned,
locally-hosted conversational model exists in the repository and is not part of the request flow
the thesis is meant to describe.

---

## Q2 — Judge model identity, single answer

`GEMINI_JUDGE_MODEL` resolution, traced fully:
- Code default: `ai-service/app/core/config.py:32` — `os.getenv("GEMINI_JUDGE_MODEL", "gemini-3.1-pro")`.
- `ai-service/.env` does not exist (confirmed: `ls ai-service/.env` → not found).
- Root `.env` (`Q:\DevPrep\.env:33`) sets `GEMINI_JUDGE_MODEL=gemini-3.5-flash-lite`.
- `infra/.env` does **not** set `GEMINI_JUDGE_MODEL` (grep: no match).
- `infra/docker-compose.yml` / `docker-compose.override.yml` do **not** set `GEMINI_JUDGE_MODEL` in
  the `environment:` block for the `ai-service` service (grep: no match); the service only picks up
  env vars through `env_file: [../.env, .env]` (`infra/docker-compose.yml:126-128`, i.e. root `.env`
  then `infra/.env`).
- Config loading confirms which `.env` local (non-Docker) runs read: `ai-service/app/core/config.py:11-12`
  computes `ROOT_ENV_PATH = Path(__file__).resolve().parents[3] / ".env"` — three `.parents[]`
  levels up from `ai-service/app/core/config.py` is the repo root, so `ROOT_ENV_PATH` resolves to
  `Q:\DevPrep\.env`, the same root file, for local dev too.

**Answer: `gemini-3.5-flash-lite` wins at runtime in both (a) local dev and (b) docker compose**,
sourced from root `.env:33` in both cases (Docker via `env_file`, local dev via `load_dotenv`).
The code-level default string `"gemini-3.1-pro"` (`config.py:32`) is never actually used in either
runtime configuration present in this repo. [UPDATES 00_EVIDENCE_MAP §D — already stated this value
but now the resolution path for both environments is confirmed, not inferred.]

No API keys were printed to produce this answer.

---

## Q3 — "Liveness" language audit across the user-facing surface

Case-insensitive grep for `liveness|live|anti-spoof|spoof|chống giả mạo|xác thực sống` across
`source/WebClient/Views/**`, `source/WebClient/wwwroot/js/**`, and `docs/**`.

**WebClient hits (6 total, all in code comments — none in rendered/user-visible UI copy):**
| File:Line | Quote (≤10 words) | User-visible? |
|---|---|---|
| `source/WebClient/Program.cs:35` | "Liveness probe for the container HEALTHCHECK." | No — infra comment, unrelated meaning (container health, not biometric) |
| `Controllers/Interview/VerificationController.cs:91` | "single frame, no challenge/liveness" | No — C# comment |
| `Controllers/Interview/InterviewController.cs:119` | "liveness face check for this session before the Room" | No — C# comment |
| `Views/Interview/Verify.cshtml:18-19` | "no head-turn/liveness requirement — see ai-service/biometrics/router.py for why liveness is disabled" | **Sent to browser as raw HTML comment (`<!-- -->`), not rendered as visible text**, but technically present in page source |
| `wwwroot/js/pages/entry-verify.js:3` | "No head-turn/liveness requirement" | No — JS comment |

**Finding: no rendered/visible UI text anywhere in WebClient promises liveness detection or
anti-spoofing to the candidate.** The one HTML-comment hit (`Verify.cshtml:18-19`) is honest about
the disablement rather than misleading, and is invisible in the rendered page (only visible via
"View Source"). No hits at all for `anti-spoof`, `spoof`, `chống giả mạo`, `xác thực sống` anywhere
in WebClient. **This resolves the risk flagged in Phase 0 §F.3 in the report's favor**: the product
does not overclaim liveness/anti-spoofing capability to end users anywhere in its own UI text,
despite the underlying check being disabled. This is a positive finding worth stating explicitly in
the G5 chapter's "Security and integrity considerations" section, not just the disablement itself.

**`docs/**` hits**: 25 files matched, entirely pre-existing design/audit artifacts
(`docs/diagrams/**`, `docs/audit/AUDIT-2026-08-13.md`, `docs/architecture/microservice-audit.md`,
`docs/architecture/phase5-verification.md`, `docs/CLEANUP_PLAN.md`) plus this report's own
`00_EVIDENCE_MAP.md`. These are internal engineering documents, not user-facing copy, so they are
out of scope for the "does the product overclaim to users" question — noted for completeness only.

---

## Q4 — `calibration.json`

**Does not exist.** Confirmed by two searches: `find ai-service/models -iname "calibration.json"`
(no result) and a repo-wide `find . -iname "calibration.json"` excluding `.venv`/`node_modules`
(no result).

Read `source/InterviewAPI/Helpers/FusionDecisionHelper.cs` in full (44 lines):
- Class doc comment (`:7-11`): "Reads the 'fusion' section of ai-service's `models/calibration.json`
  (produced by Milestone 3's `calibrate.py`) ... Returns null wherever real calibration data
  doesn't exist yet — callers must fall back to requiring both modalities to independently pass
  their thresholds and liveness checks."
- `TryLoadFusionWeights(string? calibrationJsonPath)` (`:15-36`): line 17 — `if
  (string.IsNullOrWhiteSpace(calibrationJsonPath) || !File.Exists(calibrationJsonPath)) return
  null;` — since the file does not exist, this method **always returns `null`** in the current
  repository state.
- `Decide(FusionWeights weights, ...)` (`:38-43`) computes the sigmoid fusion decision but requires
  a non-null `FusionWeights`, which never happens given the above.

**Deeper finding beyond what was asked**: grepping the whole `source/` tree for
`FusionDecisionHelper` finds **exactly one file** — the helper's own definition
(`FusionDecisionHelper.cs:13`). **No caller exists anywhere** — not in
`BiometricVerificationService.cs` (grepped directly, zero matches), not anywhere else. This is
stronger than "falls back because the file is missing": the class is never invoked at all, by
anyone, regardless of whether `calibration.json` exists.

**Answer to "is any fusion decision made in the current runtime": No.** `BiometricVerificationService`
must therefore make its pass/fail decisions using only the independent per-modality thresholds
(`FACE_MATCH_THRESHOLD=0.35`, `VOICE_MATCH_THRESHOLD=0.25`, per Phase 0 §D) — consistent with the
helper's documented fallback intent, but arrived at because the combining code is orphaned, not
because it executed its documented fallback branch. [UPDATES 00_EVIDENCE_MAP §D — the calibration
gap is more severe than "file missing"; the consuming code path doesn't exist either. To be written
up in G5 as `Status: Implemented but unused (dead code)`, separate from the liveness disablement.]

---

## Q5 — Credit deduction: comment vs. implementation

Read `SessionService.cs:460-540` (covers `InitAsync`) and `InterviewSessionController.cs:600-650`
(covers `GenerateQuestion`) plus, to find the actual deduction line, `SessionService.cs:651-750`
(`GenerateQuestionAsync`) directly.

**The TODO at `SessionService.cs:499` is genuinely current for `InitAsync`, but stale relative to
the feature as a whole — the deduction was implemented, just in a different method.**

Exact quote, `SessionService.cs:499-501`:
```
// Step B: TODO — deduct credit via PaymentApiClient (PaymentAPI not implemented yet).
// const bool CreditsEnabled = false;
// if (CreditsEnabled) await _paymentClient.DeductCreditAsync(userId, ct);
```
This code is commented out. **`InitAsync` performs no credit deduction** — confirmed, the TODO is
accurate for this method.

However, credit deduction **does** happen, on the live path, inside `SessionService.GenerateQuestionAsync`:
- `SessionService.cs:707` — `bool isFirstEntry = session.Status == "Initialized";`
- `SessionService.cs:710` — atomic `UpdateStatusIfAsync(..., "Initialized", "InProgress", ct)`.
- `SessionService.cs:715-747` — `if (isFirstEntry && statusChanged) { ... }` block:
  - `:717` — `creditCost = CreditConfig.GetCreditCost(session.DurationMinutes);`
  - `:718` — `var deductResult = await _paymentApiClient.DeductCreditAsync(userId, creditCost, sessionId, ..., ct);` — **`PaymentApiClient.DeductCreditAsync` is reached, on the first call to `GenerateQuestion` for a session.**
  - `:721-734` — on failure, rolls the status back to `Initialized` and throws either
    `CreditServiceUnavailableException` (transport failure, mapped to 503 at
    `InterviewSessionController.cs:627-633`) or `InvalidOperationException` with the PaymentAPI's
    own refusal message (e.g. insufficient credits).
  - `:744-746` — on success, persists `session.CreditCost` and sets `session.StartedAt`.

**Conclusion: the TODO at line 499 is a stale comment.** Credit deduction was implemented, but at
session-entry time (`GenerateQuestionAsync`, gated on the `Initialized→InProgress` transition)
rather than at session-creation time (`InitAsync`, where the TODO sits). The comment was not removed
when the feature was built elsewhere. Per instruction not to reconcile silently, this is reported as
observed fact, not smoothed over: **the G2 chapter must describe credit deduction as occurring at
first question-generation, not at Init**, and should note the stale TODO as a documentation/code-hygiene
finding, not a functional gap.

---

## Q6 — Enrollment bonus: one trigger or two?

Traced every call site of `TryAwardEnrollmentBonusAsync` and `CreditConfig.BiometricEnrollmentBonusCredits`
(`grep -rn` across `source/`):
- `CreditConfig.cs:14` — constant declaration (`BiometricEnrollmentBonusCredits = 100`).
- `ProfileBiometricsService.cs:84` — `var bonusStatus = await TryAwardEnrollmentBonusAsync(userId, modality, ct);`, called from `EnrollAsync`.
- `ProfileBiometricsService.cs:125` — method definition of `TryAwardEnrollmentBonusAsync`.
- `ProfileBiometricsService.cs:145` — the actual award call site referencing the constant.

**There is exactly one call site of `TryAwardEnrollmentBonusAsync` in the entire codebase**
(`ProfileBiometricsService.cs:84`, itself inside `EnrollAsync`). `SessionService.cs` does not call
`TryAwardEnrollmentBonusAsync` and does not reference `CreditConfig.BiometricEnrollmentBonusCredits`
anywhere (confirmed by the same grep — no `SessionService.cs` hit for either symbol).

**Answer: the `SessionService.cs:489` TODO does not describe a second, unimplemented trigger — it
describes functionality that already exists, fully implemented, on the Profile-enrollment side
(`ProfileBiometricsService.EnrollAsync` → `TryAwardEnrollmentBonusAsync`, awarding 100 credits once
both Face and Voice are enrolled).** The bonus is awarded at enrollment time (Profile wizard), well
before a session is ever created — `InitAsync`'s biometric gate (`SessionService.cs:490-497`) merely
*checks* that a face row already exists; it doesn't enroll or award anything itself. The TODO at
line 489 is, like the one at line 499, a stale leftover comment describing a feature that was later
built in a different file. [UPDATES `OPEN_QUESTIONS.md` §5 — fully resolved, no second trigger
exists.]

---

## Q7 — `FeedbackReport.TheoryScore`

`grep -rn "TheoryScore" source/EvaluationAPI/` returns **exactly one match**:
`source/EvaluationAPI/Models/FeedbackReport.cs:16` — `public decimal? TheoryScore { get; set; }`.

**No read site and no write site exist anywhere in `source/EvaluationAPI/`** — not in
`EvaluationService.cs`, not in `EvaluationRepository.cs`, not in any DTO or controller. The column
is declared on the EF Core entity (and, per Phase 0 §B, exists in the SQL schema at
`db/baseline/06_DevPrep_Evaluation.sql`), maps to a real database column, but is never populated by
any code path and never read back into a response. **Confirmed dead column** — this fully resolves
`OPEN_QUESTIONS.md` §4. To be written up in the G6 chapter's data-model section as
`Status: Column exists in schema, never written or read by application code`.

---

## Q8 — Referenced-but-unverified docs

| Path | Exists? | Search performed |
|---|---|---|
| `contracts/ai-service.md` | **Does not exist** | `find . -iname "ai-service.md"` (excluding `.venv`) → no result; `find contracts -type f` lists only `README.md`, `auth/*.cs`, `feedback/*.schema.json`, `interview/score-v2.schema.json` |
| `docs/architecture/code-followups.md` | **Exists** | `find . -path "*architecture/code-followups.md"` → `./docs/architecture/code-followups.md` found. Content read: `code-followups.md:131-134` contains a section literally titled "## C-03 — EvaluationAPI's regrade call shares a circuit breaker with the calls that matter", status "accepted risk, Batch 5.5b. Not scheduled." — **this confirms the risk referenced in `EvaluationAPI/Clients/InterviewApiClient.cs:200-213` is a real, already-documented, consciously-accepted risk, not a dangling reference.** |
| `ai-service/models/calibration.json` | **Does not exist** | see Q4 |

`OPEN_QUESTIONS.md` §6 (the code-followups.md reference) is now fully resolved: the file exists and
substantiates the shared-breaker risk exactly as the code comment describes.

Incidentally, this search also surfaced that `docs/architecture/` contains other pre-existing
engineering documents (`microservice-audit.md`, `phase5-verification.md`,
`microservice-remediation-plan.md`, `tooling-followups.md`) and `docs/diagrams/`,
`docs/audit/AUDIT-2026-08-13.md` contain substantial prior analysis of this same system, including
an entry at `docs/audit/AUDIT-2026-08-13.md:1193-1194` that had already flagged the
`GEMINI_JUDGE_MODEL` default-vs-actual discrepancy independently confirmed in Q2 above. These are
noted as available background reading for later phases but are not treated as evidence sources in
place of direct code citation, per the report's evidence rule.

---

## Summary of resolutions

| Q | Resolution | Severity for report |
|---|---|---|
| Q1 | Gemma-2B interviewer LLM is unreachable from the deployed system — only ai-service's own unauthenticated `/interview/*` router calls it | **High** — reframes the entire "conversational AI interviewer" narrative for G1/G3/AI catalogue |
| Q2 | `gemini-3.5-flash-lite` confirmed for both local dev and docker compose, via root `.env:33` | Low — confirms, doesn't change, Phase 0 |
| Q3 | No user-facing liveness/anti-spoof overclaim found anywhere in WebClient UI | Positive finding — strengthens G5's integrity section |
| Q4 | `calibration.json` absent AND `FusionDecisionHelper` has zero callers — fusion is fully dead code, not just data-starved | **Medium-high** — G5 must describe biometric decisions as single-modality only, no fusion of any kind occurs |
| Q5 | `SessionService.cs:499` TODO is stale; deduction is implemented in `GenerateQuestionAsync:718` | Medium — corrects G2's credit-deduction timeline |
| Q6 | `SessionService.cs:489` TODO is stale; bonus fully implemented in `ProfileBiometricsService.cs:84-145` | Low-medium — corrects G2/G4 bonus narrative, no gap exists |
| Q7 | `FeedbackReport.TheoryScore` confirmed dead column, zero read/write sites | Low — factual note for G6 data model |
| Q8 | `contracts/ai-service.md` absent (unchanged); `code-followups.md` exists and substantiates C-03; `calibration.json` absent | Resolves 2 of 3 unknowns |
