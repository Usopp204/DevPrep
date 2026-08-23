# Screenshot Checklist

One row per required screenshot. Capture at the browser's default zoom, full page (not just the
visible viewport, where the page scrolls). Save as PNG using exactly the filename in `Save as`,
into `docs/report/images/`. Rows are grouped by chapter/batch in the order features are documented.

## G1 — Session Configuration

| Fig ID | Caption | Route / URL | Precondition state to set up | What must be visible | Save as |
|---|---|---|---|---|---|
| 1.5 | Session configuration form (dashboard) | `/Interview/Index` (WebClient) | Logged in as a candidate with a known credit balance; no active session in progress | Job Role select, Experience Level segmented control, JD paste/upload widget, Duration select showing per-option credit cost with the current balance line, Focus Areas chip control | `FIG-1-5.png` |
| 1.6 | Integrity policy consent dialog | `/Interview/IntegrityPolicy/{id}` (WebClient) | A session in `Status="Initialized"` (completed F-01, not yet accepted the policy) | Full rules grid (Video Visibility, Single Participant, No Assistance, Voice Verification, Session Timer), the Violation Policy warning panel ("...maximum of 3 recorded violations"), the unchecked consent checkbox with the disabled "Enter Interview" button | `FIG-1-6.png` |

## G2 — Session Lifecycle & Credit

| Fig ID | Caption | Route / URL | Precondition state to set up | What must be visible | Save as |
|---|---|---|---|---|---|
| 2.7 | Pause overlay covering the chat panel during an active pause | `/Interview/Room/{id}` (WebClient) | A session in `Status="Paused"` (click Pause once during an `InProgress` session) | The pause overlay covering the chat panel, the "Resume" button, and the pause-quota indicator showing pauses/seconds remaining | `FIG-2-7.png` |

## G3 — Interview Room Interaction

| Fig ID | Caption | Route / URL | Precondition state to set up | What must be visible | Save as |
|---|---|---|---|---|---|
| 3.5 | Hint output bubble in the Room chat panel | `/Interview/Room/{id}` (WebClient) | An `InProgress` session on any unanswered question; click the Hint button once | The AI chat bubble showing the 💡-prefixed hint text, and the Hint button now disabled/spent for this question | `FIG-3-5.png` |

## G4 — Biometric Enrolment

| Fig ID | Caption | Route / URL | Precondition state to set up | What must be visible | Save as |
|---|---|---|---|---|---|
| 4.4 | Face enrolment capture screen | `/Profile/Identity` (WebClient), Screen 1 | Neither modality enrolled; click "Get started" from the guide screen with camera permission granted | The camera video feed inside the oval guide, the countdown or scanning state, and the "I'm ready" button | `FIG-4-4.png` |
| 4.5 | Voice enrolment recording screen | `/Profile/Identity` (WebClient), Screen 2 | Face step already completed in this wizard run; on the voice screen mid-recording | The prompt text, the live level-bar visualizer, the running timer, and the "Stop & submit" button | `FIG-4-5.png` |
| 4.6 | Biometric wizard guide screen showing enrolled status | `/Profile/Identity` (WebClient), Screen 0 | Both Face and Voice already enrolled from a previous run | Both step rows showing "Enrolled ✓ — redo replaces it" | `FIG-4-6.png` |

## G5 — Biometric Verification & Integrity Enforcement

| Fig ID | Caption | Route / URL | Precondition state to set up | What must be visible | Save as |
|---|---|---|---|---|---|
| 5.8 | Entry-gate verification failure | `/Interview/Verify/{id}` (WebClient) | A session past Init; deliberately fail the face match once (e.g. cover the camera) | The error oval state, the failure message, "Try again" button, and the "Attempts left" pill showing a reduced count | `FIG-5-8.png` |
| 5.9 | Interview room with camera panel active | `/Interview/Room/{id}` (WebClient) | An `InProgress` session with camera connected | The live camera feed panel, "Camera: On" status, and the chat panel side by side | `FIG-5-9.png` |
| 5.10 | Violation/strike notification | `/Interview/Room/{id}` (WebClient) | Trigger one strike (e.g. paste into the answer box) | The cheating-warning banner/popup showing the strike text and the "(X/Y warnings)" count | `FIG-5-10.png` |

## G6 — Evaluation & Reporting

| Fig ID | Caption | Route / URL | Precondition state to set up | What must be visible | Save as |
|---|---|---|---|---|---|
| 6.3 | Evaluation report page, Scored state | `/Evaluation/Report?sessionId={id}` (WebClient) | A `Completed` session with all substantive answers graded | The score gauge, Performance by Category cards (Technical/Communication/Problem Solving), grade chip, and the question-by-question breakdown | `FIG-6-3.png` |

## Diagnostic capture — not a UI screenshot, for `OPEN_QUESTIONS.md` #18

| Fig ID | Caption | Route / URL | Precondition state to set up | What must be captured | Save as |
|---|---|---|---|---|---|
| DIAG-1 | Voice embedding dimensionality (`Dim`) — closes the one remaining `[UNVERIFIED]` numeric fact in this report | Browser DevTools Network tab, or SSMS | Enroll voice once (`/Profile/Identity`, voice step) while watching the Network tab for the `POST api/profiles/me/biometrics/voice` request, **or** query `SELECT Dim FROM ProfileBiometrics WHERE Modality='Voice'` directly in SSMS against `DevPrep_Interview` | The JSON response body's `dim` field (proxy path) or the `Dim` column value (SQL path) — either source gives the same number, since `ProfileBiometricsService.EnrollAsync` stores `result.Dim` verbatim (`ProfileBiometricsService.cs:66`) | `DIAG-1-voice-embedding-dim.png` (screenshot) or a plain text note with the number, either is sufficient |
