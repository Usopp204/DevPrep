# 00 — Evidence Map

Status: **Phase 0 output — survey only.** No feature chapters have been written. This document is
the citable foundation for Phases 1–7. All paths are relative to the repository root (`Q:\DevPrep`).
Every factual claim below carries a `file:line` citation gathered by direct reading of the source
tree on the date this document was produced. Claims that could not be grounded in code are marked
`[UNVERIFIED: reason]` and are also logged in `OPEN_QUESTIONS.md`.

**Premise conflict flagged up front (per instruction #7):** the mission brief and `CLAUDE.md` both
reference `contracts/ai-service.md` as the ai-service internal API contract. **This file does not
exist in the repository.** Confirmed by `Glob contracts/**` and a repo-wide filename search — the
only files under `contracts/` are `contracts/README.md`, `contracts/auth/*.cs`,
`contracts/feedback/*.schema.json`, `contracts/interview/score-v2.schema.json`. Logged in
`OPEN_QUESTIONS.md` §1. All ai-service endpoint facts below come from reading the actual router
code, not from a contract document.

---

## A. Repository map — four owned components

### A.1 InterviewAPI (`source/InterviewAPI/`, .NET 8, port 7005)

```
InterviewAPI/
├── Program.cs                     ← entry point, DI registration, middleware pipeline
├── Controllers/                   ← 7 controllers (see §B.1)
├── Services/                      ← SessionService, InterviewService, BiometricVerificationService,
│                                     ProfileBiometricsService, SessionInitExceptions
│   └── Grading/                   ← BoundedGradingQueue, GradingBackgroundService (IHostedService),
│                                     GradingQueueOptions, GradingWorkItem, IGradingQueue
├── Repositories/                  ← InterviewRepository, SessionRepository (atomic ExecuteUpdateAsync
│                                     guards), ProfileBiometricsRepository,
│                                     SessionVerificationChallengeRepository/EventRepository
├── Models/                        ← InterviewDbContext.cs + InterviewDbContext.Biometrics.cs (partial),
│                                     ChatMessage, InterviewRoom, InterviewSession, ProfileBiometric,
│                                     SessionQuestion, SessionVerificationChallenge/Event, UserRoomTourStatus
├── Clients/                       ← AiServiceClient, AuthApiClient, UserApiClient, PaymentApiClient,
│                                     EvaluationApiClient (+ interfaces, exception types)
├── Helpers/                       ← BiometricSimilarity, CheatingStrikeHelper, ContentModerationHelper,
│                                     CreditConfig, DemoSession, EmbeddingCodec, FusionDecisionHelper,
│                                     ProfanityWordList, SessionCapabilityPolicy, SubstantiveAnswerRules,
│                                     VietnamTime
├── Http/ResiliencePolicies.cs     ← Polly circuit breakers (ai-service, PaymentAPI)
├── DTOs/                          ← request/response shapes, grouped by AiService/Biometrics/Evaluation/Session
├── Sql/                           ← 2 incremental migration scripts (NOT auto-applied by db-init)
└── appsettings.json / .Development.json
```
Entry point: `source/InterviewAPI/Program.cs`. DI registrations: `Program.cs:34-120`. DbContext:
`source/InterviewAPI/Models/InterviewDbContext.cs` (+ partial `InterviewDbContext.Biometrics.cs`).
Config files: `appsettings.json`, `appsettings.Development.json`, plus `.env`-sourced keys (see §E).

### A.2 EvaluationAPI (`source/EvaluationAPI/`, .NET 8, port 7006)

```
EvaluationAPI/
├── Program.cs                     ← entry point, DI registration, middleware pipeline
├── Controllers/EvaluationController.cs   ← single controller, 4 actions
├── Services/                      ← EvaluationService (grading orchestration, report aggregation),
│                                     GradingExceptions, PerformanceReportExceptions
├── Repositories/EvaluationRepository.cs  ← transactional idempotent report creation
├── Data/EvaluationDbContext.cs    ← 3 DbSets
├── Models/                        ← AnswerEvaluation, FeedbackReport, PerformanceMetric (no UserId column
│                                     on any of the three — verified)
├── Clients/                       ← AiServiceClient (grading/report), InterviewApiClient (reverse call:
│                                     Evaluation → Interview, for ownership/transcript/regrade)
├── Http/ResiliencePolicies.cs     ← breaker-only for ai-service, retry+breaker for InterviewAPI
└── appsettings.json / .Development.json
```
Entry point: `source/EvaluationAPI/Program.cs`. DI registrations: `Program.cs:33-142`. DbContext:
`source/EvaluationAPI/Data/EvaluationDbContext.cs`.

### A.3 ai-service (`ai-service/`, FastAPI/Python, internal-only, container port 8000 / host 8080)

```
ai-service/
├── app/
│   ├── main.py                    ← entry point, router registration, CORS, lifespan warm-up, /health, /ui
│   ├── core/
│   │   ├── config.py              ← Settings (pydantic-settings, prefix AI_) + module-level os.getenv keys
│   │   └── model_manager.py       ← CV/JD parser LLM loader (AutoModelForCausalLM)
│   ├── api/
│   │   ├── interview.py           ← /interview/* (stateful session, in-memory)
│   │   ├── evaluation.py          ← /evaluate
│   │   ├── catalog.py             ← /catalog
│   │   └── internal.py            ← /internal/* (.NET-facing: questions/plan, evaluate-answer, report,
│   │                                  stt, hint, questions/index/*) — manual per-endpoint key check
│   ├── cv/, jd/, match/           ← CV/JD parsing + matching (out of the 19-feature scope, unauthenticated)
│   ├── services/
│   │   ├── interviewer.py         ← Gemma-2B LoRA chat model (GemmaChat), interview state machine
│   │   ├── evaluator.py           ← GeminiJudge (grading, summary), self-consistency aggregation
│   │   ├── chroma_store.py        ← RAG: ChromaDB persistent client, BAAI/bge-m3 embeddings
│   │   └── voice.py               ← TTS (Piper local / ElevenLabs cloud), STT (Deepgram cloud)
│   └── schemas/                   ← Pydantic request/response models
├── biometrics/
│   ├── router.py                  ← /internal/biometrics/* — router-level require_internal_api_key dependency
│   ├── security.py                ← require_internal_api_key (fail-open if INTERNAL_API_KEY empty)
│   ├── config.py                  ← thresholds (FACE_MATCH_THRESHOLD, VOICE_MATCH_THRESHOLD, liveness gates)
│   ├── face_engine.py             ← InsightFace (buffalo_l / ArcFace r50) wrapper
│   ├── voice_engine.py            ← SpeechBrain ECAPA-TDNN wrapper
│   ├── liveness_face.py           ← head-turn/yaw liveness (COMPUTED BUT NOT ENFORCED — see §F)
│   ├── liveness_voice.py          ← transcript-vs-challenge-phrase similarity gate
│   ├── calibrate.py               ← offline threshold calibration tool (scikit-learn LogisticRegression)
│   └── EVAL.md                    ← explicit warning against fabricating calibration data
├── models/                        ← local checkpoints: ecapa-voxceleb/, gemma-2b-devprep-sft/ (LoRA),
│                                     insightface/ (buffalo_l), piper/ (2 .onnx voices)
├── training/train_kaggle.py       ← fine-tune/push script for the CV-parser model
├── test-ui/                       ← empty directory (mounted at /ui if present)
├── requirements.txt
└── .env / Dockerfile
```
Entry point: `ai-service/app/main.py`. Router registration: `main.py:53-60`. `ai-service/README.md`
is stale — claims "only exposes `/health`" (`README.md:7`) which contradicts the 20+ endpoints
actually present (see §B.3, and §F contradiction list).

### A.4 WebClient (`source/WebClient/`, ASP.NET Core MVC + Razor, port 5130)

```
WebClient/
├── Program.cs                     ← conventional routing only: {controller}/{action}/{id?} (Program.cs:72-74)
├── Controllers/
│   ├── Interview/InterviewController.cs, VerificationController.cs
│   ├── Evaluation/EvaluationController.cs
│   └── Profile/ProfileController.cs   ← biometric enrollment actions (EnrollFace/EnrollVoice) live here
├── Services/
│   ├── Interview/InterviewService.cs, VerificationService.cs   ← typed Gateway HTTP clients
│   ├── Evaluation/EvaluationService.cs
│   ├── Profile/BiometricsService.cs
│   └── Shared/ApiClient.cs, ApiGatewayOptions.cs, JwtDelegatingHandler.cs  ← attaches/refreshes Bearer JWT
├── Views/
│   ├── Interview/{Index,Verify,IntegrityPolicy,Room,History,Details}.cshtml
│   ├── Evaluation/{Index,Report}.cshtml
│   └── Profile/Identity.cshtml    ← biometric enrollment wizard shell
└── wwwroot/js/pages/
    ├── dashboard.js                ← session-config widgets, catalog fetch, recent-sessions feed
    ├── entry-verify.js             ← entry-gate face-check camera capture
    ├── biometric-wizard.js         ← enrollment (face+voice) wizard, also reused for session verify
    ├── interview-room.js           ← 1997 lines: timer, chat, voice, camera, anti-cheat, pause — the
    │                                  single largest client-side file in the four owned components
    └── content-filter.js           ← client-side mirror of ContentModerationHelper (UX only)
```
Entry point / routing: `source/WebClient/Program.cs:72-74`. **No controller in scope carries
`[Route]` or `[Authorize]`** (confirmed by grep) — every WebClient-internal URL is the ASP.NET
default `{controller}/{action}/{id?}`, and authentication is enforced either by manual
`Request.Cookies["access_token"]` checks (e.g. `Controllers/Profile/ProfileController.cs:88,125,136,217,237`)
or by catching a 401 propagated from the Gateway/backend (`Controllers/Shared/CandidateAuthenticationRedirect.cs:9-13`).

---

## B. Endpoint inventory

### B.1 InterviewAPI (`source/InterviewAPI/Controllers/`)

**AdminInterviewController** — `[Route("api/admin/interviews")]`, `[Authorize(Roles="Admin,Manager")]` (`AdminInterviewController.cs:14-17`)
| Method | Route | file:line | Auth | Called by |
|---|---|---|---|---|
| GET | `api/admin/interviews/statistics/dashboard` | `:27` | class (Admin,Manager) | Admin dashboard UI |
| GET | `api/admin/interviews` | `:44` | class | Admin session list |
| GET | `api/admin/interviews/{id:guid}` | `:52` | class | Admin session detail |
| DELETE | `api/admin/interviews/{id:guid}` | `:60-62` | `Roles="Admin"` (method override) | Admin soft-delete |
| POST | `api/admin/interviews/{id:guid}/restore` | `:70-72` | `Roles="Admin"` | Admin restore |

**InternalInterviewActivityController** — `[Route("api/internal/interviews")]`, no `[Authorize]` (`:7-9`)
| Method | Route | file:line | Auth | Called by |
|---|---|---|---|---|
| POST | `api/internal/interviews/last-activity` | `:20-39` | none | UserAPI (cross-service, internal) |

**InternalInterviewController** — `[Route("api/internal/interviews")]`, no `[Authorize]` (`:7-9`)
| Method | Route | file:line | Auth | Called by |
|---|---|---|---|---|
| GET | `api/internal/interviews/sessions?userId=` | `:19-55` | none | UserAPI (cross-service, internal) |

**InterviewSessionController** — `[Route("api/interview/sessions")]`, `[Authorize]` (`:12-15`)
| Method | Route | file:line | Auth | Called by |
|---|---|---|---|---|
| GET | `api/interview/catalog` | `:38-53` | class | WebClient dashboard.js |
| POST | `api/interview/room-tour/seen` | `:59-75` | class | WebClient Room |
| GET | `api/interview/sessions` | `:78-94` | class | WebClient |
| GET | `api/interview/sessions/active` | `:102-108` | class | WebClient (resume banner) |
| GET | `api/interview/sessions/history` | `:110-129` | `Roles="User"` | WebClient History |
| GET | `api/interview/sessions/{id}/chat-log` | `:130-150` | `Roles="User"` | WebClient Details |
| GET | `api/interview/sessions/feedback-eligibility` | `:152-161` | `Roles="User"` | WebClient |
| GET | `api/interview/sessions/{id}/feedback-eligibility` | `:163-179` | `Roles="User"` | WebClient, EvaluationAPI (cross-service) |
| GET | `api/interview/sessions/{id}/transcript` | `:181-197` | class | WebClient Evaluation Report; EvaluationAPI (cross-service) |
| GET | `api/interview/sessions/{id}/owns` | `:203-211` | class | EvaluationAPI (IDOR check, cross-service) |
| POST | `api/interview/sessions/{id}/regrade-missing` | `:227-248` | class | EvaluationAPI (cross-service recovery) |
| GET | `api/interview/sessions/{id}/integrity-policy` | `:250-263` | class | WebClient |
| POST | `api/interview/sessions/{id}/integrity-policy/accept` | `:265-282` | class | WebClient |
| GET | `api/interview/sessions/{id}/timer` | `:284-301` | class | (polled client-side, not directly cited in WebClient survey) |
| POST | `api/interview/sessions/{id}/track-time` | `:304-321` | class | WebClient Room |
| POST | `api/interview/sessions/{id}/complete` | `:324-341` | `Roles="User"` | WebClient |
| POST | `api/interview/sessions/{id}/end` | `:344-361` | `Roles="User"` | WebClient |
| POST | `api/interview/sessions/{id}/pause` | `:364-380` | class | WebClient |
| POST | `api/interview/sessions/{id}/unpause` | `:383-399` | class | WebClient |
| POST | `api/interview/sessions/{id}/camera-violation` | `:404-421` | class | WebClient |
| GET | `api/interview/sessions/{id}/resume` | `:424-448` | class | WebClient Room |
| DELETE | `api/interview/sessions/{id}` | `:451-475` | class | WebClient History |
| PUT | `api/interview/sessions/config/duration` | `:478-509` | class | WebClient Start |
| PUT | `api/interview/sessions/config/level` | `:512-541` | class | WebClient Start |
| PUT | `api/interview/sessions/config/interaction` | `:544-573` | class | WebClient Start |
| POST | `api/interview/sessions/{id}/message` | `:579-603` | class | WebClient Room |
| POST | `api/interview/sessions/{id}/questions/generate` | `:609-641` | class | WebClient Room |
| POST | `api/interview/sessions/tts` | `:647-667` | class | WebClient Room |
| POST | `api/interview/sessions/stt` | `:673-697` | class | WebClient Room |
| POST | `api/interview/sessions/{id}/hint` | `:704-734` | class | WebClient Room |
| POST | `api/interview/sessions/init` | `:740-767` | class | WebClient Start |
| GET | `api/interview/sessions/credit-balance` | `:774-799` | class | WebClient |

**ManagerInterviewSessionsController** — `[Route("api/admin/manager/sessions")]`, `[Authorize(Roles="Manager")]` (`:8-11`)
| Method | Route | file:line |
|---|---|---|
| GET | `api/admin/manager/sessions` | `:20-25` |
| GET | `api/admin/manager/sessions/{id}/log` | `:27-37` |

**ProfileBiometricsController** — `[Route("api/profiles/me/biometrics")]`, `[Authorize]` (`:12-15`)
| Method | Route | file:line |
|---|---|---|
| GET | `api/profiles/me/biometrics` | `:29-45` |
| POST | `api/profiles/me/biometrics/face` | `:48-50` |
| POST | `api/profiles/me/biometrics/voice` | `:53-55` |

**SessionVerificationController** — `[Route("api/interview/sessions/{sessionId}/verify")]`, `[Authorize]` (`:13-16`)
| Method | Route | file:line |
|---|---|---|
| GET | `.../verify/in-session-check?sessionQuestionId=` | `:38-55` |
| GET | `.../verify/challenge?modality=` | `:60-86` |
| POST | `.../verify/face?phase=Entry\|InSession` | `:92-118` |
| POST | `.../verify/voice?phase=Entry\|InSession` | `:123-149` |
| GET | `.../verify/entry-gate` | `:155-174` |
| POST | `.../verify/in-session` | `:185-208` |
| POST | `.../verify/voice-answer` | `:214-238` |
| POST | `.../verify/tab-event` | `:254-275` |

### B.2 EvaluationAPI (`source/EvaluationAPI/Controllers/EvaluationController.cs`)
`[Route("api/evaluation/sessions")]`, `[Authorize]` class-level (`:14-15`)
| Method | Route | file:line | Auth | Called by |
|---|---|---|---|---|
| POST | `api/evaluation/sessions/{id}/answers/{answerId}` | `:44-49` | class | InterviewAPI GradingBackgroundService (cross-service) |
| GET | `api/evaluation/sessions/{sessionId}/performance` | `:109-111` | `Roles="User"` | WebClient EvaluationService |
| POST | `api/evaluation/sessions/{sessionId}/report` | `:153-156` | `Roles="User"` | WebClient EvaluationService |
| GET | `api/evaluation/sessions/{id}/answers` | `:213-215` | class | WebClient EvaluationService |

### B.3 ai-service (`ai-service/app/`, `ai-service/biometrics/`)

| Method | Route | file:line | Auth | Feature group |
|---|---|---|---|---|
| POST | `/internal/questions/plan` | `app/api/internal.py:50` | manual key check | G1 (Init) |
| POST | `/internal/questions/index/upsert` | `internal.py:213` | manual key check | RAG maintenance |
| DELETE | `/internal/questions/index/{question_id}` | `internal.py:231` | manual key check | RAG maintenance |
| DELETE | `/internal/questions/index` | `internal.py:238` | manual key check | RAG maintenance |
| GET | `/catalog` | `app/api/catalog.py:20` | **none** | G1 |
| POST | `/interview/start` | `app/api/interview.py:73` | **none** | (stateful, superseded by .NET-side flow) |
| POST | `/interview/{session_id}/answer` | `interview.py:87` | **none** | |
| POST | `/interview/{session_id}/voice/answer` | `interview.py:95` | **none** | |
| POST | `/interview/{session_id}/hint` | `interview.py:105` | **none** | |
| POST | `/interview/{session_id}/skip` | `interview.py:110` | **none** | |
| POST | `/interview/{session_id}/end` | `interview.py:118` | **none** | |
| POST | `/interview/{session_id}/report` | `interview.py:126` | **none** | |
| GET | `/interview/{session_id}/transcript` | `interview.py:145` | **none** | |
| POST | `/interview/tts` | `interview.py:162` | **none** | G3 (Voice) |
| POST | `/evaluate` | `app/api/evaluation.py:17` | **none** | |
| POST | `/internal/evaluate-answer` | `internal.py:85` | manual key check | G3 (Send Message → grading) |
| POST | `/internal/report` | `internal.py:126` | manual key check | G6 |
| POST | `/internal/stt` | `internal.py:149` | manual key check | G3 (Voice) |
| POST | `/internal/hint` | `internal.py:182` | manual key check | G3 (Get Hint) |
| POST | `/internal/biometrics/face/embed` | `biometrics/router.py:50` | router-level `require_internal_api_key` | G4 |
| POST | `/internal/biometrics/voice/embed` | `router.py:59` | router-level | G4 |
| POST | `/internal/biometrics/verify` (modality=face\|voice) | `router.py:80` | router-level | G5 |
| POST | `/internal/biometrics/face/verify-live` | `router.py:118` | router-level | G5 |
| POST | `/internal/biometrics/voice/verify-challenge` | `router.py:151` | router-level | G5 |
| POST | `/parse/cv`, `/parse/jd`, `/parse/jd-file`, `/match` | `app/cv/router.py:26`, `app/jd/router.py:21,30`, `app/match/router.py:15` | **none** | out of 19-feature scope |
| GET | `/health` | `app/main.py:64` | **none** | liveness probe |

**Auth mechanism detail**: two independent implementations of the `X-Internal-Api-Key` check exist —
`biometrics/security.py:13-15` (`require_internal_api_key`, router-level dependency) and
`app/api/internal.py:21-24` (`_check_key`, called manually inside each `/internal/*` function body).
Both **fail open**: if `INTERNAL_API_KEY` is empty/unset, the check passes unconditionally
(`internal.py:23`, `security.py:14`). The `/interview/*`, `/evaluate`, `/catalog`, `/parse/*`,
`/match` routers have **no auth check at all** — confirmed by reading each router file in full, no
`Header`/`X-Internal-Api-Key` reference anywhere in them.

---

## C. Feature → artefact matrix

| # | Feature | WebClient controller/view/JS | Backend controller/service | ai-service router | DB table(s) | Gateway route |
|---|---|---|---|---|---|---|
| G1-1 | Configure Interview Session | `Controllers/Interview/InterviewController.cs:42-45,71-117` (Index/Start); `Views/Interview/Index.cshtml`; `wwwroot/js/pages/dashboard.js` | `InterviewSessionController.cs:740-767` (Init); `SessionService.InitAsync` (`SessionService.cs:478`) | `POST /internal/questions/plan` (`internal.py:50`) | `InterviewSessions`, `SessionQuestions` | `interview-route` (`ApiGateway/appsettings.json:67`) |
| G1-2 | Configure Session Duration | part of `Start` above | `InterviewSessionController.cs:478-509` (ConfigDuration); `SessionService.ConfigDurationAsync` (`SessionService.cs:365`); `Helpers/CreditConfig.cs:16-28` | — | `InterviewSessions.DurationMinutes` | `interview-route` |
| G1-3 | Configure Level | part of `Start` above | `InterviewSessionController.cs:512-541` (ConfigLevel); `SessionService.ConfigLevelAsync` (`:410`) | — | `InterviewSessions.Level` | `interview-route` |
| G1-4 | Accept Integrity Policy | `InterviewController.cs:166-201` (IntegrityPolicy/AcceptIntegrityPolicy); `Views/Interview/IntegrityPolicy.cshtml` | `InterviewSessionController.cs:250-282`; `SessionService.GetIntegrityPolicyStatusAsync/AcceptIntegrityPolicyAsync` (`:1995,2008`) | — | `InterviewSessions.IntegrityPolicyAccepted(At)` | `interview-route` |
| G2-5 | Start Interview & Charge Credit | `InterviewController.cs:71-117` (Start) | `InterviewSessionController.cs:609-641` (GenerateQuestion, first-entry deduction per comment at `SessionService.cs:272-278`); `Clients/PaymentApiClient.cs:31` (`DeductCreditAsync` → PaymentAPI `POST /api/internal/credit/deduct`, integration only, PaymentAPI is a stub per project docs) | — | `InterviewSessions.CreditCost` | `interview-route` |
| G2-6 | Pause / Resume | `InterviewController.cs:678-719` (Pause/Unpause); `Views/Interview/Room.cshtml` pause overlay; `interview-room.js:285-322` | `InterviewSessionController.cs:364-399`; `SessionService.PauseSessionAsync/UnpauseSessionAsync` (`:1290,1318`) | — | `InterviewSessions.{PausedAt,PauseCount,TotalPausedSeconds}` | `interview-route` |
| G2-7 | Resume Active Session | `InterviewController.cs:207-300` (Room, calls `GetRoomAsync`) | `InterviewSessionController.cs:424-448` (ResumeSession); `SessionService.ResumeSessionAsync` (`:1444`) | — | `InterviewSessions`, `SessionQuestions`, `ChatMessages` | `interview-route` |
| G2-8 | End Session | `InterviewController.cs:652-676` (CompleteSession/EndSession) | `InterviewSessionController.cs:324-361`; `SessionService.CompleteSessionAsync/EndSessionAsync` (`:1220,1259`) | — | `InterviewSessions.{Status,EndedAt,TerminationReason}` | `interview-route` |
| G3-9 | Send Message / Answer | `InterviewController.cs:559-577` (Message); `interview-room.js:1076-1133` (`sendAnswer`) | `InterviewSessionController.cs:579-603`; `SessionService.SendMessageAsync` (`:803`); `Services/Grading/GradingBackgroundService.cs` → `Clients/EvaluationApiClient.cs:27` | `POST /internal/evaluate-answer` (`internal.py:85`), via EvaluationAPI `EvaluationController.cs:44-49` | `ChatMessages`, `AnswerEvaluations` (Evaluation DB) | `interview-route`, `evaluation-route` |
| G3-10 | Voice Interaction (TTS+STT) | `InterviewController.cs:579-627` (Tts/Stt); `interview-room.js:1334-1408,1907-1944` | `InterviewSessionController.cs:647-697`; `Clients/AiServiceClient.cs:59,91` | `POST /interview/tts` (`interview.py:162`), `POST /internal/stt` (`internal.py:149`) | — (audio not persisted per schema found) | `interview-route` |
| G3-11 | Get Hint | `InterviewController.cs:629-650` (Hint); `interview-room.js:1295-1325` | `InterviewSessionController.cs:704-734`; `SessionService.HintAsync` (`:2031`) | `POST /internal/hint` (`internal.py:182`) | `SessionQuestions.HintUsedAt` | `interview-route` |
| G4-12 | Enroll Face Biometric | `Controllers/Profile/ProfileController.cs:165-187` (EnrollFace); `Views/Profile/Identity.cshtml`; `wwwroot/js/pages/biometric-wizard.js:337-419` | `ProfileBiometricsController.cs:48-50`; `ProfileBiometricsService.EnrollAsync` (`:44`); `Clients/AiServiceClient.cs:120` | `POST /internal/biometrics/face/embed` (`biometrics/router.py:50`) | `ProfileBiometrics` (Modality='Face') | `profiles-biometrics-route` (`ApiGateway/appsettings.json:81`, cluster `interview-cluster`) |
| G4-13 | Enroll Voice Biometric | `ProfileController.cs:190-212` (EnrollVoice); `biometric-wizard.js:491-550,570-589` | `ProfileBiometricsController.cs:53-55`; `ProfileBiometricsService.EnrollAsync` | `POST /internal/biometrics/voice/embed` (`biometrics/router.py:59`) | `ProfileBiometrics` (Modality='Voice') | `profiles-biometrics-route` |
| G4-14 | Check Biometric Enrollment | `ProfileController.cs:133-161` (Identity); `Services/Profile/BiometricsService.cs:25-37` | `ProfileBiometricsController.cs:29-45` (GetStatus); `ProfileBiometricsService.GetStatusAsync` (`:198`) | — | `ProfileBiometrics` | `profiles-biometrics-root-route` |
| G5-15 | Verify Face (Entry Gate) | `InterviewController.cs:120-153` (Verify/VerifyFailed); `Controllers/Interview/VerificationController.cs:44-89`; `Views/Interview/Verify.cshtml`; `wwwroot/js/pages/entry-verify.js` | `SessionVerificationController.cs:92-118,155-174` (VerifyFace phase=Entry, GetEntryGateStatus); `BiometricVerificationService.VerifyFaceAsync/GetEntryGateStatusAsync` (`:128,224`) | `POST /internal/biometrics/face/verify-live` (`biometrics/router.py:118`) | `ProfileBiometrics`, `SessionVerificationEvent`, `SessionVerificationChallenge` | `interview-route` |
| G5-16 | Verify Face (In-Session) | `VerificationController.cs:93-118` (VerifyInSession); `interview-room.js:1757-1899` | `SessionVerificationController.cs:185-208`; `BiometricVerificationService.VerifyInSessionAsync` (`:300`) | `POST /internal/biometrics/face/verify-live` (reused) | `SessionVerificationEvent` | `interview-route` |
| G5-17 | Verify Voice Answer | `InterviewController.cs:597-627` (Stt, forwards to voice-answer verify); `VerificationController.cs:143-164` | `SessionVerificationController.cs:214-238`; `BiometricVerificationService.VerifyVoiceAnswerAsync` (`:454`) | `POST /internal/biometrics/voice/verify-challenge` (`biometrics/router.py:151`) | `SessionVerificationEvent` | `interview-route` |
| G5-18 | Report Camera Violation | `InterviewController.cs:692-705`; `interview-room.js:1445-1528` | `InterviewSessionController.cs:404-421`; `SessionService.ReportCameraViolationAsync` (`:1347`) | — | `InterviewSessions.CameraViolationCount` | `interview-route` |
| G6-19 | View Evaluation & Reporting | `Controllers/Evaluation/EvaluationController.cs:22-94` (Report); `Views/Evaluation/Report.cshtml`; `Services/Evaluation/EvaluationService.cs` | EvaluationAPI `EvaluationController.cs:109-215` (GetPerformance/EnsureReport/GetAnswerEvaluations); `EvaluationService.cs:226-464` | `POST /internal/report` (`internal.py:126`) | `AnswerEvaluations`, `FeedbackReports`, `PerformanceMetrics` (all Evaluation DB) | `evaluation-route` |

Every feature in the 19-item inventory was located; none are `NOT LOCATED`. Note that "tab-switch /
paste anti-cheat" (`SessionVerificationController.cs:254-275`, `RecordTabEvent`) exists as
additional integrity-enforcement machinery beyond the four G5 features named in the brief — it will
be documented as a sub-mechanism of G5 in Phase 5 rather than as a 20th feature, unless instructed
otherwise.

---

## D. AI/ML asset inventory

| Model | Library / class | Identifier | Load site | Local or Hub | Threshold / config |
|---|---|---|---|---|---|
| CV/JD parser LLM | `transformers.AutoModelForCausalLM` (+ `BitsAndBytesConfig` 4-bit) | `AI_MODEL_ID` env, actual value `DuongNTCE/devprep-cv-parser` (`.env:19`) | `ai-service/app/core/model_manager.py:39,50-77` | HF Hub (private, requires `HF_TOKEN`) | none (generative JSON extraction) |
| Interviewer conversational LLM | `transformers.AutoModelForCausalLM` + `peft` LoRA | Local dir `GEMMA_MODEL_DIR` (default `/app/models/gemma-2b-devprep-sft`), LoRA base `google/gemma-2b-it` (`adapter_config.json:6`) | `ai-service/app/services/interviewer.py:288,294-296` | Local adapter + Hub base | sampling only: `temperature=0.7, top_p=0.9` (`interviewer.py:299,321-326`) |
| Judge LLM (grading, hint, summary) | `google.genai.Client` | `GEMINI_JUDGE_MODEL` env, code default `gemini-3.1-pro` (`config.py:32`), **actual `.env` value `gemini-3.5-flash-lite`** (`.env:33`) | `ai-service/app/services/evaluator.py:175-181` | Cloud API (Google) | `SELF_CONSISTENCY_RUNS` default 1 (`evaluator.py:31`), actual `.env` value 3 |
| RAG embedding model | `chromadb.utils.embedding_functions.SentenceTransformerEmbeddingFunction` | `EMBED_MODEL` env, value `BAAI/bge-m3` (`chroma_store.py:26`; `infra/docker-compose.yml:141`) | `ai-service/app/services/chroma_store.py:34` | HF Hub | `NEAR_DUP_THRESHOLD=0.92` (`chroma_store.py:31`), retrieval `k=2` default (`chroma_store.py:288`, `evaluator.py:487`) |
| Face biometrics | `insightface.app.FaceAnalysis` | `FACE_MODEL_PACK=buffalo_l` (ArcFace ResNet-50) (`biometrics/config.py:6`) | `ai-service/biometrics/face_engine.py:22-25` | Local (`ai-service/models/insightface/`) | `FACE_MATCH_THRESHOLD=0.35` (`config.py:15`); det/blur/ratio gates `config.py:19-21`; liveness gates `config.py:26-28` (**computed, not enforced** — see §F) |
| Voice biometrics | `speechbrain.inference.speaker.EncoderClassifier` | `VOICE_MODEL_SOURCE=speechbrain/spkrec-ecapa-voxceleb` (ECAPA-TDNN) (`biometrics/config.py:8`) | `ai-service/biometrics/voice_engine.py:22-27` | Local cache (`ai-service/models/ecapa-voxceleb/`) | `VOICE_MATCH_THRESHOLD=0.25` (`config.py:16`); `VOICE_CONTENT_MIN_SIMILARITY=0.70` (`config.py:29`) |
| TTS (default) | `piper.PiperVoice` | `PIPER_VOICE` env, actual value `en_US-ryan-medium` (`.env:67`) | `ai-service/app/services/voice.py:94-100` | Local (`ai-service/models/piper/*.onnx`) | n/a |
| TTS (alternate) | `elevenlabs.client.ElevenLabs` | `ELEVENLABS_TTS_MODEL` default `eleven_flash_v2_5` (`config.py:60`) | `voice.py:59-70` | Cloud API | selected via `TTS_PROVIDER` (`.env` value `piper`, i.e. not active by default) |
| STT | `deepgram.DeepgramClient` | `DEEPGRAM_STT_MODEL` value `nova-3` (`.env:37`, `config.py:38`) | `ai-service/app/services/voice.py:35-46` | Cloud API | n/a |

**Fusion decision (face+voice combined score, InterviewAPI side)**: `Helpers/FusionDecisionHelper.cs:15-45`
loads `Biometrics:CalibrationJsonPath` (`appsettings.json`, points at `ai-service/models/calibration.json`)
and computes a sigmoid over `bias + wFace*faceScore + wVoice*voiceScore` compared against a
`DecisionThreshold`. **No `calibration.json` file was found under `ai-service/models/` by any
survey agent** — flagged as `[UNVERIFIED: calibration.json existence/contents not directly confirmed]`
in `OPEN_QUESTIONS.md` §2; if absent, `FusionDecisionHelper` presumably falls back to a hardcoded
default (needs direct verification in Phase 5, not assumed here).

**Threshold provenance**: `ai-service/biometrics/EVAL.md:74-77` explicitly warns against fabricating
genuine/impostor scores to make `calibrate.py` "work" and states such output "says nothing about
real accuracy and must never be presented as such or used to change the fixed defaults in
config.py." No `calibration.json` was located, meaning the fixed defaults
(`FACE_MATCH_THRESHOLD=0.35`, `VOICE_MATCH_THRESHOLD=0.25`) are, on current evidence, **heuristic/
uncalibrated**, not EER/FAR-derived. This will be written up honestly in the §8f threshold-provenance
subsection of the relevant G5 feature chapters.

---

## E. Configuration inventory

### InterviewAPI (`source/InterviewAPI/appsettings.json` unless noted)
| Key | Value | Purpose |
|---|---|---|
| `Jwt:Issuer` / `Jwt:Audience` | `DevPrep.AuthAPI` / `DevPrep.Client` | JWT validation, shared with AuthAPI |
| `Jwt:Key` | absent (env-only) | JWT signing key — **secret** |
| `AiService:BaseUrl` | `http://localhost:8080` | ai-service address |
| `AiService:InternalApiKey` | `<redacted>` (plaintext in committed appsettings.json, `:15`) | `X-Internal-Api-Key` header |
| `Biometrics:Enabled` | `true` | feature flag for entire biometric gate |
| `Biometrics:ChallengeTtlSeconds` | `120` | challenge TTL |
| `Biometrics:EntryMaxRetries` | `3` | max failed entry-gate attempts |
| `Biometrics:InSessionStrictness` | `"Strict"` | Strict=ends session on cap, Flag=records only |
| `Biometrics:InSessionMaxStrikes` | `3` | strike cap |
| `Biometrics:InSessionCheckTtlSeconds` | `20` | in-session check-token expiry |
| `Biometrics:TabSwitchEnabled` | `true` | enables tab/paste monitoring |
| `Biometrics:TabSwitchMinAwayMs` | `1500` | debounce for flicker |
| `Biometrics:TabStrikeThreshold` / `NoFaceStrikeThreshold` | `3` / `3` | N events → 1 strike |
| `Biometrics:BonusDedupMatchThreshold` | `0.5` | cross-account face-dedup threshold for enrollment bonus |
| `Biometrics:FaceReEnrollmentMatchThreshold` | `0.35` | re-enrollment identity match |
| `Biometrics:VoiceReEnrollmentMatchThreshold` | `0.25` | re-enrollment identity match |
| `Biometrics:CalibrationJsonPath` | `../../ai-service/models/calibration.json` | fusion-weights file (existence unverified, see §D) |
| env-only | `INTERVIEW_DB_CONNECTION_STRING`, `INTERNAL_API_KEY`, `INTERVIEW_API_PORT`, `SERVICE_BIND_HOST`, `AUTH_API_BASEURL`, `USER_API_BASEURL`, `PAYMENT_API_BASEURL`, `EVALUATION_API_BASEURL`, `DEMO_SESSION_ENABLED`, `Interview:SkipCooldownSeconds` | not in appsettings, `.env`/environment only |

### EvaluationAPI (`source/EvaluationAPI/appsettings.json`)
| Key | Value | Purpose |
|---|---|---|
| `Jwt:Issuer` / `Jwt:Audience` | `DevPrep.AuthAPI` / `DevPrep.Client` | same as InterviewAPI |
| `AiService:BaseUrl` | `http://localhost:8080` | |
| `AiService:InternalApiKey` | `<redacted>` (plaintext, `:15`) | |
| env-only | `EVALUATION_DB_CONNECTION_STRING`, `EVALUATION_API_PORT`, `SERVICE_BIND_HOST`, `INTERVIEW_API_BASEURL`, `Jwt:Key` | |

### ai-service (`ai-service/app/core/config.py`, `biometrics/config.py`, `.env`)
| Key | Purpose | Secret? |
|---|---|---|
| `GEMINI_API_KEY` | Gemini judge auth | secret |
| `GEMINI_JUDGE_MODEL` | model id (`gemini-3.5-flash-lite` actual) | no |
| `DEEPGRAM_API_KEY` | STT auth | secret |
| `DEEPGRAM_STT_MODEL` | model id (`nova-3`) | no |
| `TTS_PROVIDER`, `PIPER_VOICE`, `PIPER_MODEL_DIR`, `PIPER_SPEAKER_ID`, `TTS_CACHE_DIR/ENABLED` | TTS config | no |
| `ELEVENLABS_API_KEY` | alt-TTS auth | secret |
| `ELEVENLABS_VOICE_ID`, `ELEVENLABS_TTS_MODEL` | alt-TTS config | no |
| `INTERNAL_API_KEY` | shared secret for `X-Internal-Api-Key`, **fail-open if empty** | secret |
| `ALLOWED_ORIGINS` | CORS allow-list, actual value `*` with `allow_credentials=True` | no (but a security-relevant combination — flagged in §F) |
| `HF_TOKEN` | Hugging Face Hub auth (private model access) | secret |
| `AI_MODEL_ID`, `GEMMA_MODEL_DIR`, `EMBED_MODEL`, `CHROMA_DIR` | model/RAG paths | no |
| `FACE_MATCH_THRESHOLD`, `VOICE_MATCH_THRESHOLD`, `FACE_MIN_DET_SCORE`, `FACE_MIN_FACE_RATIO`, `FACE_MIN_BLUR_VAR`, `LIVENESS_MIN_FRAMES`, `LIVENESS_MIN_YAW_RANGE_DEG`, `LIVENESS_MAX_IDENTITY_DRIFT`, `VOICE_MIN_DURATION_SEC`, `VOICE_TARGET_SR`, `VOICE_CONTENT_MIN_SIMILARITY` | biometric gates, hardcoded not env-overridable (`biometrics/config.py:19-29`) | no |
| `SELF_CONSISTENCY_RUNS` | grading re-run count, actual value 3 | no |

### ApiGateway (`source/ApiGateway/appsettings*.json`)
| Key | Purpose |
|---|---|
| `Gateway:BodySizeLimitMb` | Kestrel max request body (default 10MB) |
| `Gateway:RequestTimeoutSeconds` / `AiRequestTimeoutSeconds` / `DocumentRequestTimeoutSeconds` | 30s / 90s / 330s default; Dev overrides to 300/300/330 |
| `CORS_ALLOWED_ORIGINS` | default `http://localhost:5130` |
| `ReverseProxy:Routes:*` / `Clusters:*` | full route table, §B / infra addresses per environment (`appsettings.json` = localhost ports, `appsettings.Compose.json` = Docker DNS names) |

**No `AuthorizationPolicy` is set on any Gateway route** (confirmed via grep of `source/ApiGateway/`
for `AuthorizationPolicy` — zero matches). The only gateway-level access control is a blanket 404 on
any path segment literally equal to `"internal"` (`source/ApiGateway/Program.cs:106-121`), which
exists to stop the browser reaching `/internal/*` endpoints through prefix-stripped routes. All real
authorization is enforced downstream, per-service.

### infra/docker-compose secrets (names only, `<redacted>` values)
`MSSQL_SA_PASSWORD`, `INTERNAL_API_KEY`, `JWT_KEY`, `HF_TOKEN`, `VNPAY_TMNCODE`, `VNPAY_HASHSECRET` —
all sourced from `infra/.env`/`infra/.env.docker` (not read directly; only `.example` templates were
read, per the read-only/no-secrets constraint).

---

## F. Contradictions, dead code, TODO/FIXME, and reliability caveats

1. **Missing contract doc** — `contracts/ai-service.md` does not exist despite being named in both
   the mission brief and `CLAUDE.md`. Logged in `OPEN_QUESTIONS.md` §1.
2. **Stale README** — `ai-service/README.md:7` claims the service "exposes only `/health`"; actual
   code has 20+ endpoints across 8 routers (§B.3). Documentation, not behavior — flag as stale, not
   as a functional defect.
3. **Face liveness computed but not enforced (`Status: Implemented but disabled`)** — the pass/fail
   decision at `ai-service/biometrics/router.py:147` is `passed = bool(identity is not None and
   identity >= threshold)` — face-match only. Liveness (`result["live"]`) is deliberately excluded,
   per comment `router.py:139-147`, because real-rig testing (dated 2026-07-24 in the comment) showed
   the yaw signal the liveness check depends on produces 0.55–1.61° of measured head-turn range
   against an 18° threshold during a clear, deliberate turn — i.e. the signal does not work on real
   hardware. The head-turn direction challenge is separately documented as unreliable (~80%
   disagreement with the requested direction) at `ai-service/biometrics/liveness_face.py:36-42`, and
   its output (`direction_ok`) is computed but not consumed by any pass/fail branch. Practical
   consequence: `/internal/biometrics/face/verify-live` currently provides identity matching only,
   with no anti-spoofing guarantee, despite its name and response schema (`FaceVerifyLiveResponse`,
   `biometrics/schemas.py:30-38`) implying liveness is checked. This is the single most consequential
   finding for the G5 (Biometric Verification & Integrity Enforcement) chapter and must be written up
   there as `Status: Implemented but disabled` with these exact citations, per instruction #8.
4. **Biometric thresholds are heuristic, not empirically calibrated** — `ai-service/biometrics/EVAL.md:74-77`
   explicitly forbids fabricating calibration data and states the current fixed thresholds in
   `config.py` must not be presented as accuracy-validated. No `calibration.json` was located under
   `ai-service/models/`. To be written up honestly in each G4/G5 feature's §8f (threshold provenance)
   using the correct FAR/FRR/EER terminology, per instruction 8f/8k.
5. **Voice-embedding quality score is explicitly a non-gating heuristic** —
   `ai-service/biometrics/voice_engine.py:67-69`: "a heuristic (informational, not a gate)... not
   derived from real calibration data (that's Milestone 3)."
6. **Dev-mode exposure warning** — `infra/docker-compose.override.yml:26-28` states in-repo that
   publishing the ai-service host port together with a tunnel exposes `/parse/*`, `/interview/*`,
   `/evaluate`, `/catalog`, `/ui` to the public internet with no authentication. Consistent with the
   router-level audit in §B.3 (those routers genuinely have zero auth).
7. **CORS wildcard + credentials on ai-service** — `ai-service/app/main.py:45-51` sets
   `allow_origins=ALLOWED_ORIGINS` (actual value `*`) together with `allow_credentials=True`. This
   combination is unusual (`*` origin with credentials is rejected by browsers per the Fetch spec
   unless the server reflects a literal origin, which FastAPI's `CORSMiddleware` does when
   `allow_origins=["*"]` and `allow_credentials=True` are both set — `[UNVERIFIED: whether
   CORSMiddleware's wildcard-with-credentials reflection behavior was exercised/tested here]`).
   Logged in `OPEN_QUESTIONS.md` §3 for the cross-cutting security chapter.
8. **Auth fail-open on ai-service internal endpoints** — both `X-Internal-Api-Key` check
   implementations (`biometrics/security.py:13-15`, `app/api/internal.py:21-24`) pass unconditionally
   if `INTERNAL_API_KEY` is unset. In the committed `.env`/`appsettings.json`, `InternalApiKey` values
   are set (non-empty), so this fail-open path is a **local-dev convenience, not the deployed
   posture** — but it is a design decision worth naming explicitly in the security chapter (Phase 7).
9. **`EvaluationAPI`'s `PlanQuestionsAsync`/`/internal/questions/plan` client method is dead code from
   EvaluationAPI's own call graph** — implemented end-to-end (`Clients/AiServiceClient.cs:22`,
   `Clients/IAiServiceClient.cs:7`, DTOs with `CvText`/`JdText` fields) but never called from
   `EvaluationController.cs` or `EvaluationService.cs`. Consistent with CV/JD-driven question
   planning being deferred (CvJdAPI is a stub per `CLAUDE.md`), but no explicit comment marks it as
   such in EvaluationAPI. `Status: Implemented but unused` — to confirm in Phase 3/6 chapters whether
   it's genuinely unreachable or reachable via a path not yet surveyed.
10. **`FeedbackReport.TheoryScore` column appears unpopulated** — `Models/FeedbackReport.cs:16`
    declares the column but the EvaluationAPI-survey agent found no write site for it in
    `EvaluationService.cs`. `[UNVERIFIED: not confirmed against every write path — needs a targeted
    grep in Phase 6]`. Logged in `OPEN_QUESTIONS.md` §4.
11. **Two unrelated tables both named `FeedbackReports`** — one in the Evaluation DB
    (`db/baseline/06_DevPrep_Evaluation.sql:71-93`, the per-session AI-generated evaluation summary —
    in scope) and one in the Feedback DB (`db/baseline/07_DevPrep_Feedback.sql:64-81`, a
    moderation/report-abuse table — teammate-owned, out of scope). Must not be conflated in the G6
    chapter or the ER diagram; will be labeled by schema/database in all diagrams.
12. **A second, older biometric-verification table exists in AuthAPI's DB** —
    `BiometricVerifications` (`db/baseline/01_DevPrep_Auth.sql:97-111`), distinct from
    `ProfileBiometrics` in InterviewAPI's DB. AuthAPI is teammate-owned (per `CLAUDE.md` boundary
    rules), so this will be documented only as an integration-contract note if InterviewAPI ever
    calls it (no such call was found in the InterviewAPI survey) — otherwise left unmentioned in
    feature chapters to avoid scope creep into a teammate's service.
