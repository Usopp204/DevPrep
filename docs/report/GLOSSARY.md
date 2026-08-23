# Glossary

Each entry gives one accessible sentence for a non-specialist reader, followed by one precise
sentence for a reader checking the technical claim. Entries are added as later chapters introduce
new terms; nothing already defined here is removed in a later batch.

**Embedding** — In plain terms, a way of turning a piece of text into a list of numbers that
captures its meaning, so texts with similar meaning end up as similar-looking lists. Precisely: a
dense, fixed-length real-valued vector produced by a trained encoder such that semantic similarity
between two inputs is approximated by a distance or similarity function (typically cosine
similarity) between their vectors.

**Cosine similarity** — In plain terms, a score from -1 to 1 (in practice usually 0 to 1 for text
embeddings) that says how closely two vectors point in the same direction, which stands in for how
similar their meanings are. Precisely, for vectors $\mathbf{a}$ and $\mathbf{b}$:
$\mathrm{sim}(\mathbf{a},\mathbf{b}) = \frac{\mathbf{a}\cdot\mathbf{b}}{\lVert\mathbf{a}\rVert
\lVert\mathbf{b}\rVert}$.

**RAG (Retrieval-Augmented Generation)** — In plain terms, a technique where a system looks up
relevant snippets of text before asking a language model to write an answer, so the model's output
can be grounded in real, retrieved information. Precisely: a pipeline in which a retrieval step
(typically nearest-neighbor search over embedded documents) supplies context that is inserted into
a prompt fed to a generative language model, whose output is thereby conditioned on that retrieved
context. **This report reserves the term for genuine semantic retrieval feeding a generation step**
(as used in answer grading, documented in G3/G6) — the deterministic metadata-filtered retrieval
used for question selection in G1 is explicitly *not* called RAG in this report, since it involves
neither semantic similarity search nor a generation step; see `01_G1_session_configuration.md` F-01
§8.

