"""
voice.py — Voice I/O: Deepgram Nova-3 (STT) + Piper (TTS, local) / ElevenLabs Flash v2.5 (TTS).
Location: ai-service/app/services/voice.py

Turn-based: transcribe a captured utterance, synthesize the interviewer reply.
(Full-duplex streaming with VAD/barge-in is a future enhancement.)

Dependencies: deepgram-sdk, piper-tts, elevenlabs.
NOTE: requires live API keys + real audio; the SDK calls are not unit-tested here.
API keys are read from the environment (DEEPGRAM_API_KEY / ELEVENLABS_API_KEY).
Piper runs fully offline — no API key, no network call.
"""
from __future__ import annotations

import io
import logging
import os
import subprocess
import wave
from typing import Iterator, Optional

from app.core import config

logger = logging.getLogger(__name__)


class DeepgramSTT:
    """Transcribe a single captured utterance (Deepgram Python SDK v6)."""

    def __init__(self, model: Optional[str] = None):
        self.model = model or config.DEEPGRAM_STT_MODEL
        self._client = None

    def _load(self) -> None:
        from deepgram import DeepgramClient
        self._client = DeepgramClient()  # reads DEEPGRAM_API_KEY from the environment

    def transcribe(self, audio_bytes: bytes, language: Optional[str] = None) -> str:
        # language=None (default) preserves existing behavior for callers that don't pass
        # it: the SDK omits the param and Deepgram defaults to English (language=en) for
        # every model, per Deepgram's docs. Pass an explicit BCP-47 code (e.g. "vi") for
        # non-English audio.
        if self._client is None:
            self._load()
        resp = self._client.listen.v1.media.transcribe_file(
            request=audio_bytes, model=self.model, language=language)
        return resp.results.channels[0].alternatives[0].transcript


class ElevenLabsTTS:
    """Stream interviewer speech as mp3 chunks (ElevenLabs Python SDK)."""

    def __init__(self, voice_id: Optional[str] = None, model: Optional[str] = None):
        self.voice_id = voice_id or config.ELEVENLABS_VOICE_ID
        self.model = model or config.ELEVENLABS_TTS_MODEL
        self._client = None

    def _load(self) -> None:
        from elevenlabs.client import ElevenLabs
        self._client = ElevenLabs(api_key=config.ELEVENLABS_API_KEY)

    def stream(self, text: str) -> Iterator[bytes]:
        if self._client is None:
            self._load()
        return self._client.text_to_speech.stream(
            text=text,
            voice_id=self.voice_id,
            model_id=self.model,
            output_format="mp3_44100_128",
        )

    def synthesize(self, text: str) -> bytes:
        """Gather the full MP3 from stream() — same interface as PiperTTS.synthesize(),
        used by the /tts route which needs complete bytes upfront to cache/return."""
        return b"".join(self.stream(text))


class PiperTTS:
    """Synthesize interviewer speech locally with Piper (offline neural TTS).

    Piper only produces WAV; the result is transcoded to mono 64kbps MP3 via
    ffmpeg (already in the image, see Dockerfile) so callers get the same MP3
    bytes ElevenLabsTTS produced.
    """

    def __init__(self, voice: Optional[str] = None, model_dir: Optional[str] = None,
                 speaker_id: Optional[int] = None):
        self.voice = voice or config.PIPER_VOICE
        self.model_dir = model_dir or config.PIPER_MODEL_DIR
        self.speaker_id = config.PIPER_SPEAKER_ID if speaker_id is None else speaker_id
        self._voice = None

    def _load(self) -> None:
        from piper import PiperVoice

        model_path = os.path.join(self.model_dir, f"{self.voice}.onnx")
        try:
            self._voice = PiperVoice.load(model_path)
        except Exception:
            logger.exception("Failed to load Piper voice model at %s", model_path)
            raise

    def synthesize(self, text: str) -> bytes:
        """Synthesize `text` to MP3 bytes. Loads the model on first call, then reuses it."""
        if self._voice is None:
            self._load()
        try:
            from piper import SynthesisConfig

            syn_config = SynthesisConfig(speaker_id=self.speaker_id)
            wav_buffer = io.BytesIO()
            with wave.open(wav_buffer, "wb") as wav_file:
                self._voice.synthesize_wav(text, wav_file, syn_config=syn_config)
            return self._wav_to_mp3(wav_buffer.getvalue())
        except Exception:
            logger.exception("Piper synthesis failed (text length=%d)", len(text))
            raise

    @staticmethod
    def _wav_to_mp3(wav_bytes: bytes) -> bytes:
        try:
            result = subprocess.run(
                [
                    "ffmpeg", "-hide_banner", "-loglevel", "error",
                    "-f", "wav", "-i", "pipe:0",
                    "-ac", "1", "-b:a", "64k",
                    "-f", "mp3", "pipe:1",
                ],
                input=wav_bytes,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=True,
            )
            return result.stdout
        except Exception:
            logger.exception("ffmpeg WAV-to-MP3 transcode failed")
            raise

    def stream(self, text: str) -> Iterator[bytes]:
        """Drop-in replacement for ElevenLabsTTS.stream(): Piper has no network
        streaming API, so this yields the fully-synthesized MP3 as a single chunk."""
        yield self.synthesize(text)


# Module-level singleton so app.main's lifespan warm-up and every caller of
# get_tts_provider() (e.g. app.api.interview) share the same loaded model —
# mirrors the _get_analyzer()/_get_encoder() singleton pattern in biometrics/.
_piper_tts: Optional[PiperTTS] = None


def _get_piper_tts() -> PiperTTS:
    global _piper_tts
    if _piper_tts is None:
        _piper_tts = PiperTTS()
    return _piper_tts


def get_tts_provider():
    """Select the TTS provider per config.TTS_PROVIDER ("piper" default, or "elevenlabs")."""
    if config.TTS_PROVIDER == "elevenlabs":
        return ElevenLabsTTS()
    return _get_piper_tts()


def warm_up_tts() -> None:
    """Load the configured TTS provider's model at startup so the first request is fast."""
    try:
        if config.TTS_PROVIDER == "piper":
            _get_piper_tts()._load()
        logger.info("TTS provider (%s) warmed up.", config.TTS_PROVIDER)
    except Exception:
        logger.exception("TTS warm-up failed")