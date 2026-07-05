"""
interviewer.py — RAG 1: the Interviewer (realtime conversation, chat + voice).
Location: ai-service/app/services/interviewer.py

Design:
- Questions come from the "question bank" (chroma_store.build_question_pool), exact-match level.
- Gemma 2B only handles conversation: greeting, follow-ups, closing. It does NOT score.
- The reference answer stays server-side (hidden from the user); it drives follow-up
  generation and is handed off to the Evaluator.

The orchestration (InterviewSession) is pure logic -> testable without the model.
The model loading (GemmaChat) is isolated and lazy-loaded.

Note: prompt language is English, so the interview is conducted in English
(consistent with the English UI and English question bank).
"""
from __future__ import annotations

import os
from dataclasses import dataclass, field
from typing import Callable, Optional

GEMMA_MODEL_DIR = os.getenv("GEMMA_MODEL_DIR", "/app/models/gemma-2b-devprep-sft")
MAX_FOLLOWUPS = 1  # max follow-up questions per main question

# Duration (minutes) -> target number of questions (matches the "Q3 of 10" UI bar)
# The interview length is driven by TIME, not a fixed count: the client runs the countdown
# and ends the session at 0 (or when the user clicks End). The pool is sized generously for
# the duration so it rarely runs out before time does.
QUESTIONS_PER_MINUTE = 1.5

# The dataset's `Position` field is a coarse track, not a job title. Observed values:
# Backend, Frontend, Database, Language, System Design, Problem Solving.
# Problem Solving is woven into every session; System Design is added for mid+ levels.
PROBLEM_SOLVING_POSITION = "Problem Solving"
SYSTEM_DESIGN_POSITION = "System Design"
SYSTEM_DESIGN_LEVELS = {"middle", "senior", "techlead"}
PROBLEM_SOLVING_SHARE = 0.2
SYSTEM_DESIGN_SHARE = 0.25


def target_questions(duration_min: int) -> int:
    """Generous pool size for the duration (the session ends by time, not by this count)."""
    return max(5, round((duration_min or 45) * QUESTIONS_PER_MINUTE))


@dataclass
class SessionConfig:
    position: str
    level: str                       # intern/fresher/junior/middle/senior/techlead
    interview_type: str = "technical"
    focus_areas: list[str] = field(default_factory=list)
    duration_min: int = 45
    jd: str = ""


@dataclass
class QuestionItem:
    """A single planned question item sent to the client at session start."""
    qid: str
    question: str
    category: str
    difficulty: str


@dataclass
class InterviewerTurn:
    message: str               # content sent to the user (NEVER contains the reference answer)
    ended: bool                # whether the session has ended
    question_number: int = 0   # 1-based index of the current main question
    total_questions: int = 0   # total planned questions for the session
    question: str = ""         # first question (start only) -> rendered as a separate bubble
    scenario: list = field(default_factory=list)  # full question list (start only, no answers)


# --- Prompts (system is merged into the user turn because Gemma's chat template
#     does not support a 'system' role) ---
def _persona(cfg: SessionConfig) -> str:
    jd = f" JD context: {cfg.jd[:500]}." if cfg.jd else ""
    return (
        f"You are a professional technical interviewer for the {cfg.position} role, "
        f"{cfg.level} level. Focus areas: {', '.join(cfg.focus_areas) or 'general'}.{jd} "
        "Keep a concise, professional, friendly tone. "
        "Do NOT reveal the reference answer, do NOT score, do NOT judge right/wrong."
    )


def _greeting_prompt(cfg: SessionConfig) -> tuple[str, str]:
    return _persona(cfg), "Write a 1-2 sentence opening greeting for the interview. Return only the greeting."


def _followup_prompt(cfg: SessionConfig, question: str, reference: str, answer: str) -> tuple[str, str]:
    system = _persona(cfg)
    user = (
        f"QUESTION: {question}\n"
        f"REFERENCE ANSWER (for your reference only, NEVER read it aloud): {reference[:800]}\n"
        f"CANDIDATE ANSWER: {answer}\n\n"
        "If the answer is missing core points compared to the reference, ask ONE follow-up "
        "question targeting the gap (return only the question). "
        "If the answer is sufficient, return exactly the token: [NEXT]"
    )
    return system, user


