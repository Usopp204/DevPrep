# 09 — Limitations and Future Work

Phase 7. Organized by severity, not by chapter, so a reader can triage without cross-referencing
six documents. Every item restates a fact already established with a citation in an earlier
chapter; none are new claims. "What would fix it" is offered only where the fix is a direct,
evidence-grounded consequence of the finding — never speculation about unstated intent.

---

## Security

1. **Unauthenticated ai-service surface.** `/parse/*`, `/interview/*`, `/evaluate`, `/catalog`,
   `/ui` carry no authentication check of any kind. *Evidence*: `app/main.py:53-60` router
   registration, each router file grepped for `Header`/`X-Internal-Api-Key`/`_check_key` with no
   match (G1; 07 §5). *Consequence*: not exposed in the default deployment (no host port published
   for ai-service in `docker-compose.yml`); exposed if `docker-compose.override.yml`'s host-port
   mapping is active with a tunnel, a scenario the codebase's own comment already warns about
   (`docker-compose.override.yml:26-28`). *Fix*: apply the same `X-Internal-Api-Key` check (or a
   stronger mechanism) to these routers before ever publishing ai-service's port outside the
   internal Docker network.
2. **CORS wildcard origin with credentials, confirmed functional.** `ALLOWED_ORIGINS=*` combined
   with `allow_credentials=True` was verified this phase against the actual installed Starlette
   source (`starlette/middleware/cors.py:166-168`) to reflect the literal request origin rather than
   the wildcard — i.e. it functions as an unrestricted-origin, credentialed CORS policy, not an
   inert misconfiguration (07 §5, `OPEN_QUESTIONS.md` #3, resolved). *Consequence*: same scoping as
   item 1 — relevant only when ai-service's port is externally reachable. *Fix*: set
   `ALLOWED_ORIGINS` to the specific WebClient/Gateway origins actually expected to call ai-service,
   once any legitimate browser-facing use of these routes is identified; otherwise remove
   `allow_credentials=True`.
3. **Internal API key checks fail open when unset.** Both `X-Internal-Api-Key` implementations
   (`biometrics/security.py:13-15`, `app/api/internal.py:21-24`) pass unconditionally if
   `INTERNAL_API_KEY` is empty (07 §4). *Consequence*: none in the committed configuration, which
   sets a non-empty key — but a future deployment misconfiguration (an empty key reaching
   production) would silently disable authentication rather than fail loudly. *Fix*: fail closed
   (reject all requests) when the configured key is empty, rather than treating empty as "open."
4. **`InternalApiKey` committed in plaintext.** Key name `AiService:InternalApiKey`
   (`source/InterviewAPI/appsettings.json:15`, `source/EvaluationAPI/appsettings.json:15`), value
   `<redacted>` per this report's secrets policy. *Consequence*: the shared internal secret is
   readable by anyone with repository access, which may be acceptable for a capstone/development
   context but would need rotation before any real deployment. *Fix*: move to environment-only
   configuration (as the JWT signing key already is) before any non-development use.

## Integrity (biometric/anti-cheat)

5. **No presentation-attack defence on either modality, for two distinct reasons.** Face: liveness
   is computed but explicitly excluded from the pass/fail decision, after real-hardware testing
   showed the yaw signal does not function (0.55–1.61° measured against an 18° threshold,
   `router.py:139-147`) — an evidence-based disablement (G5 §Threat model). Voice: the
   content/challenge-check mechanism that would add a second layer is fully implemented but wired
   only to an unreachable UI path (`biometric-wizard.js`'s "verify mode," G5 F-17 §7,
   `OPEN_QUESTIONS.md` #22) — an unreachable feature, not a disablement. *Consequence*: a static
   photo/video (face) or a replayed recording (voice) of the genuine enrolled candidate is not
   caught by either channel today. *Fix*: face requires new liveness engineering (the current yaw
   signal must be diagnosed and repaired, per the TODO at `router.py:146`) before re-enabling; voice
   requires only UI wiring, since the mechanism already exists and works as designed.
6. **Every match/quality threshold in the system is uncalibrated** (08 §4–5 for the complete table
   and the FAR/FRR/EER explanation). No threshold has measured FAR/FRR/EER evidence behind it;
   `EVAL.md` explicitly forbids presenting fabricated calibration data as real accuracy. *Fix*:
   collect a labelled genuine/impostor dataset under representative conditions and run
   `calibrate.py` to produce a real `models/calibration.json`, which the code (`router.py:40-46`)
   is already written to prefer over the fixed defaults if the file existed.
7. **No score-level fusion of face and voice occurs at runtime.** `FusionDecisionHelper` (a sigmoid
   over a weighted combination of both modalities' scores) has zero callers anywhere in `source/`
   (confirmed by grep twice, Phase 0.5 and G5); each modality is thresholded independently. In one
   sentence, fusion would have added a joint decision surface — tolerating a slightly weaker match
   on one modality when the other is strong — instead of two independent yes/no gates (G5 § AI/ML
   consolidation, 08 §2). *Fix*: wire `FusionDecisionHelper` into `BiometricVerificationService`'s
   entry-gate decision once/if a calibrated `calibration.json` exists to feed it meaningful weights.

## Scoring validity

8. **The 50/30/20 grading rubric weights have no recorded provenance.** Annotated in code only as
   "(design section 4.2)," which does not exist as a locatable document anywhere in the repository
   (G3 F-09 §8f, `OPEN_QUESTIONS.md` #14).
9. **Grade letter-band cut-offs (A≥85, B≥70, C≥55) have no recorded derivation** (G6 F-19 §8c).
10. **Three independently-chosen score-band threshold sets exist for related purposes with no
    single source of truth**: grade letters (85/70/55), narrative comment bands (85/70, consistent
    with each other across ai-service and WebClient), and display color/hero-title bands (80/60,
    numerically inconsistent with the other two) (G6 F-19 §8d, `OPEN_QUESTIONS.md` #25).
11. **The load-bearing statement, restated once more, deliberately, for this consolidated
    chapter**: the score a candidate receives is a large-language-model judgement, run through fixed
    rubric weights of unrecorded provenance, averaged across questions by simple arithmetic mean,
    and mapped to a letter grade at cut-offs that are likewise unrecorded. **It is not a
    psychometrically validated instrument** — no reliability (inter-rater or test-retest
    consistency) or validity (correlation with an independent, external measure of interview
    performance) evidence exists anywhere in this codebase for this scoring pipeline (G6 F-19 §8f).
    *Fix*: this is a research question, not an engineering one — establishing either property would
    require a validation study outside the scope of this codebase.

## Robustness

12. **The in-memory grading queue loses all pending work on process restart.** `BoundedGradingQueue`
    wraps a `System.Threading.Channels.Channel` with no persistence (G3 F-09 §6). *Compensation
    that exists*: `RequestRegradeIfNeededAsync`, triggered on report view, re-queues missing
    evaluations via a non-blocking path, with a 60-second per-session cooldown and two dedup layers
    (G3 F-09 §6, G6 F-19 §7). *Residual gap*: a session whose report is never viewed after a crash
    could, in principle, keep missing evaluations indefinitely, since nothing else triggers the
    recovery check (G3 F-09 §11).
13. **Unlimited concurrent `Initialized` sessions per user.** `SessionRepository.GetActiveSessionAsync`
    filters only on `Status ∈ {InProgress, Paused}`, never `Initialized` — both the session-creation
    guard (G1) and the Resume-discovery banner (G2) miss this state entirely, so a candidate can
    hold arbitrarily many configured-but-not-entered sessions at once (G2 F-07 §7 R1,
    `OPEN_QUESTIONS.md` #11). Whether intentional is not recorded anywhere.
14. **The voluntary Pause/Unpause writes have no atomic guard**, unlike every other quota/state
    transition in this codebase (`UpdateStatusIfAsync`, `TryConsumeHintAsync`,
    `TryConsumeSkipCooldownAsync`, `IncrementAnsweredIfAsync` all use conditional
    `ExecuteUpdateAsync`; Pause/Unpause use plain `SaveChangesAsync`) — a double-click or two-tab
    race could in principle push `PauseCount` past its cap of 3 by one (G2 F-06 §6–7,
    `OPEN_QUESTIONS.md` #12). Not reproduced.
15. **The cross-account enrolment-bonus dedup scan is O(n).** `IsCanonicalBonusAccountAsync` fetches
    every face-modality `ProfileBiometrics` row for every user in the system on every enrol/re-enrol
    call, with no filtering, pagination, or index beyond `Modality` (G4 § Enrolment bonus). Fine at
    a capstone project's scale; would not survive unmodified at a materially larger user base.

## Correctness

16. **`SYSTEM_DESIGN_LEVELS` vs. InterviewAPI's `Level` whitelist mismatch.** ai-service's
    `SYSTEM_DESIGN_LEVELS = {"middle","senior","techlead"}` gates the System-Design question track;
    InterviewAPI's `Level` whitelist has no `"techlead"` value and lowercases `"Lead"` to `"lead"`,
    which is not in that set. **Candidates configured at `Lead` level never receive System-Design
    questions** — only `Middle` and `Senior` do (G1 F-03 §7 R3, `OPEN_QUESTIONS.md` #8). Whether
    this is a naming drift or an intentional design choice is not recorded anywhere; not adjudicated
    by this report.
17. **`FeedbackReport.TheoryScore`** is a declared EF property and SQL column with zero read or
    write sites anywhere in EvaluationAPI, confirmed twice (Phase 0.5, G6) (`Status: Declared but
    unused`).
18. **Stale code comments, four instances found across this audit**, listed together since each is
    documentation-accuracy only, not a functional defect:
    - `SessionService.cs:499` — "TODO: deduct credit via PaymentApiClient... not implemented yet" —
      deduction *is* implemented, at `GenerateQuestionAsync:718`, just not at `InitAsync` where the
      comment sits (G2 F-05 §7 R5, Phase 0.5 §Q5).
    - `SessionService.cs:489` — "TODO: award a one-time credit bonus on first biometric enrollment"
      — the bonus *is* implemented, in `ProfileBiometricsService.EnrollAsync` (G4 § Enrolment bonus,
      Phase 0.5 §Q6).
    - `BiometricEmbeddingResult.cs:6` — "Quality is face-only; null for voice" — voice *does*
      receive a computed quality value (G4 F-12 §7 R3, `OPEN_QUESTIONS.md` #17).
    - `SessionVerificationController.cs:151-153` — "not called from any Room UI yet" — the endpoint
      *is* called, from two confirmed live sites (G5 F-15 §7 R4, `OPEN_QUESTIONS.md` #21).

## Data protection

19. **Embedding-only storage as a designed mitigation, stated as a genuine strength.** No raw face
    image or voice recording is persisted anywhere in the traced enrolment or verification call
    chains — confirmed by reading every write path in G4 and G5. `ProfileBiometric.cs:8-9`: "Stores
    the embedding vector only — never raw images/audio (privacy: Decree 13/2023)." Consent is
    captured once, explicitly naming the same decree, at the wizard's guide screen
    (`Views/Profile/Identity.cshtml:96-101`, quoted in full in G4 F-12 §10).
20. **No deletion or withdrawal flow was located for enrolled biometric templates.** A repo-wide
    search for any delete/remove/withdraw operation on `ProfileBiometrics` found none —
    re-enrolment *replaces* a template but nothing *removes* one (G4 F-12 §11,
    `OPEN_QUESTIONS.md` #19). **Stated as an observation, not an assertion of non-compliance**: an
    embedding is still biometric personal data under Nghị định 13/2023/NĐ-CP even though it is not
    the raw source media; storing only the embedding reduces but does not eliminate the data
    controller's obligations, and a data-subject deletion/withdrawal right is a standard expectation
    this codebase does not yet implement for this table.

---

## Author's account (not code evidence)

*This subsection is a stub, deliberately left for the report's author to complete. No rationale for
the Gemma-2B model's non-integration into the deployed request path (§ "Trained or implemented
components not on the deployed path," `08_ai_model_catalogue.md` §2) is recorded anywhere in the
codebase — no comment near `create_session`, `GemmaChat`, or the ai-service Dockerfile's GPU
reservation explains the decision. This report's evidence rule forbids inferring or inventing a
motive, so none is offered here.*

**TODO (author): if there is a real reason the conversational Gemma interviewer was left unwired —
timeline, VRAM budget on the actual deployment target, a decision to prioritize the RAG+judge path
instead, or something else — record it here in your own words. This is the one place in the report
where first-person authorial context belongs; everywhere else in this document is code evidence
only.**
