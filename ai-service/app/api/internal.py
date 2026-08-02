"""
internal.py — Stateless internal API consumed by the .NET InterviewAPI / EvaluationAPI.
Location: ai-service/app/api/internal.py

These endpoints hold NO session state. They are thin wrappers over the existing RAG/grading
logic. Auth is the shared header X-Internal-Api-Key (validated against config.INTERNAL_API_KEY).
"""
from fastapi import APIRouter, File, Header, HTTPException, UploadFile
from pydantic import BaseModel, Field

from app.core import config
from app.services import chroma_store, evaluator, interviewer
from app.services.voice import DeepgramSTT

router = APIRouter(prefix="/internal", tags=["internal"])

# Stateless STT: transcribes an uploaded audio clip. No session state is touched.
_stt = DeepgramSTT()


def _check_key(x_internal_api_key: str | None) -> None:
    # If a key is configured, require it. (Empty key = open, for local dev only.)
    if config.INTERNAL_API_KEY and x_internal_api_key != config.INTERNAL_API_KEY:
        raise HTTPException(status_code=401, detail="Invalid internal API key.")


# ---- 1. Question plan -------------------------------------------------------
class PlanIn(BaseModel):
    position: str
    level: str
    focus_areas: list[str] = Field(default_factory=list)
    duration_min: int = 30
    cv_text: str | None = None
    jd_text: str | None = None


class PlannedQuestion(BaseModel):
    order_index: int
    question: str
    reference_answer: str
    category: str
    difficulty: str


class PlanOut(BaseModel):
    questions: list[PlannedQuestion]


@router.post("/questions/plan", response_model=PlanOut)
def plan_questions(body: PlanIn, x_internal_api_key: str | None = Header(default=None)):
    _check_key(x_internal_api_key)
    cfg = interviewer.SessionConfig(
        position=body.position, level=body.level, interview_type="technical",
        focus_areas=body.focus_areas, duration_min=body.duration_min, jd=body.jd_text or "",
    )
    pool = interviewer.build_interview_plan(cfg, chroma_store)
    questions = [
        PlannedQuestion(order_index=i + 1, question=q["question"],
                        reference_answer=q.get("answer", ""), category=q.get("category", ""),
                        difficulty=q.get("difficulty", ""))
        for i, q in enumerate(pool)
    ]
    return PlanOut(questions=questions)


# ---- 2. Evaluate one answer -------------------------------------------------
class EvaluateIn(BaseModel):
    question: str
    reference_answer: str = ""
    category: str = ""
    difficulty: str = ""
    user_answer: str = ""
    followups: list[dict] = Field(default_factory=list)


class EvaluateOut(BaseModel):
    score: float
    criteria: dict[str, float]
    analysis: str
    suggested_improvement: str
    praise: str
    model_used: str


@router.post("/evaluate-answer", response_model=EvaluateOut)
def evaluate_answer(body: EvaluateIn, x_internal_api_key: str | None = Header(default=None)):
    _check_key(x_internal_api_key)
    item = {
        "qid": "Q", "question": body.question, "reference_answer": body.reference_answer,
        "category": body.category, "difficulty": body.difficulty,
        "user_answer": body.user_answer, "followups": body.followups, "skipped": False,
    }
    judge = evaluator.GeminiJudge()
    try:
        graded = evaluator.grade_item(item, judge, evaluator.enrich_for(item))
    except evaluator.GradingFailure:
        raise HTTPException(status_code=503, detail="Grading failed, please try again.") from None
    return EvaluateOut(
        score=graded["score"], criteria=graded["criteria"],
        analysis=graded["analysis"],
        suggested_improvement=graded["suggested_improvement"],
        praise=graded["praise"], model_used=judge.model,
    )


# ---- 3. Aggregate report ----------------------------------------------------
class EvalRow(BaseModel):
    qid: str = ""
    question: str = ""
    user_answer: str = ""
    category: str = ""
    difficulty: str = ""
    score: float = 0
    criteria: dict | None = None
    analysis: str = ""
    praise: str = ""
    suggested_improvement: str = ""


class ReportIn(BaseModel):
    duration_actual_sec: int = 0
    evaluations: list[EvalRow] = Field(default_factory=list)


