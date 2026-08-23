# DevPrep — InterviewAPI / EvaluationAPI / ai-service / WebClient: Feature Specification

This is an evidence-based specification of 19 features spanning session configuration, session
lifecycle and credit, in-room interview interaction, biometric enrolment, biometric verification and
integrity enforcement, and evaluation/reporting — the parts of DevPrep owned by this report's author
(`InterviewAPI`, `EvaluationAPI`, `ai-service`, `WebClient`). Every behavioural claim in this
document set carries a `file:line` citation back to the source it was read from, or is explicitly
marked `[UNVERIFIED: reason]`; nothing is inferred or presented as fact without a citation. The
report's evidence rules, hard constraints, and per-feature template are recorded in the mission that
produced it, not repeated here.

## Reading order

1. **`00_EVIDENCE_MAP.md`** — the Phase 0 survey: repository map, endpoint inventory, feature→
   artefact matrix, AI/ML asset inventory, configuration inventory, and the first list of
   contradictions found. Read this first for orientation; every later chapter builds on it.
2. **`00A_VERIFICATION.md`** — Phase 0.5's targeted verification pass, resolving eight specific
   questions (including the single most consequential finding in the report: the Gemma-2B
   interviewer model is not on the deployed request path) before any feature chapter was drafted.
3. **`01_G1_session_configuration.md`** through **`06_G6_evaluation_and_reporting.md`** — the six
   feature-group chapters, one per batch, each self-contained and re-verified against source at
   the time it was written (not carried forward from memory of an earlier batch). Read in order if
   you want the narrative arc of a single interview session end-to-end (configure → start/pause/
   resume/end → chat/voice/hint → biometric enrolment → biometric verification → evaluation
   report); read any one in isolation if you only need that feature group.
4. **`07_cross_cutting_architecture.md`** — system context, the same-origin proxy pattern, the
   actual (not assumed) authorization topology, internal service-to-service auth, the timeout
   ladder, resilience policy, data/time conventions, and deployment topology. Read this before
   trusting any single chapter's security-and-integrity section in isolation — it is where the
   whole-system security posture is stated once, plainly.
5. **`08_ai_model_catalogue.md`** — every model on the deployed path in one table; every trained-
   or-implemented-but-unreachable component in a second table with the pattern named explicitly;
   every numeric threshold in the system in one honesty-first provenance table; FAR/FRR/EER
   explained once. **This is the chapter to read if you only read one.**
6. **`09_limitations_and_future_work.md`** — every limitation found in this audit, organized by
   severity rather than by chapter, plus one deliberately-unfilled stub left for the report's
   author's first-person account (never inferred by the audit itself).

## Supporting files

- **`GLOSSARY.md`** — every technical term used in this report, each with one accessible sentence
  and one precise sentence.
- **`REFERENCES.md`** — IEEE-style numbered citations, all currently `[CITATION TO VERIFY]` pending
  independent bibliographic confirmation; each entry names exactly which chapter/section cited it.
- **`FIGURE_INDEX.md`** — all 39 figures (28 diagrams, 11 screenshots), their type, chapter, and
  capture status.
- **`SCREENSHOT_CHECKLIST.md`** — what to capture, where, in what state, for every pending
  screenshot, plus one diagnostic-capture row (voice embedding dimensionality) that closes the
  report's one remaining unverified numeric fact.
- **`OPEN_QUESTIONS.md`** — every claim this report could not fully resolve from static source
  alone, with a precise statement of what remains unknown; items are moved to a "Resolved" section
  with their evidence as later batches close them, never silently deleted.
- **`diagrams/`** — every diagram's Mermaid source as a standalone `.mmd` file (also embedded
  inline in its chapter) plus `render.ps1`, a reference script for converting them to SVG (written,
  not executed, per the mission's constraints).
- **`images/`** — destination folder for captured screenshots; empty until the report's author
  works through `SCREENSHOT_CHECKLIST.md`.

## What this report is not

It is not a design document (it describes what the code does today, including features labelled
`Status: Implemented but disabled/unused/unreachable` where that is what the evidence shows) and it
is not a security audit sign-off (§ security findings are named plainly throughout, especially in
`07_cross_cutting_architecture.md` §5 and `09_limitations_and_future_work.md`'s Security section,
but no claim of overall system safety is made or implied anywhere in this document set).