13. **Possible stale TODO in `SessionService.cs:499`** — comment reads "Step B: TODO — deduct credit
    via PaymentApiClient (PaymentAPI not implemented yet)," but `Clients/PaymentApiClient.cs:31`
    (`DeductCreditAsync` → `POST /api/internal/credit/deduct`) is fully implemented and, per the
    controller-level comment at `InterviewSessionController.cs` around the `GenerateQuestion` action,
    credit deduction is described as happening on first entry. **This is a candidate premise
    conflict between a code comment and the surrounding implementation** — per instruction #7, this
    will be re-verified with a direct, non-delegated read of `SessionService.cs` lines 470–520 and
    `InterviewSessionController.cs` lines 609–641 at the start of Phase 2 (G2: Session Lifecycle &
    Credit) before the chapter is written, rather than silently reconciled here. Logged in
    `OPEN_QUESTIONS.md` §5.
14. **`SessionService.cs:489` TODO** ("award a one-time credit bonus on first biometric enrollment")
    describes a bonus that, on the Profile-enrollment side, appears to already be implemented by
    `ProfileBiometricsService.TryAwardEnrollmentBonusAsync` (`ProfileBiometricsService.cs:125`,
    `CreditConfig.BiometricEnrollmentBonusCredits=100`). These may be two distinct bonus trigger
    points (session-flow-time vs. profile-wizard-time) rather than a contradiction — needs direct
    verification in Phase 2/4, not assumed here. Logged in `OPEN_QUESTIONS.md` §5.
