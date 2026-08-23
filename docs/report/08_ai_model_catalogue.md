# 08 — AI Model Catalogue

Phase 7. Consolidates every model and every numeric threshold documented across chapters 01–06 into
one place. Every fact here was already established with its own citation in an earlier chapter;
this chapter restates rather than re-derives, and cross-references rather than duplicates the full
a–k treatment given in the originating chapter. T1–T6 terminology rules remain in force — question
selection (G1) is not listed as a "model" below because it involves none (deterministic
metadata-filtered retrieval); the RAG entry is the genuine semantic-retrieval one from G3/G6.

---

## 1. Live models — on the deployed request path

| Model | Purpose | Runtime | Input | Output representation | Decision rule | Threshold (config key) | Evidence |
|---|---|---|---|---|---|---|---|
| Gemini (`gemini-3.5-flash-lite`) | Per-answer grading judge; whole-session summary; stateless hint | Cloud API (Google) | Grading/summary/hint prompt text | JSON `{knowledge, problem_solving, communication, analysis, ...}` (grading) or plain text (hint) | Weighted sum of 3 medianed axis scores (grading, G3 §8e); mean-of-questions (report aggregate, G6 §8a) | N/A (LLM judgement, not a similarity threshold) | `.env:33` (runtime value, overrides code default `gemini-3.1-pro`, `config.py:32`); G3 F-09 §8, G6 F-19 §8 |
| `BAAI/bge-m3` | Sentence embedding for (a) question-bank near-dup suppression, (b) genuine RAG retrieval feeding the grading prompt | Local (CPU/GPU depending on host), via `sentence-transformers` | Question/answer text | Dense embedding vector (dimensionality not independently re-verified) | Cosine similarity ≥ `NEAR_DUP_THRESHOLD` (dedup); top-`k=2` semantic query (RAG) | `NEAR_DUP_THRESHOLD=0.92` (`chroma_store.py:31`); `k=2` hardcoded, no config key | G1 F-01 §8, G3 F-09 §8h |
| InsightFace `buffalo_l` (ArcFace ResNet-50) | Face embedding for enrolment, entry-gate, in-session, and (unreachable) challenge verification | Local CPU (`CPUExecutionProvider`) | JPEG frame(s) | 512-d, L2-normalized (`face_engine.py:92`, confirmed not assumed) | Cosine similarity ≥ `FACE_MATCH_THRESHOLD` | `FACE_MATCH_THRESHOLD=0.35` (`biometrics/config.py:15`) | G4 F-12 §8, G5 F-15/F-16 §8 |
| SpeechBrain ECAPA-TDNN (`spkrec-ecapa-voxceleb`) | Voice embedding for enrolment and voice-answer/challenge verification | Local CPU (`run_opts={"device":"cpu"}`) | `audio/webm`, resampled to 16kHz mono | L2-normalized vector; dimensionality **not confirmable from this repository's source** (§ below) | Cosine similarity ≥ `VOICE_MATCH_THRESHOLD` | `VOICE_MATCH_THRESHOLD=0.25` (`config.py:16`) | G4 F-13 §8, G5 F-17 §8 |
| Piper (`en_US-ryan-medium`) | Text-to-speech for the AI interviewer voice | Local CPU, offline, no network call | AI question/hint text | WAV → mono 64kbps MP3 via `ffmpeg` | N/A (synthesis, no decision) | N/A | G3 F-10 §8 |
| Deepgram (`nova-3`) | Speech-to-text for voice answers and voice-challenge transcripts | Cloud API | `audio/webm` | Transcript string, top alternative only | N/A (transcription, no decision) | N/A | G3 F-10 §8, G5 F-17 §5 |

**Voice embedding dimensionality — restated honestly, not guessed.** `voice_engine.py:64,72`
computes `dim` dynamically from the model's own output shape (`emb.shape[0]`); no literal number is
hardcoded anywhere in this repository, and the model's `hyperparams.yaml` (which would state it) is
a symlink into a Docker-container-only path not resolvable on the host used for this report
(G4 F-13 §8d, §11). **This report does not state a number for this field.**

## 2. Trained or implemented components not on the deployed path

Every entry below was built, compiles, and is reachable by *something* in the codebase — but not by
any path a real candidate's browser session can trigger through the deployed WebClient UI.