def _closing_prompt(cfg: SessionConfig) -> tuple[str, str]:
    return _persona(cfg), "Write a 1-2 sentence polite closing for the interview. Return only the closing."


def _wants_next(text: str) -> bool:
    return "[NEXT]" in text.upper() or len(text.strip()) < 3


def _echoes_question(followup: str, question: str) -> bool:
    """True if a generated follow-up merely restates the current question."""
    norm = lambda s: " ".join(s.lower().split())
    f, q = norm(followup), norm(question)
    if not f:
        return True
    if q and (q in f or f in q):
        return True
    fa, qa = set(f.split()), set(q.split())
    if not fa or not qa:
        return False
    return len(fa & qa) / len(fa | qa) >= 0.7


# --- Interview orchestration (pure logic) ------------------------------------
class InterviewSession:
    def __init__(self, config: SessionConfig, pool: list[dict], chat_fn: Callable[[str, str], str]):
        self.cfg = config
        self.chat = chat_fn
        self.pool = pool
        self.idx = -1
        self.current: Optional[dict] = None
        self.items: list[dict] = []              # finalized answered items
        self.graded: dict[str, dict] = {}        # qid -> grade (filled by background grader)
        self._scheduled: set[str] = set()        # qids already handed to the grader

    def _turn(self, message: str, ended: bool) -> "InterviewerTurn":
        qn = (self.idx + 1) if self.current is not None else len(self.items)
        return InterviewerTurn(message=message, ended=ended,
                               question_number=qn, total_questions=len(self.pool))

    # --- lifecycle ---
    def _greeting_text(self) -> str:
        focus = ", ".join(self.cfg.focus_areas)
        focus_part = f" Today we will focus on {focus}." if focus else ""
        return (
            f"Welcome, and thank you for joining today's {self.cfg.position} interview "
            f"at the {self.cfg.level} level.{focus_part} "
            "I'll ask a series of questions to explore how you think and what you know \u2014 "
            "take your time, feel free to think out loud, and answer just as you would in a "
            "real interview. Ready when you are. Let's begin!"
        )

    def start(self) -> "InterviewerTurn":
        """Fixed greeting + first question. Also returns the full planned scenario so the
        client can display a question list and drive the flow locally (no follow-ups)."""
        first_q = self._next_question_text()
        scenario = [
            QuestionItem(qid=f"Q{i+1}", question=q["question"],
                         category=q.get("category", ""), difficulty=q.get("difficulty", ""))
            for i, q in enumerate(self.pool)
        ]
        turn = self._turn(self._greeting_text(), False)
        turn.question = first_q
        turn.scenario = [vars(s) for s in scenario]
        return turn

    def submit_answer(self, answer: str) -> "InterviewerTurn":
        """Record the answer and immediately advance — no follow-up logic."""
        if self.current is None:
            return self._turn("Session has not started.", True)
        self.current["user_answer"] = answer
        return self._advance()

    def skip(self) -> "InterviewerTurn":
        if self.current is not None:
            self.current["skipped"] = True
        return self._advance()

    def hint(self) -> str:
        """Light hint, does NOT reveal the answer. Does not change session state."""
        if self.current is None:
            return ""
        sys_p = _persona(self.cfg)
        usr_p = (
            f"QUESTION: {self.current['question']}\n"
            f"REFERENCE ANSWER (must not be read aloud): {self.current['_reference'][:800]}\n"
            "Give ONE light directional hint without revealing the answer."
        )
        return self.chat(sys_p, usr_p)

    def _closing_text(self) -> str:
        """Fixed closing message (no model call) so ending the session is instant."""
        return (
            "That wraps up our interview \u2014 thank you for your time and thoughtful answers! "
            "I'm now putting together your detailed evaluation."
        )

    def end(self) -> InterviewerTurn:
        self._finalize_current()
        return self._turn(self._closing_text(), True)

    def transcript(self, duration_actual_sec: int) -> dict:
        return {
            "config": {
                "position": self.cfg.position, "level": self.cfg.level,
                "type": self.cfg.interview_type, "focus_areas": self.cfg.focus_areas,
                "duration_min": self.cfg.duration_min, "jd": self.cfg.jd,
            },
            "duration_actual_sec": duration_actual_sec,
            "items": [self._public_item(it) for it in self.items],
        }

    # --- internals ---
    def _next_question_text(self) -> str:
        self.idx += 1
        q = self.pool[self.idx]
        self.current = {
            "qid": f"Q{self.idx + 1}",
            "question": q["question"],
            "_reference": q.get("answer", ""),
            "category": q.get("category", ""),
            "difficulty": q.get("difficulty", ""),
            "user_answer": None,
            "followups": [],
            "skipped": False,
        }
        return q["question"]

    def _finalize_current(self) -> None:
        if self.current is not None:
            self.items.append(self.current)
            self.current = None

    def _advance(self) -> InterviewerTurn:
        self._finalize_current()
        if self.idx + 1 >= len(self.pool):
            return self._turn(self._closing_text(), True)
        return self._turn(self._next_question_text(), False)

    @staticmethod
    def _public_item(it: dict) -> dict:
        """Hand-off item for the Evaluator: KEEP reference_answer; drop internal _ keys."""
        return {
            "qid": it["qid"],
            "question": it["question"],
            "reference_answer": it["_reference"],
            "category": it["category"],
            "difficulty": it["difficulty"],
            "user_answer": it["user_answer"],
            "followups": it["followups"],
            "skipped": it["skipped"],
        }

    def public_items(self) -> list[dict]:
        """All finalized items in evaluator-ready form."""
        return [self._public_item(it) for it in self.items]

    def pending_grading(self) -> list[dict]:
        """Finalized items not yet handed to the grader; marks them as scheduled.

        Lets the API grade each answer in the background as the interview proceeds, so the
        final report is just an aggregation (fast) instead of grading everything at the end.
        """
        out = []
        for it in self.items:
            if it["qid"] not in self._scheduled:
                self._scheduled.add(it["qid"])
                out.append(self._public_item(it))
        return out