15. **Shared-breaker cross-contamination risk, self-documented** —
    `EvaluationAPI/Clients/InterviewApiClient.cs:200-213` notes that `RequestRegradeAsync` shares a
    circuit breaker with `OwnsSessionAsync`/`GetSessionEvaluationContextAsync` (one HttpClient/breaker
    per `Program.cs:77`, not per-method), so persistent regrade failures could open the breaker for
    unrelated ownership/transcript calls. The comment references "C-03 in
    docs/architecture/code-followups.md" — **this file was not located/verified to exist**.
    `[UNVERIFIED: docs/architecture/code-followups.md existence]`. Logged in `OPEN_QUESTIONS.md` §6.
16. **No `[Authorize]`/`[Route]` anywhere in WebClient** — all four in-scope controllers rely on
    manual cookie checks or downstream-401 propagation instead of ASP.NET's declarative
    authorization. Not a defect per se (documented pattern, consistent across all four controllers)
    but must be described accurately rather than assumed to be `[Authorize]`-gated in the security
    section of each chapter.
17. **Gateway has zero route-level `AuthorizationPolicy`** — confirmed via full-file read and grep;
    all authorization is enforced by the downstream .NET services' own `[Authorize]` attributes (or,
    for WebClient, by its manual checks). This is an architectural fact to state plainly in the
    cross-cutting chapter (Phase 7), not a bug — but it means a reader should not assume the gateway
    itself provides any authorization boundary beyond the internal-segment 404 block
    (`ApiGateway/Program.cs:106-121`).

