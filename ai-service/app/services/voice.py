"""
voice.py — Voice I/O: Deepgram Nova-3 (STT) + ElevenLabs Flash v2.5 (TTS).
Location: ai-service/app/services/voice.py

Turn-based: transcribe a captured utterance, synthesize the interviewer reply.
(Full-duplex streaming with VAD/barge-in is a future enhancement.)

Dependencies: deepgram-sdk, elevenlabs.
NOTE: requires live API keys + real audio; the SDK calls are not unit-tested here.
API keys are read from the environment (DEEPGRAM_API_KEY / ELEVENLABS_API_KEY).
"""
from __future__ import annotations

from typing import Iterator, Optional

from app.core import config


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