"""
config.py — Centralized environment configuration.
Location: ai-service/app/core/config.py
"""
import os
from pathlib import Path

from dotenv import load_dotenv
from pydantic_settings import BaseSettings, SettingsConfigDict

ROOT_ENV_PATH = Path(__file__).resolve().parents[3] / ".env"
load_dotenv(ROOT_ENV_PATH)


class Settings(BaseSettings):
    app_name: str = "DevPrep AI Service"
    host: str = "0.0.0.0"
    port: int = 8000

    model_config = SettingsConfigDict(
        env_file=str(ROOT_ENV_PATH),
        env_prefix="AI_",
        extra="ignore",
    )


settings = Settings()

# --- Evaluator judge (Gemini) ---
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY", "")
# Set this to the exact Gemini 3.1 Pro model string available to your account.
GEMINI_JUDGE_MODEL = os.getenv("GEMINI_JUDGE_MODEL", "gemini-3.1-pro")

# --- Voice: STT (Deepgram) ---
# "or" (not the getenv default) so a SET-BUT-EMPTY env var (e.g. "ELEVENLABS_TTS_MODEL=" in .env)
# still falls back to the default instead of silently sending "" to the provider.
DEEPGRAM_API_KEY = os.getenv("DEEPGRAM_API_KEY", "")
DEEPGRAM_STT_MODEL = os.getenv("DEEPGRAM_STT_MODEL") or "nova-3"

# --- Voice: TTS provider select ---
# "piper" (default): local, offline, no API key. "elevenlabs": cloud, needs ELEVENLABS_API_KEY.
TTS_PROVIDER = os.getenv("TTS_PROVIDER") or "piper"

# --- Voice: TTS (Piper, local) ---
# Voice model baked into the image at build time (see Dockerfile). PIPER_VOICE is the
# basename (no extension) of the .onnx/.onnx.json pair under PIPER_MODEL_DIR.
# en_US-libritts_r-medium is a multi-speaker model (904 speakers, CC BY 4.0 dataset
# license) — PIPER_SPEAKER_ID picks which speaker to synthesize with.
PIPER_VOICE = os.getenv("PIPER_VOICE") or "en_US-libritts_r-medium"
PIPER_MODEL_DIR = os.getenv("PIPER_MODEL_DIR") or "/app/models/piper"
PIPER_SPEAKER_ID = int(os.getenv("PIPER_SPEAKER_ID") or "0")

# --- Voice: TTS disk cache (tts_cache.py) ---
TTS_CACHE_DIR = os.getenv("TTS_CACHE_DIR") or "tts_cache"
TTS_CACHE_ENABLED = (os.getenv("TTS_CACHE_ENABLED", "true").strip().lower() != "false")

# --- Voice: TTS (ElevenLabs) ---
ELEVENLABS_API_KEY = os.getenv("ELEVENLABS_API_KEY", "")
ELEVENLABS_VOICE_ID = os.getenv("ELEVENLABS_VOICE_ID") or "JBFqnCBsd6RMkjVDRZzb"  # default interviewer voice
ELEVENLABS_TTS_MODEL = os.getenv("ELEVENLABS_TTS_MODEL") or "eleven_flash_v2_5"

# --- Internal service-to-service auth ---
# The .NET services call ai-service with header X-Internal-Api-Key matching this value.
INTERNAL_API_KEY = os.getenv("INTERNAL_API_KEY", "")

# --- API ---
ALLOWED_ORIGINS = os.getenv("ALLOWED_ORIGINS", "*").split(",")

# --- Logging ---
# Level for THIS project's own loggers (the "app" and "biometrics" namespaces — see
# app/main.py's _configure_logging). Does not touch root or third-party loggers (torch,
# speechbrain, transformers, httpx, ...), which stay at Python's default and would otherwise
# drown our own lines in library chatter at INFO. Configurable so a noisy-in-practice level
# can be dropped without a rebuild — see _configure_logging for why this matters here.
LOG_LEVEL = os.getenv("LOG_LEVEL") or "INFO"