### TODO/FIXME census (complete, by component)
| Component | Count | Locations |
|---|---|---|
| InterviewAPI | 3 | `SessionService.cs:489`, `SessionService.cs:499`, `BiometricVerificationService.cs:298-299` |
| EvaluationAPI | 0 | — |
| ai-service (`app/`) | 0 | — |
| ai-service (`biometrics/`) | 2 | `biometrics/router.py:146`, `biometrics/liveness_face.py:42` (both describe the disabled-liveness finding in §F.3) |
| WebClient | 0 | — |
| ApiGateway / infra / `*.json` / `docker-compose*.yml` | 0 | (only false positives inside a tokenizer vocab file, not developer comments) |

---

## Evidence volume (for the Definition-of-Done checklist)

Approximate distinct `file:line` citations gathered across the five survey passes: **550+**
(InterviewAPI ~230, EvaluationAPI ~140, ai-service ~140, WebClient ~180, Gateway/infra/SQL ~120 —
counts overlap where the same fact is cross-cited by two agents, e.g. an endpoint documented from
both the caller and callee side). Zero fabricated identifiers; every model name, threshold, port,
and config key above was read directly from source, `appsettings*.json`, `.env.example`,
`docker-compose*.yml`, or `*.sql`, per instruction #5. Six items are logged as `[UNVERIFIED]` in
`OPEN_QUESTIONS.md` and none were silently resolved.
