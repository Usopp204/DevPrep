"""
router.py — Internal biometrics API consumed by the .NET InterviewAPI.
Location: ai-service/biometrics/router.py

Stateless: extracts face/voice embeddings and verifies a probe sample against a
stored embedding. Auth is the shared header X-Internal-Api-Key (see security.py).
"""
import json
import logging

from fastapi import APIRouter, Depends, File, Form, HTTPException, UploadFile

from .config import (
    FACE_MATCH_THRESHOLD, FACE_MODEL_VERSION,
    VOICE_MATCH_THRESHOLD, VOICE_MODEL_VERSION,
    load_calibration,
)
from .face_engine import FaceQualityError, MultipleFacesError, extract_face_embedding
from .liveness_face import verify_face_live
from .liveness_voice import verify_voice_challenge
from .schemas import (
    FaceEmbedResponse, FaceVerifyLiveResponse,
    VerifyResponse, VoiceEmbedResponse, VoiceVerifyChallengeResponse,
)
from .security import require_internal_api_key
from .similarity import cosine_similarity
from .voice_engine import VoiceQualityError, extract_voice_embedding
from app.services.voice import DeepgramSTT

logger = logging.getLogger(__name__)

# Reuse the existing Deepgram integration for the voice-challenge transcript
# (same STT that backs /internal/stt — no second STT integration).
_stt = DeepgramSTT()

router = APIRouter(prefix="/internal/biometrics", tags=["biometrics"],
                   dependencies=[Depends(require_internal_api_key)])


def _threshold_for(modality: str) -> float:
    """Prefer models/calibration.json (Milestone 3) over the fixed defaults."""
    calib = load_calibration() or {}
    threshold = (calib.get(modality) or {}).get("threshold")
    if isinstance(threshold, (int, float)):
        return float(threshold)
    return FACE_MATCH_THRESHOLD if modality == "face" else VOICE_MATCH_THRESHOLD


@router.post("/face/embed", response_model=FaceEmbedResponse)
async def embed_face(file: UploadFile = File(...)):
    data = await file.read()
    try:
        result = extract_face_embedding(data)
    except FaceQualityError as exc:
        raise HTTPException(status_code=422, detail=str(exc))
    return FaceEmbedResponse(**result, model_version=FACE_MODEL_VERSION)


@router.post("/voice/embed", response_model=VoiceEmbedResponse)
async def embed_voice(file: UploadFile = File(...)):
    data = await file.read()
    try:
        result = extract_voice_embedding(data)
    except VoiceQualityError as exc:
        raise HTTPException(status_code=422, detail=str(exc))
    return VoiceEmbedResponse(**result, model_version=VOICE_MODEL_VERSION)


def _parse_stored_embedding(stored_embedding: str) -> list:
    try:
        stored = json.loads(stored_embedding)
    except json.JSONDecodeError:
        raise HTTPException(status_code=422, detail="stored_embedding must be a JSON array.")
    if not isinstance(stored, list) or not stored:
        raise HTTPException(status_code=422, detail="stored_embedding must be a non-empty JSON array.")
    return stored


@router.post("/verify", response_model=VerifyResponse)
async def verify(
    modality: str = Form(...),
    stored_embedding: str = Form(...),  # JSON array of floats
    file: UploadFile = File(...),
):
    modality = modality.strip().lower()
    if modality not in ("face", "voice"):
        raise HTTPException(status_code=422, detail="modality must be 'face' or 'voice'.")
    stored = _parse_stored_embedding(stored_embedding)

    data = await file.read()
    try:
        if modality == "face":
            probe = extract_face_embedding(data)["embedding"]
        else:
            probe = extract_voice_embedding(data)["embedding"]
    except MultipleFacesError as exc:
        # Structured detail (not just a string) — the in-session cheating check is the only
        # caller of this endpoint today and needs the actual face_count, not a message to parse.
        raise HTTPException(status_code=422, detail={
            "code": "multiple_faces",
            "face_count": exc.face_count,
            "message": str(exc),
        })
    except (FaceQualityError, VoiceQualityError) as exc:
        raise HTTPException(status_code=422, detail=str(exc))

    score = cosine_similarity(probe, stored)
    threshold = _threshold_for(modality)
    return VerifyResponse(match=score >= threshold, score=round(score, 4), threshold=threshold)


@router.post("/face/verify-live", response_model=FaceVerifyLiveResponse)
async def verify_face_live_endpoint(
    frames: list[UploadFile] = File(...),
    stored_embedding: str = Form(...),  # JSON array of floats
    challenge: str = Form(...),         # JSON object, e.g. {"type":"turn_head","direction":"right"}
):
    stored = _parse_stored_embedding(stored_embedding)
    try:
        challenge_obj = json.loads(challenge)
    except json.JSONDecodeError:
        raise HTTPException(status_code=422, detail="challenge must be a JSON object.")
    if not isinstance(challenge_obj, dict):
        raise HTTPException(status_code=422, detail="challenge must be a JSON object.")

    frame_bytes = [await f.read() for f in frames]
    try:
        result = verify_face_live(frame_bytes, stored, challenge_obj)
    except FaceQualityError as exc:
        raise HTTPException(status_code=422, detail=str(exc))

    threshold = _threshold_for("face")
    identity = result.get("identity_score")
    # Liveness (`result["live"]`) is intentionally NOT part of the pass/fail decision — the yaw
    # measurement it depends on is broken on real hardware: real-rig data (2026-07-24) showed
    # YawRange of just 0.55-1.61° during a clear, deliberate head turn (the 18° magnitude
    # threshold assumes a working signal; it isn't one — see liveness_face.py). Entry gate is
    # face-match only for now. `live`/`yaw_range`/`moved_enough`/`direction_ok` are still
    # computed and returned/recorded (Detail JSON on the .NET side) purely as diagnostics, in
    # case someone investigates and re-enables liveness later.
    # TODO: investigate face.pose indexing / frame capture timing before re-enabling liveness.
    passed = bool(identity is not None and identity >= threshold)
    return FaceVerifyLiveResponse(passed=passed, threshold=threshold, **result)


@router.post("/voice/verify-challenge", response_model=VoiceVerifyChallengeResponse)
async def verify_voice_challenge_endpoint(
    file: UploadFile = File(...),
    stored_embedding: str = Form(...),  # JSON array of floats
    expected_phrase: str = Form(...),
):
    stored = _parse_stored_embedding(stored_embedding)
    data = await file.read()
    try:
        # Challenge phrases are Vietnamese; force language=vi (Deepgram defaults to
        # English otherwise, which garbled Vietnamese speech into unrelated English words).
        result = verify_voice_challenge(
            data, stored, expected_phrase, lambda b: _stt.transcribe(b, language="vi"))
    except VoiceQualityError as exc:
        raise HTTPException(status_code=422, detail=str(exc))

    threshold = _threshold_for("voice")
    passed = bool(result["speaker_score"] >= threshold and result["content_ok"])
    return VoiceVerifyChallengeResponse(passed=passed, threshold=threshold, **result)


def warm_up():
    """Load both models at startup (in a background thread) so the first request is fast."""
    try:
        from .face_engine import _get_analyzer
        from .voice_engine import _get_encoder
        _get_analyzer()
        _get_encoder()
        logger.info("Biometrics models warmed up.")
    except Exception:
        logger.exception("Biometrics model warm-up failed")
