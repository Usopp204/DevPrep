"""
tts_cache.py — Disk cache for synthesized TTS audio.
Location: ai-service/app/services/tts_cache.py

Interview questions repeat text across sessions (same scenario/level), so caching
the synthesized MP3 bytes on disk skips re-running Piper (or paying ElevenLabs) for
text already spoken before.

Cache errors must never break the TTS request path: every disk operation is wrapped
in try/except and degrades to a cache miss (get -> None, put -> no-op) on failure.
"""
from __future__ import annotations

import hashlib
import logging
import os
import re

from app.core import config

logger = logging.getLogger(__name__)

_WHITESPACE_RE = re.compile(r"\s+")


def _normalize(text: str) -> str:
    return _WHITESPACE_RE.sub(" ", text.strip()).lower()


def cache_key(text: str, provider: str, voice: str, speaker_id) -> str:
    """sha256 of the normalized text plus every parameter that changes the audio
    output. voice/speaker_id MUST be part of the key — otherwise switching voices
    would keep serving cached audio recorded with the old one."""
    normalized = _normalize(text)
    raw = f"{normalized}|{provider}|{voice}|{speaker_id}"
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def _path_for(key: str) -> str:
    return os.path.join(config.TTS_CACHE_DIR, f"{key}.mp3")


def get(key: str) -> bytes | None:
    if not config.TTS_CACHE_ENABLED:
        return None
    try:
        path = _path_for(key)
        if not os.path.isfile(path):
            return None
        with open(path, "rb") as f:
            return f.read()
    except Exception:
        logger.warning("TTS cache read failed for key %s", key, exc_info=True)
        return None


def put(key: str, data: bytes) -> None:
    if not config.TTS_CACHE_ENABLED:
        return
    try:
        os.makedirs(config.TTS_CACHE_DIR, exist_ok=True)
        path = _path_for(key)
        # Unique tmp name (pid-suffixed) so two concurrent misses for the same key
        # can't interleave writes to the same inode; os.replace() is still what
        # makes the final file atomic for readers.
        tmp_path = f"{path}.{os.getpid()}.tmp"
        with open(tmp_path, "wb") as f:
            f.write(data)
        os.replace(tmp_path, path)
    except Exception:
        logger.warning("TTS cache write failed for key %s", key, exc_info=True)