# --- Load Gemma 2B (lazy) ----------------------------------------------------
class GemmaChat:
    """Calls the local fine-tuned Gemma 2B. Isolated so the loader is easy to swap."""

    def __init__(self, model_dir: str = GEMMA_MODEL_DIR):
        self.model_dir = model_dir
        self._model = None
        self._tok = None

    def _load(self) -> None:
        import torch
        from transformers import AutoModelForCausalLM, AutoTokenizer

        self._tok = AutoTokenizer.from_pretrained(self.model_dir)
        device = "cuda" if torch.cuda.is_available() else "cpu"
        # gemma-2b-devprep-sft is a LoRA adapter; transformers auto-loads the base model +
        # adapter (peft is installed). Do NOT use device_map="auto" here: it can leave the
        # LoRA layers on CPU while the base is on GPU, causing a cpu/cuda mismatch. Load on
        # a single device explicitly instead.
        self._model = AutoModelForCausalLM.from_pretrained(
            self.model_dir, torch_dtype=torch.bfloat16,
        ).to(device)
        self._model.eval()

    def __call__(self, system: str, user: str, max_new_tokens: int = 128) -> str:
        if self._model is None:
            self._load()
        # Gemma's chat template has no 'system' role -> merge it into the user turn
        messages = [{"role": "user", "content": f"{system}\n\n{user}"}]
        # return_dict=True yields a BatchEncoding (input_ids + attention_mask) that we can
        # unpack with **inputs; this works across transformers versions.
        inputs = self._tok.apply_chat_template(
            messages, add_generation_prompt=True, return_tensors="pt", return_dict=True,
        ).to(self._model.device)
        prompt_len = inputs["input_ids"].shape[-1]

        # Stop at the end of the FIRST model turn so the model can't ramble into fake
        # multi-turn dialogue (which leaks "model" turn markers as text).
        eos_ids = [self._tok.eos_token_id]
        end_of_turn = self._tok.convert_tokens_to_ids("<end_of_turn>")
        if isinstance(end_of_turn, int) and end_of_turn >= 0:
            eos_ids.append(end_of_turn)

        out = self._model.generate(
            **inputs,
            max_new_tokens=max_new_tokens,
            do_sample=True,
            temperature=0.7,
            top_p=0.9,
            repetition_penalty=1.1,
            eos_token_id=eos_ids,
            pad_token_id=self._tok.pad_token_id or self._tok.eos_token_id,
        )
        text = self._tok.decode(out[0][prompt_len:], skip_special_tokens=True)

        # Safety net: cut at any leftover turn marker the model emitted as plain text.
        for marker in ("<end_of_turn>", "<start_of_turn>", "\nmodel", "\nuser"):
            cut = text.find(marker)
            if cut != -1:
                text = text[:cut]
        return text.strip()


