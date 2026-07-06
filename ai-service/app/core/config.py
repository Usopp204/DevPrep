"""
config.py — Centralized environment configuration.
Location: ai-service/app/core/config.py
"""
import os

# --- Evaluator judge (Gemini) ---
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY", "")
# Set this to the exact Gemini 3.1 Pro model string available to your account.
GEMINI_JUDGE_MODEL = os.getenv("GEMINI_JUDGE_MODEL", "gemini-3.1-pro")

# --- Voice: STT (Deepgram) ---
# "or" (not the getenv default) so a SET-BUT-EMPTY env var (e.g. "ELEVENLABS_TTS_MODEL=" in .env)
# still falls back to the default instead of silently sending "" to the provider.
DEEPGRAM_API_KEY = os.getenv("DEEPGRAM_API_KEY", "")
DEEPGRAM_STT_MODEL = os.getenv("DEEPGRAM_STT_MODEL") or "nova-3"

# --- Voice: TTS (ElevenLabs) ---
ELEVENLABS_API_KEY = os.getenv("ELEVENLABS_API_KEY", "")
ELEVENLABS_VOICE_ID = os.getenv("ELEVENLABS_VOICE_ID") or "JBFqnCBsd6RMkjVDRZzb"  # default interviewer voice
ELEVENLABS_TTS_MODEL = os.getenv("ELEVENLABS_TTS_MODEL") or "eleven_flash_v2_5"

# --- Internal service-to-service auth ---
# The .NET services call ai-service with header X-Internal-Api-Key matching this value.
INTERNAL_API_KEY = os.getenv("INTERNAL_API_KEY", "")

# --- API ---
ALLOWED_ORIGINS = os.getenv("ALLOWED_ORIGINS", "*").split(",")