# Figure Index

One row per figure. `Type` is `diagram` (Class A, generated `.mmd` + inline) or `screenshot`
(Class B, captured by the report's author into `docs/report/images/`). Figure IDs follow
`<chapter>.<n>` where chapter number matches the batch (1 = G1, 2 = G2, ... 6 = G6; 7 = cross-cutting
Phase 7 figures), numbered in the order they first appear in that chapter's body text — diagrams and
screenshots share one running sequence per chapter, not separate ranges. `Status` tracks whether the
figure is referenced in body text (`Referenced`) or still pending a captured image
(`Pending capture`).

| Figure ID | Caption | Type | Chapter | Source file | Status |
|---|---|---|---|---|---|
| 1.1 | Configure Interview Session, end-to-end sequence from Start to the entry-gate redirect | diagram | G1 | `diagrams/FIG-1.1.mmd` | Referenced |
| 1.2 | Configure Session Duration sequence, including the two guarded branches | diagram | G1 | `diagrams/FIG-1.2.mmd` | Referenced |
| 1.3 | Configure Level sequence | diagram | G1 | `diagrams/FIG-1.3.mmd` | Referenced |
| 1.4 | Accept Integrity Policy sequence, GET status then POST accept | diagram | G1 | `diagrams/FIG-1.4.mmd` | Referenced |
| 1.5 | Session configuration form (dashboard) | screenshot | G1 | `images/FIG-1-5.png` | Pending capture |
| 1.6 | Integrity policy consent dialog | screenshot | G1 | `images/FIG-1-6.png` | Pending capture |
| 2.1 | Start Interview & Charge Credit sequence, success and deduction-failure branches | diagram | G2 | `diagrams/FIG-2.1.mmd` | Referenced |
| 2.2 | Pause / Resume sequence, plus the lazy auto-resume note | diagram | G2 | `diagrams/FIG-2.2.mmd` | Referenced |
| 2.3 | `InterviewSession.Status` state diagram, all seven values, F-05 through F-08 | diagram | G2 | `diagrams/FIG-2.3.mmd` | Referenced |
| 2.4 | Resume Active Session: Dashboard discovery, then full Room reconstruction | diagram | G2 | `diagrams/FIG-2.4.mmd` | Referenced |
| 2.5 | CompleteSession: automatic, deadline-gated termination | diagram | G2 | `diagrams/FIG-2.5.mmd` | Referenced |
| 2.6 | EndSession: candidate-initiated cancellation, no deadline check | diagram | G2 | `diagrams/FIG-2.6.mmd` | Referenced |
| 2.7 | Pause overlay covering the chat panel during an active pause | screenshot | G2 | `images/FIG-2-7.png` | Pending capture |
| 3.1 | Send Message / Answer: synchronous response path, then the asynchronous grading handoff | diagram | G3 | `diagrams/FIG-3.1.mmd` | Referenced |
| 3.2 | Voice Interaction: TTS playback, then the combined STT + speaker-verification call | diagram | G3 | `diagrams/FIG-3.2.mmd` | Referenced |
| 3.3 | Get Hint sequence, with the Gemini-deployed-versus-Gemma-not-deployed contrast | diagram | G3 | `diagrams/FIG-3.3.mmd` | Referenced |
| 3.4 | RAG pipeline used in answer grading — the genuine RAG in this system, per T2 | diagram | G3 | `diagrams/FIG-3.4.mmd` | Referenced |
| 3.5 | Hint output bubble in the Room chat panel | screenshot | G3 | `images/FIG-3-5.png` | Pending capture |
| 4.1 | Enroll Face Biometric sequence, including upsert/re-enrolment and cross-account bonus check | diagram | G4 | `diagrams/FIG-4.1.mmd` | Referenced |
| 4.2 | Enroll Voice Biometric sequence | diagram | G4 | `diagrams/FIG-4.2.mmd` | Referenced |
| 4.3 | Check Biometric Enrollment sequence | diagram | G4 | `diagrams/FIG-4.3.mmd` | Referenced |
| 4.4 | Face enrolment capture screen | screenshot | G4 | `images/FIG-4-4.png` | Pending capture |
| 4.5 | Voice enrolment recording screen | screenshot | G4 | `images/FIG-4-5.png` | Pending capture |
| 4.6 | Biometric wizard guide screen showing enrolled status for both modalities | screenshot | G4 | `images/FIG-4-6.png` | Pending capture |
| 5.1 | Verify Face Entry Gate sequence, including the authoritative server re-poll on failure | diagram | G5 | `diagrams/FIG-5.1.mmd` | Referenced |
| 5.2 | Verify Face In-Session sequence | diagram | G5 | `diagrams/FIG-5.2.mmd` | Referenced |
| 5.3 | Verify Voice Answer sequence, including the parallel transcription/verification split | diagram | G5 | `diagrams/FIG-5.3.mmd` | Referenced |
| 5.4 | Report Camera Violation sequence | diagram | G5 | `diagrams/FIG-5.4.mmd` | Referenced |
| 5.5 | Tab-Switch / Paste Monitoring sequence, showing both strike shapes | diagram | G5 | `diagrams/FIG-5.5.mmd` | Referenced |
| 5.6 | Strike accumulation and termination decision flowchart, shared by every in-session strike source | diagram | G5 | `diagrams/FIG-5.6.mmd` | Referenced |
| 5.7 | Entry-gate attempt lifecycle state diagram | diagram | G5 | `diagrams/FIG-5.7.mmd` | Referenced |
| 5.8 | Entry-gate verification failure | screenshot | G5 | `images/FIG-5-8.png` | Pending capture |
| 5.9 | Interview room with camera panel active | screenshot | G5 | `images/FIG-5-9.png` | Pending capture |
| 5.10 | Violation/strike notification | screenshot | G5 | `images/FIG-5-10.png` | Pending capture |
| 6.1 | View Evaluation and Reporting: read path, generation path, and recovery hooks | diagram | G6 | `diagrams/FIG-6.1.mmd` | Referenced |
| 6.2 | Five lifecycle states of the evaluation report | diagram | G6 | `diagrams/FIG-6.2.mmd` | Referenced |
| 6.3 | Evaluation report page, Scored state | screenshot | G6 | `images/FIG-6-3.png` | Pending capture |
| 7.1 | System context — trust boundaries across WebClient, Gateway, owned and teammate-owned services, data and external AI | diagram | Cross-cutting | `diagrams/FIG-7.1.mmd` | Referenced |
| 7.2 | Consolidated ER diagram — `DevPrep_Interview` and `DevPrep_Evaluation`, labelled by database | diagram | Cross-cutting | `diagrams/FIG-7.2.mmd` | Referenced |

**Total: 39 figures** — 28 diagrams (Class A, all generated and embedded inline in their chapter),
11 screenshots (Class B, specified in `SCREENSHOT_CHECKLIST.md`, pending capture by the report's
author). Every diagram is referenced in its chapter's body text at the point its finding is
discussed, not merely listed in a trailing "Figures" section — the §12 "Figures" line at the end of
each feature is a summary pointer, not the only reference.
