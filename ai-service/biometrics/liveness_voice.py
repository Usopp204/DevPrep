import re
import unicodedata
from difflib import SequenceMatcher

from .config import VOICE_CONTENT_MIN_SIMILARITY
from .similarity import cosine_similarity
from .voice_engine import extract_voice_embedding


def _normalize_vi(text: str) -> str:
    text = unicodedata.normalize("NFKC", text or "").lower().strip()
    text = re.sub(r"[^\w\s]", " ", text)
    return re.sub(r"\s+", " ", text).strip()


def phrase_similarity(a: str, b: str) -> float:
    na, nb = _normalize_vi(a), _normalize_vi(b)
    if not na or not nb:
        return 0.0
    seq = SequenceMatcher(None, na, nb).ratio()
    wa, wb = set(na.split()), set(nb.split())
    jacc = len(wa & wb) / len(wa | wb) if (wa | wb) else 0.0
    return max(seq, jacc)  # lenient: STT errors shouldn't fail a genuine read


def verify_voice_challenge(audio_bytes: bytes, stored_embedding, expected_phrase: str,
                           transcribe_fn) -> dict:
    """transcribe_fn(audio_bytes) -> Vietnamese transcript. Pass the EXISTING Deepgram STT function
    used by /internal/stt so we don't add a second STT integration."""
    probe = extract_voice_embedding(audio_bytes)["embedding"]
    speaker_score = cosine_similarity(probe, stored_embedding)
    transcript = transcribe_fn(audio_bytes) or ""
    content_score = phrase_similarity(transcript, expected_phrase)
    content_ok = content_score >= VOICE_CONTENT_MIN_SIMILARITY
    return {"speaker_score": round(speaker_score, 4),
            "content_score": round(content_score, 4),
            "content_ok": content_ok, "transcript": transcript}