def _plan_quotas(total: int, level: str) -> tuple[int, int]:
    """Allocate how many Problem-Solving and System-Design questions a session gets."""
    n_ps = max(1, round(total * PROBLEM_SOLVING_SHARE))
    n_sd = max(1, round(total * SYSTEM_DESIGN_SHARE)) if level.lower() in SYSTEM_DESIGN_LEVELS else 0
    return n_ps, n_sd


def _weave(core: list[dict], ps: list[dict], sd: list[dict]) -> list[dict]:
    """Interleave so Problem-Solving (~every 4th) and System-Design (~every 6th) are spread
    across the session rather than stacked at the end; core fills the rest."""
    out: list[dict] = []
    ci = pi = si = 0
    slot = 0
    while ci < len(core) or pi < len(ps) or si < len(sd):
        if slot % 4 == 3 and pi < len(ps):
            out.append(ps[pi]); pi += 1
        elif slot % 6 == 5 and si < len(sd):
            out.append(sd[si]); si += 1
        elif ci < len(core):
            out.append(core[ci]); ci += 1
        elif pi < len(ps):
            out.append(ps[pi]); pi += 1
        elif si < len(sd):
            out.append(sd[si]); si += 1
        slot += 1
    return out


def build_interview_plan(config: SessionConfig, store) -> list[dict]:
    """
    Compose the question pool from several tracks:
      - core technical questions for the role (narrowed by focus areas)
      - Problem-Solving questions woven into every session
      - System-Design questions added only for middle/senior/techlead
    `store` is anything exposing build_question_pool (chroma_store, or a fake for tests).
    """
    total = target_questions(config.duration_min)
    n_ps, n_sd = _plan_quotas(total, config.level)

    problem_solving = store.build_question_pool(PROBLEM_SOLVING_POSITION, config.level, limit=n_ps)
    system_design = (
        store.build_question_pool(SYSTEM_DESIGN_POSITION, config.level, limit=n_sd) if n_sd else []
    )

    # Core fills whatever budget the cross-cutting tracks did not (e.g. if a track is empty).
    n_core = max(1, total - len(problem_solving) - len(system_design))
    # When the user picked technologies (focus areas), drive the core by technology across all
    # tracks (a tech like MongoDB lives under the Database track but is valid for a Backend role).
    # With no focus areas, fall back to the role's own track.
    if config.focus_areas:
        core = store.build_question_pool(
            difficulty=config.level, categories=config.focus_areas, limit=n_core,
        )
    else:
        core = store.build_question_pool(
            position=config.position, difficulty=config.level, limit=n_core,
        )
    # If too few questions matched, top up from the role's track.
    if len(core) < n_core:
        seen = {q["qid"] for q in core}
        for q in store.build_question_pool(position=config.position, difficulty=config.level, limit=n_core):
            if q["qid"] not in seen and len(core) < n_core:
                core.append(q)
                seen.add(q["qid"])

    # Weave Problem-Solving / System-Design THROUGHOUT (not only at the end): a time-driven
    # session may stop anywhere, so these must appear early too.
    ordered = _weave(core, problem_solving, system_design)

    # Final guard against repeats: the per-call near-duplicate filter does not see across
    # tracks or the top-up pass, so the same question text could appear twice. Drop repeats
    # by qid AND by normalized question text.
    seen_ids: set[str] = set()
    seen_text: set[str] = set()
    unique: list[dict] = []
    for q in ordered:
        text_key = " ".join((q.get("question") or "").lower().split())
        if q["qid"] in seen_ids or text_key in seen_text:
            continue
        seen_ids.add(q["qid"])
        seen_text.add(text_key)
        unique.append(q)
    return unique


def create_session(config: SessionConfig) -> InterviewSession:
    """Factory: build the composed pool from ChromaDB + load Gemma."""
    from app.services import chroma_store

    pool = build_interview_plan(config, chroma_store)
    if not pool:
        raise ValueError(f"No questions for {config.position}/{config.level}/{config.focus_areas}")
    return InterviewSession(config, pool, GemmaChat())