| Component | What it does | Why unreachable/inactive | Evidence |
|---|---|---|---|
| Gemma-2B LoRA (`gemma-2b-devprep-sft`, base `google/gemma-2b-it`) | Conversational interviewer: greetings, follow-ups, reference-answer-aware in-session hints | `GemmaChat` is only instantiated inside `create_session()`, called only from ai-service's own unauthenticated `POST /interview/start`. No component in `source/InterviewAPI`/`EvaluationAPI`/`WebClient` calls any `/interview/{session_id}/*` route — `AiServiceClient.cs`'s only `/interview/*` call is `/interview/tts` | G1 F-01 §8, `OPEN_QUESTIONS.md` #7 |
| Plain STT endpoint (`api/interview/sessions/stt` → `/internal/stt`) | Stateless transcription with no speaker verification | The Room's actual voice-answer flow exclusively uses `.../verify/voice-answer` instead, which does STT *and* speaker verification together; no WebClient code calls the plain route | G3 F-10 §3, `OPEN_QUESTIONS.md` #16 |
| Challenge-based voice/face verify (`.../voice/verify-challenge`, `verify/face?phase=InSession`, `verify/voice`) | Server-issued phrase + speaker-and-content match (voice); server-issued head-turn direction (face) | Wired only to `biometric-wizard.js`'s "verify mode" (`mode="verify"`), whose trigger elements (`#verify-start-btn`, `#verify-session-select`) exist in no shipped Razor view | G5 F-17 §7, `OPEN_QUESTIONS.md` #22 |
| ElevenLabs TTS (`eleven_flash_v2_5`) | Cloud text-to-speech, same interface as Piper | Fully implemented, provider-selectable via `TTS_PROVIDER`, but the committed `.env` sets `TTS_PROVIDER=piper` | G3 F-10 §7 |
| `EvaluationAPI.AiServiceClient.PlanQuestionsAsync` (`POST /internal/questions/plan`) | Would let EvaluationAPI request a question plan directly from ai-service | No call site in `EvaluationController.cs`/`EvaluationService.cs` (fully re-read twice, Phase 0 and G6) — question planning is InterviewAPI's concern; this appears to be a vestigial capability on EvaluationAPI's client surface | G6 F-19 §11 |
| `FusionDecisionHelper` (.NET) | Would combine a face score and a voice score into one joint, calibrated pass/fail decision via a sigmoid over a weighted sum | Zero callers anywhere in `source/` (confirmed by grep, twice); `GetEntryGateStatusAsync`'s own comment states voice "stays out of the entry decision (the fusion helper remains dormant...)" | G5 § AI/ML consolidation, Phase 0.5 §Q4 |

## 3. Synthesis — the pattern, named once