**Deterministic metadata-filtered retrieval** — In plain terms, picking items from a list purely by
matching tags (like "role = Backend" or "difficulty = Middle"), the same way a database query with
a `WHERE` clause would, with no AI judgment involved in which items match. Precisely: a document
retrieval operation implemented as an exact-match filter over structured metadata fields (e.g.
ChromaDB's `collection.get(where=...)`), which returns the same result set for the same filter
values every time and involves no embedding computation, similarity ranking, or generative step.

**Idempotency** — In plain terms, an operation that gives the same end result no matter how many
times it is repeated, so retrying it safely never causes duplicate effects. Precisely: an operation
is idempotent if applying it $n \geq 1$ times produces the same system state as applying it once;
in this report, idempotency is stated explicitly per business rule (e.g.
`01_G1_session_configuration.md` F-04 R2) rather than assumed, since several of this codebase's
write paths are read-then-decide rather than atomic and are *not* idempotent under concurrency
(e.g. F-01/F-02's session-creation race, flagged as an open question).

**LoRA (Low-Rank Adaptation)** — In plain terms, a way to fine-tune a large pretrained language
model cheaply by only training a small set of extra add-on weights, instead of retraining the whole
model. Precisely: a parameter-efficient fine-tuning technique that freezes the base model's weights
and injects trainable low-rank decomposition matrices into selected layers, so the adapter
(typically megabytes, not gigabytes) can be distributed and loaded on top of the shared base model
at inference time. Relevant to this report because ai-service's interviewer model
(`gemma-2b-devprep-sft`) is exactly such an adapter over the base `google/gemma-2b-it` model — see
`00A_VERIFICATION.md` §Q1.

**Semantic-versus-metadata retrieval** (contrast pair, not a standalone term) — see *RAG* and
*Deterministic metadata-filtered retrieval* above; this report is deliberately precise about which
of the two mechanisms is in play at each point in the system, per the Phase 1 terminology
corrections.

**L2 normalisation** — In plain terms, rescaling a list of numbers so its overall "length" is
exactly 1, without changing the direction it points in — like shrinking or stretching an arrow so
its tip always lands on a circle of radius 1, no matter how long it started. Precisely: dividing a
vector $\mathbf{v}$ by its Euclidean (L2) norm, $\mathbf{v}_{\text{norm}} = \mathbf{v} /
\lVert\mathbf{v}\rVert_2$, so that $\lVert\mathbf{v}_{\text{norm}}\rVert_2 = 1$; this is done to
every face and voice embedding in this system (`face_engine.py:92`, `voice_engine.py:65`) so that
cosine similarity between two embeddings reduces to a plain dot product.

**Presentation attack** — In plain terms, trying to fool a face or voice check by showing it a
photo, a video, or a recording of the real person instead of the real person themselves. Precisely:
an attempt to deceive a biometric verification system by presenting an artifact (printed photo,
replayed video or audio, a mask, etc.) captured from a genuine enrolled subject, rather than the
live subject; defended against by *liveness detection*, which this system computes for the face
channel but excludes from its pass/fail decision (see `05_G5_biometric_verification_and_integrity.md`,
Threat model).

**Substitution attack** — In plain terms, having a different real person answer instead of the
enrolled candidate — not a photo or recording, an actual different human. Precisely: an attempt to
defeat identity verification by having an unauthorized individual complete the verified task in
place of the enrolled subject; this is the threat model this system's continuous face/voice
matching actually defends against (`05_G5_biometric_verification_and_integrity.md`, Threat model,
point 1).

**Liveness (detection)** — In plain terms, a check that the thing in front of the camera or
microphone is a live person right now, not a static photo or a replayed clip. Precisely: a
biometric anti-spoofing technique that analyzes signals expected to be present only in a live
capture (e.g. natural head motion, blink, texture/depth cues) to distinguish a genuine live subject
from a presentation-attack artifact. In this system, liveness is computed (measured yaw range,
frame-to-frame identity drift) but explicitly excluded from the entry-gate pass/fail decision after
real-hardware testing showed the yaw signal does not function (`ai-service/biometrics/router.py:
139-147`).

**Angular margin (loss)** — In plain terms, a way of training a face-recognition model so that
photos of the same person end up clustered tightly together and clearly separated from everyone
else's cluster, by an explicit safety margin, not just "roughly close." Precisely: a training
objective (e.g. ArcFace) that adds an angular penalty margin between a sample's embedding and its
true class center on the hypersphere before applying the softmax loss, which yields embeddings
whose cosine similarity is a more reliable same-identity/different-identity signal at inference
time than embeddings trained with a plain softmax alone.

**FAR / FRR / EER** — In plain terms: FAR (False Accept Rate) is how often the system wrongly says
"same person" for two different people; FRR (False Reject Rate) is how often it wrongly says
"different person" for the same person on two occasions; EER (Equal Error Rate) is the one
threshold setting where those two mistake rates are equal, used as a standard single-number
summary of how good a biometric system is. Precisely: FAR and FRR are both functions of the chosen
match threshold, computed from labelled genuine and impostor comparison scores; EER is the
threshold at which $\text{FAR}(\tau) = \text{FRR}(\tau)$. **None of these three rates has been
measured for any threshold in this system** — see `08_ai_model_catalogue.md` §5.

**IDOR (Insecure Direct Object Reference)** — In plain terms, a security bug where an application
lets a logged-in user access someone else's data just by guessing or changing an ID in a request,
because the server never checked who actually owns that record. Precisely: an authorization flaw in
which an application exposes a reference to an internal object (e.g. a database key) without
verifying that the requesting principal is authorized to access the specific object referenced;
this report documents, per feature, the specific ownership check (typically `session.UserId !=
userId`, or a cross-service `.../owns` call for `EvaluationAPI`) that defends against it.

**TOCTOU (Time-Of-Check to Time-Of-Use)** — In plain terms, a bug where a program checks whether
something is true, but by the time it actually acts on that check, the situation has changed
underneath it — like checking a parking spot is empty, then finding a car there by the time you
park. Precisely: a race condition in which the validity of a condition verified at check-time is no
longer guaranteed at use-time, because another concurrent operation altered the relevant state in
between; this report traces exactly which of this codebase's guards are atomic (a single
conditional `UPDATE`, closing the gap) versus read-then-decide (leaving a narrow window), e.g.
`02_G2_session_lifecycle_and_credit.md` F-05 §7 R2 (atomic) versus F-07 §7 R1 (not atomic across
the `Initialized` status).

**Circuit breaker** — In plain terms, an automatic safety switch that stops a service from
repeatedly calling a dependency that has clearly stopped working, giving it time to recover instead
of hammering it. Precisely: a resilience pattern that tracks consecutive failures against a
dependency and, once a threshold is exceeded, "opens" (rejects calls immediately without attempting
them) for a cooldown period before allowing a single trial call through to test recovery. This
system uses one singleton breaker instance per dependency per process, deliberately, because the
failure count lives inside the policy object itself (`07_cross_cutting_architecture.md` §7).

**Self-consistency** — In plain terms, asking the same question to an AI model multiple times and
taking the middle (or most common) answer, so one unusually harsh or lenient response doesn't skew
the result. Precisely: a technique that runs the same prompt through a language model $n$ times
independently and aggregates the results (here, via the per-axis median) to reduce the variance of
any single stochastic sample; used for per-answer grading only, not for the whole-session summary
or the cross-question report aggregate, which use a single model call and a plain mean respectively
— see `03_G3_interview_room_interaction.md` F-09 §8k and `06_G6_evaluation_and_reporting.md` F-19
§8a for why these must not be conflated.

**LLM-as-judge** — In plain terms, using a general-purpose AI language model to grade or evaluate
something (like an interview answer) by giving it a rubric and asking it to score, instead of using
a model specifically trained to output scores. Precisely: a pattern in which a general
instruction-following large language model is prompted, zero- or few-shot, with a task description
and scoring rubric, and its free-text or structured-JSON output is parsed as the evaluation result;
this system's grading judge (`evaluator.GeminiJudge`) is exactly this pattern, with no task-specific
fine-tuning applied — see `03_G3_interview_room_interaction.md` F-09 §8b.

**VAD (Voice Activity Detection)** — In plain terms, automatically figuring out which parts of an
audio recording actually contain someone talking, versus silence or background noise. Precisely: a
signal-processing or model-based technique that classifies audio frames as speech or non-speech,
commonly used to trim silence before feeding audio to a downstream STT or speaker-verification
model. **Not implemented anywhere in this codebase** — `ai-service/app/services/voice.py:5-6`'s own
module docstring notes full-duplex streaming with VAD/barge-in as "a future enhancement," and no
VAD step was found in any of the audio-processing paths documented in this report
(`03_G3_interview_room_interaction.md` F-10, `05_G5_biometric_verification_and_integrity.md` F-17).
