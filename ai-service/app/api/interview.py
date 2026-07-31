"""
interview.py — Interview session API (chat + voice, realtime).
Location: ai-service/app/api/interview.py

Sessions are kept in memory keyed by session_id. Text answers go through /answer,
voice answers through /voice/answer (STT), and /tts streams the interviewer's voice
when the UI "AI Voice" toggle is on — text and voice are interchangeable per turn.
"""
from __future__ import annotations

import logging
import time
import uuid

from fastapi import APIRouter, BackgroundTasks, File, HTTPException, UploadFile
from fastapi.responses import JSONResponse, Response

from app.core import config
from app.schemas.interview import (
    AnswerIn, ReportIn, SessionConfigIn, StartResponse, TTSIn, TurnResponse, VoiceAnswerResponse,
)
from app.services import tts_cache
from app.services.interviewer import SessionConfig, create_session
from app.services.voice import DeepgramSTT, get_tts_provider

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/interview", tags=["interview"])

_sessions: dict[str, dict] = {}   # session_id -> {"session": InterviewSession, "created_at": float}
_stt = DeepgramSTT()
_tts = get_tts_provider()  # config.TTS_PROVIDER: "piper" (default, local) or "elevenlabs"
_judge = None   # shared lazy Gemini judge for background grading


def _get_judge():
    global _judge
    if _judge is None:
        from app.services.evaluator import GeminiJudge
        _judge = GeminiJudge()
    return _judge


def _grade_into_session(session, item: dict) -> None:
    """Grade one answered item and stash it on the session (runs in the background)."""
    from app.services import evaluator
    try:
        graded = evaluator.grade_item(item, _get_judge(), evaluator.enrich_for(item))
        if graded:
            session.graded[item["qid"]] = graded
    except Exception:
        pass  # leave ungraded; the report endpoint will retry synchronously as a fallback


def _schedule_grading(session, background_tasks: BackgroundTasks) -> None:
    for item in session.pending_grading():
        background_tasks.add_task(_grade_into_session, session, item)


def _get(session_id: str) -> dict:
    entry = _sessions.get(session_id)
    if not entry:
        raise HTTPException(status_code=404, detail="Session not found")
    return entry


def _turn_resp(turn) -> TurnResponse:
    return TurnResponse(message=turn.message, ended=turn.ended,
                        question_number=turn.question_number, total_questions=turn.total_questions)


@router.post("/start", response_model=StartResponse)
def start(cfg: SessionConfigIn):
    try:
        session = create_session(SessionConfig(**cfg.model_dump()))
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))
    turn = session.start()
    sid = uuid.uuid4().hex
    _sessions[sid] = {"session": session, "created_at": time.time()}
    return StartResponse(session_id=sid, message=turn.message, question=turn.question,
                         ended=turn.ended, question_number=turn.question_number,
                         total_questions=turn.total_questions, scenario=turn.scenario)


@router.post("/{session_id}/answer", response_model=TurnResponse)
def answer(session_id: str, body: AnswerIn, background_tasks: BackgroundTasks):
    session = _get(session_id)["session"]
    turn = session.submit_answer(body.answer)
    _schedule_grading(session, background_tasks)
    return _turn_resp(turn)


@router.post("/{session_id}/voice/answer", response_model=VoiceAnswerResponse)
async def voice_answer(session_id: str, background_tasks: BackgroundTasks, audio: UploadFile = File(...)):
    session = _get(session_id)["session"]
    text = _stt.transcribe(await audio.read())
    turn = session.submit_answer(text)
    _schedule_grading(session, background_tasks)
    return VoiceAnswerResponse(user_text=text, message=turn.message, ended=turn.ended,
                               question_number=turn.question_number, total_questions=turn.total_questions)


@router.post("/{session_id}/hint")
def hint(session_id: str):
    return {"hint": _get(session_id)["session"].hint()}


@router.post("/{session_id}/skip", response_model=TurnResponse)
def skip(session_id: str, background_tasks: BackgroundTasks):
    session = _get(session_id)["session"]
    turn = session.skip()
    _schedule_grading(session, background_tasks)
    return _turn_resp(turn)


@router.post("/{session_id}/end", response_model=TurnResponse)
def end(session_id: str, background_tasks: BackgroundTasks):
    session = _get(session_id)["session"]
    turn = session.end()
    _schedule_grading(session, background_tasks)   # grade the last answered item too
    return _turn_resp(turn)


@router.post("/{session_id}/report")
def report(session_id: str, body: ReportIn):
    """Build the final report from per-item grades computed in the background during the
    interview; any item not graded yet is graded now (synchronous fallback)."""
    from app.services import evaluator
    session = _get(session_id)["session"]
    judge = _get_judge()
    reports = []
    for item in session.public_items():
        rep = session.graded.get(item["qid"])
        if rep is None:
            rep = evaluator.grade_item(item, judge, evaluator.enrich_for(item))
            if rep:
                session.graded[item["qid"]] = rep
        if rep:
            reports.append(rep)
    return evaluator.build_report(reports, judge, body.duration_actual_sec)


@router.get("/{session_id}/transcript")
def transcript(session_id: str):
    entry = _get(session_id)
    elapsed = int(time.time() - entry["created_at"])
    return entry["session"].transcript(elapsed)


def _tts_identity() -> tuple[str, str]:
    """(voice, speaker_id) the active _tts provider synthesizes with — duck-typed so this
    works for both PiperTTS (.voice/.speaker_id) and ElevenLabsTTS (.voice_id, no speaker)
    without importing either class. Part of the cache key: MUST reflect the real provider
    config or a voice change would keep serving audio cached under the old voice."""
    voice = getattr(_tts, "voice", None) or getattr(_tts, "voice_id", "") or ""
    speaker_id = getattr(_tts, "speaker_id", "")
    return voice, str(speaker_id)


@router.post("/tts")
def tts(body: TTSIn):
    """Synthesize the interviewer's speech (used when the 'AI Voice' toggle is on).

    Always returns a full audio/mpeg body or a JSON error — never a 200 with an empty
    body (the previous StreamingResponse silently swallowed provider errors that way).
    """
    start = time.monotonic()
    voice, speaker_id = _tts_identity()
    key = tts_cache.cache_key(body.text, config.TTS_PROVIDER, voice, speaker_id)

    cached = tts_cache.get(key)
    if cached is not None:
        return Response(
            content=cached,
            media_type="audio/mpeg",
            headers={
                "X-TTS-Cache": "HIT",
                "X-TTS-Provider": config.TTS_PROVIDER,
                "X-TTS-Latency-Ms": str(int((time.monotonic() - start) * 1000)),
            },
        )

    try:
        audio = _tts.synthesize(body.text)
    except Exception:
        logger.exception("TTS synthesis failed (provider=%s)", config.TTS_PROVIDER)
        return JSONResponse(
            status_code=502,
            content={"error": "tts_unavailable"},
            headers={
                "X-TTS-Cache": "MISS",
                "X-TTS-Provider": config.TTS_PROVIDER,
                "X-TTS-Latency-Ms": str(int((time.monotonic() - start) * 1000)),
            },
        )

    tts_cache.put(key, audio)
    return Response(
        content=audio,
        media_type="audio/mpeg",
        headers={
            "X-TTS-Cache": "MISS",
            "X-TTS-Provider": config.TTS_PROVIDER,
            "X-TTS-Latency-Ms": str(int((time.monotonic() - start) * 1000)),
        },
    )