@router.post("/report")
def build_report(body: ReportIn, x_internal_api_key: str | None = Header(default=None)):
    _check_key(x_internal_api_key)
    reports = [
        {
            "qid": row.qid,
            "question": row.question,
            "user_answer": row.user_answer,
            "category": row.category,
            "difficulty": row.difficulty,
            "score": row.score,
            "criteria": row.criteria,
            "analysis": row.analysis,
            "praise": row.praise,
            "suggested_improvement": row.suggested_improvement,
        }
        for row in body.evaluations
    ]
    judge = evaluator.GeminiJudge()
    return evaluator.build_report(reports, judge, body.duration_actual_sec)


# ---- 4. Speech-to-text (stateless) ------------------------------------------
@router.post("/stt")
async def transcribe_audio(
    audio: UploadFile = File(...),
    x_internal_api_key: str | None = Header(default=None),
):
    """Transcribe a single uploaded audio clip to text. No session state is touched;
    the .NET side feeds the text back into its own SQL-tracked message flow."""
    _check_key(x_internal_api_key)
    text = _stt.transcribe(await audio.read())
    return {"text": text}


# ---- 5. Hint (stateless) ----------------------------------------------------
class HintIn(BaseModel):
    question: str
    # Defaults to English so existing callers (.NET does not send this field yet) are unaffected.
    # Extend LANGUAGE_NAMES below when adding another supported language (e.g. "vi").
    language: str = "en"


LANGUAGE_NAMES = {"en": "English"}


_HINT_PROMPT = (
    "You are a technical interview coach. The candidate is stuck on this question:\n"
    "QUESTION: {question}\n\n"
    "Give ONE short directional hint (1-2 sentences) that nudges them toward the right line of "
    "thinking or the key concept to recall. Do NOT reveal the answer, do NOT give a model answer, "
    "and do NOT list all the points they should mention.\n"
    "IMPORTANT: respond in {language_name} only. Return only the hint text."
)


@router.post("/hint")
def get_hint(body: HintIn, x_internal_api_key: str | None = Header(default=None)):
    """Return one light, directional hint for a question. Stateless — no session, no reference
    answer (the model uses general knowledge). Falls back to a generic nudge on grader failure."""
    _check_key(x_internal_api_key)
    language_name = LANGUAGE_NAMES.get(body.language, LANGUAGE_NAMES["en"])
    judge = evaluator.GeminiJudge()
    try:
        prompt = _HINT_PROMPT.format(question=body.question, language_name=language_name)
        hint = judge(prompt).strip()
    except Exception:
        hint = ""
    if not hint:
        hint = "Think about the core concept this question targets, and start from its definition."
    return {"hint": hint}


# ---- 6. Question index sync (incremental, QuestionAPI admin CRUD) ------------
# QuestionAPI's QuestionIndexClient calls these after every admin create/update/delete so the
# ChromaDB index stays in sync with SQL Server without a full re-seed. Document ID = the SQL
# QuestionId (GUID) — the same scheme the batch seed (chroma_store.index_from_api) uses.
# The client only checks the HTTP status code, so a plain 200 + {"ok": true} is the contract.
class IndexUpsertIn(BaseModel):
    question_id: str
    question: str
    answer: str | None = None
    category: str | None = None
    difficulty: str | None = None
    position: str | None = None


@router.post("/questions/index/upsert")
def upsert_question_index(body: IndexUpsertIn, x_internal_api_key: str | None = Header(default=None)):
    _check_key(x_internal_api_key)
    question_id = body.question_id.strip()
    question = body.question.strip()
    if not question_id or not question:
        raise HTTPException(status_code=400, detail="question_id and question are required.")
    chroma_store.upsert_question(
        question_id=question_id,
        question=question,
        answer=body.answer,
        category=body.category,
        difficulty=body.difficulty,   # DB casing ("Junior") — normalized to lowercase inside
        position=body.position,
    )
    return {"ok": True}


@router.delete("/questions/index/{question_id}")
def delete_question_index(question_id: str, x_internal_api_key: str | None = Header(default=None)):
    _check_key(x_internal_api_key)
    chroma_store.delete_question(question_id.strip())
    return {"ok": True}


@router.delete("/questions/index")
def reset_question_index(x_internal_api_key: str | None = Header(default=None)):
    _check_key(x_internal_api_key)
    chroma_store.reset_index()
    return {"ok": True}