Six distinct capabilities across three phases of this report turned out to be implemented but
unreachable or inactive. Read individually, each looks like a separate defect; read together, they
describe one architectural fact: **ai-service was developed as a broader capability layer than the
.NET services and WebClient currently consume.** A conversational LLM interviewer, a challenge-based
biometric verification mode, a second TTS provider, a cross-service question-planning client, and a
score-fusion mechanism were all built — several with real engineering care evident in their code
(the Gemma prompt engineering, the fusion sigmoid, the phrase-similarity matching) — but the
consumer side (InterviewAPI, EvaluationAPI, WebClient) never grew a call path to most of them,
either because an earlier design was superseded by a simpler one that shipped instead (the plain
STT endpoint, most plausibly), or because the capability was built ahead of the UI work needed to
expose it (the challenge-based verify mode; Gemma's conversational hints). **The lesson this
supports, stated as a lesson rather than six separate complaints**: a capability layer developed
ahead of or wider than its consumers needs consumer-side tracking — some record of which endpoints
are actually load-bearing for the shipped product — or the gap between "built" and "used" grows
invisibly, one feature at a time, until an audit like this one is what surfaces it.

## 4. Threshold provenance table

The single most useful artefact in this report for a reviewer, and its usefulness depends entirely
on being honest about the middle column. Values were traced to their exact source across G1–G6;
none are invented or estimated here.

| Threshold / parameter | Value | Config key / location | Provenance |
|---|---|---|---|
| Face match (identity) | 0.35 | `FACE_MATCH_THRESHOLD`, `biometrics/config.py:15` | No rationale recorded — fixed default, `calibration.json` override mechanism exists but the file does not exist in this deployment |
| Voice match (identity) | 0.25 | `VOICE_MATCH_THRESHOLD`, `config.py:16` | No rationale recorded — same override mechanism, same absent file |
| Face re-enrolment match | 0.35 | `Biometrics:FaceReEnrollmentMatchThreshold` (.NET) | No rationale recorded — a separate config surface that happens to reuse ai-service's face value |
| Voice re-enrolment match | 0.25 | `Biometrics:VoiceReEnrollmentMatchThreshold` (.NET) | No rationale recorded for this value or for why it differs numerically from the face re-enrolment value (`OPEN_QUESTIONS.md` #20) |
| Cross-account bonus dedup | 0.5 | `Biometrics:BonusDedupMatchThreshold` | **Documented rationale in code** for why it is *higher* than re-enrolment (avoids false-positive bonus denial, `ProfileBiometricsService.cs:176-179`) — but the specific number is explicitly marked "provisional pending Milestone 3 biometric calibration" in the same comment, so the rationale for the *relationship* is recorded while the *value itself* is not empirically calibrated |
| Question near-duplicate | 0.92 | `NEAR_DUP_THRESHOLD`, `chroma_store.py:31` | No rationale recorded beyond an inline "Tune 0.85–0.95 if needed" comment — heuristic |
| Voice challenge content match | 0.70 | `VOICE_CONTENT_MIN_SIMILARITY`, `biometrics/config.py:29` | No rationale recorded; used only in the unreachable challenge-verify path (§2) |
| Face detector confidence | 0.60 | `FACE_MIN_DET_SCORE` | No rationale recorded — engineering-judgment quality gate |
| Face bounding-box ratio | 0.05 | `FACE_MIN_FACE_RATIO` | No rationale recorded |
| Face blur (Laplacian variance) | 40.0 | `FACE_MIN_BLUR_VAR` | No rationale recorded |
| Voice minimum duration | 3.0s | `VOICE_MIN_DURATION_SEC` | No rationale recorded |
| Liveness minimum frames | 5 | `LIVENESS_MIN_FRAMES` | No rationale recorded; moot regardless — liveness is disabled (G5) |
| Liveness minimum yaw range | 18.0° | `LIVENESS_MIN_YAW_RANGE_DEG` | **Empirically tested and found broken** — real-rig measurement showed 0.55–1.61° during a genuine head turn; the *disablement* is evidence-based even though the *threshold value itself* was never validated against a working signal |
| Liveness max identity drift | 0.25 | `LIVENESS_MAX_IDENTITY_DRIFT` | No rationale recorded |
| No-face strike accumulation | 3 events → 1 strike | `Biometrics:NoFaceStrikeThreshold` | No rationale recorded — policy/product decision, not a biometric calibration |
| Tab-switch strike accumulation | 3 events → 1 strike | `Biometrics:TabStrikeThreshold` | No rationale recorded |
| In-session strike cap | 3 | `Biometrics:InSessionMaxStrikes` | No rationale recorded |
| Entry-gate retry cap | 3 | `Biometrics:EntryMaxRetries` | No rationale recorded |
| Grading rubric weights | 0.5 / 0.3 / 0.2 (knowledge/problem-solving/communication) | `evaluator.py:28-30` | No rationale recorded — a "design section 4.2" is referenced in the code comment but no such document exists anywhere in the repository (G3 F-09 §8f, `OPEN_QUESTIONS.md` #14) |
| Grade letter bands | A≥85, B≥70, C≥55, else D | `GradeFor`, `EvaluationService.cs:566-573` | No rationale recorded |
| Score comment bands | ≥85 "Strong", ≥70 "Solid", else "Needs improvement" | `_band_comment` (ai-service) / `CommentFor` (`Report.cshtml`) | No rationale recorded; **consistent with each other** but not with the two rows below |
| Score display-color/hero bands | ≥80, ≥60 | `Band`/`HeroTitle`, `Report.cshtml:26-27,48-49` | No rationale recorded; **numerically inconsistent** with the grade-letter and comment bands above — three independently-chosen band sets for related purposes, no single source of truth (`OPEN_QUESTIONS.md` #25) |
| Gemini's own internal grading anchors | 90+/70/50/<40 | `_grading_prompt`, `evaluator.py:104-105` | A **fourth**, distinct set — instructions given *to* the model as grading guidance, not a threshold applied *to* a computed score by application code; listed here for completeness, not counted among the "three conflicting band sets" above |
| Self-consistency runs | 3 (code default 1) | `SELF_CONSISTENCY_RUNS`, `.env:49` | Operator-set; the code-level default of 1 would silently disable the variance-reduction property if ever deployed without this override (G3 F-09 §8j) |

**Honest summary of this table**: of the roughly twenty numeric decision points enumerated above,
**one** (`BonusDedupMatchThreshold`'s relationship to the re-enrolment thresholds) has a documented
*rationale* in code, and **zero** have been *empirically calibrated* against a labelled dataset. One
(`LIVENESS_MIN_YAW_RANGE_DEG`) was tested and found to sit behind a broken measurement, which is a
different and more rigorous outcome than "untested," but still leaves the numeric value itself
unvalidated. The remainder are, plainly, heuristic defaults chosen by engineering judgment and never
revisited with measured data.

## 5. FAR, FRR, and EER — explained properly, once

For any biometric or similarity-based matching system, three standard error rates describe how well
a chosen threshold trades off two opposite mistakes:

- **False Accept Rate (FAR)** — the proportion of impostor comparisons (two different people's
  samples) that are wrongly accepted as a match, at a given threshold. A FAR that is too high means
  an attacker or a different person can pass as the enrolled user.
- **False Reject Rate (FRR)** — the proportion of genuine comparisons (the same person's samples,
  taken on different occasions) that are wrongly rejected as a non-match, at the same threshold. An
  FRR that is too high means the legitimate candidate is repeatedly locked out of their own account.
- **Equal Error Rate (EER)** — the single operating point on the FAR/FRR trade-off curve where the
  two rates are equal; it is the standard, threshold-independent summary statistic used to compare
  the discriminative power of different biometric systems or models, and it is normally *derived*
  from a labelled dataset of genuine and impostor comparison scores.

**None of these three rates has been measured for any threshold in the table above.** Computing
them would require a labelled set of genuine and impostor face/voice comparisons collected under
conditions representative of real usage — exactly what `ai-service/biometrics/calibrate.py` and
`EVAL.md` describe a process for, and exactly what `EVAL.md` warns, in its own words, must not be
faked: *"Do not fabricate genuine/impostor scores to make `calibrate.py` 'work'... it says nothing
about real accuracy and must never be presented as such or used to change the fixed defaults in
`config.py`"* (`EVAL.md:74-77`, G4 F-12 §8f). **As of this report, no `calibration.json` file exists
anywhere in the repository** (Phase 0.5 §Q4), meaning `calibrate.py` has not been run to completion
with validated input data, or its output was never committed. Every threshold in §4 above should be
read with this in mind: they are the fixed, uncalibrated defaults the codebase's own documentation
already says they are.

## 6. Academic grounding — consolidated

Citations already given in full, per-model, in G4/G5's §8k sections; gathered here for one-stop
reference. Confident citations only; anything uncertain is marked `[CITATION TO VERIFY]` at the
point it was first used and is not upgraded to a confident citation here.

- **ArcFace / angular-margin loss** (InsightFace `buffalo_l`): Deng, J. et al., "ArcFace: Additive
  Angular Margin Loss for Deep Face Recognition," CVPR 2019. `[CITATION TO VERIFY]` (G4 F-12 §8k).
- **ECAPA-TDNN** (SpeechBrain speaker verification): Desplanques, B., Thienpondt, J., Demuynck, K.,
  "ECAPA-TDNN: Emphasized Channel Attention, Propagation and Aggregation in TDNN Based Speaker
  Verification," Interspeech 2020. `[CITATION TO VERIFY]` (G4 F-13 §8k).
- **VoxCeleb** (ECAPA-TDNN training corpus): Nagrani, A., Chung, J.S., Zisserman, A., "VoxCeleb: A
  Large-Scale Speaker Identification Dataset," Interspeech 2017. `[CITATION TO VERIFY]` (G4 F-13
  §8k).
- **Dense retrieval / sentence embeddings** (`BAAI/bge-m3`, RAG): Chen, J. et al., "BGE
  M3-Embedding: Multi-Lingual, Multi-Functionality, Multi-Granularity Text Embeddings Through
  Self-Knowledge Distillation." `[CITATION TO VERIFY]` (G1 F-01 §8k). For the retrieval-augmented
  generation pattern itself: Lewis, P. et al., "Retrieval-Augmented Generation for
  Knowledge-Intensive NLP Tasks," NeurIPS 2020. `[CITATION TO VERIFY]` (G3 F-09 §8k).
- **LLM-as-judge**: Zheng, L. et al., "Judging LLM-as-a-Judge with MT-Bench and Chatbot Arena,"
  NeurIPS 2023 Datasets and Benchmarks Track. `[CITATION TO VERIFY]` (G3 F-09 §8k).
- **Self-consistency**: Wang, X. et al., "Self-Consistency Improves Chain of Thought Reasoning in
  Language Models," ICLR 2023. `[CITATION TO VERIFY]` (G3 F-09 §8k).
- **Face presentation-attack detection** (future-work reference only, not grounding for the current
  disabled mechanism): George, A., Marcel, S., "Deep Pixel-wise Binary Supervision for Face
  Presentation Attack Detection," ICB 2019. `[CITATION TO VERIFY]` (G5 F-15 §8k).

No entry above is upgraded from `[CITATION TO VERIFY]` in this consolidation — none was
independently re-checked against a bibliographic database in this pass, and the report's evidence
rule requires that uncertainty be preserved rather than resolved by assertion.
