import os
import tempfile
import threading

import numpy as np

from .config import (
    VOICE_MODEL_SOURCE, VOICE_MODEL_SAVEDIR,
    VOICE_MIN_DURATION_SEC, VOICE_TARGET_SR,
)

_lock = threading.Lock()
_encoder = None


def _get_encoder():
    global _encoder
    if _encoder is None:
        with _lock:
            if _encoder is None:
                try:
                    from speechbrain.inference.speaker import EncoderClassifier
                except ImportError:
                    from speechbrain.pretrained import EncoderClassifier
                _encoder = EncoderClassifier.from_hparams(
                    source=VOICE_MODEL_SOURCE, savedir=VOICE_MODEL_SAVEDIR,
                    run_opts={"device": "cpu"})
    return _encoder


class VoiceQualityError(ValueError):
    pass


def load_audio_mono_16k(audio_bytes: bytes) -> np.ndarray:
    import librosa

    # libsndfile (soundfile) can't decode WebM/Opus (MediaRecorder's default browser output),
    # so librosa falls back to the audioread ffmpeg backend — which shells out to the `ffmpeg`
    # binary and needs a real file path, not a BytesIO. Write to a temp file first.
    fd, tmp_path = tempfile.mkstemp(suffix=".webm")
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(audio_bytes)
        wav, _ = librosa.load(tmp_path, sr=VOICE_TARGET_SR, mono=True)
    finally:
        os.remove(tmp_path)
    return wav.astype(np.float32)


def extract_voice_embedding(audio_bytes: bytes) -> dict:
    try:
        wav = load_audio_mono_16k(audio_bytes)
    except Exception as exc:
        raise VoiceQualityError("Cannot decode audio") from exc
    duration = float(len(wav) / VOICE_TARGET_SR)
    if duration < VOICE_MIN_DURATION_SEC:
        raise VoiceQualityError(f"Audio too short ({duration:.1f}s, need >= {VOICE_MIN_DURATION_SEC}s)")

    import torch
    signal = torch.from_numpy(wav).unsqueeze(0)
    with torch.no_grad():
        emb = _get_encoder().encode_batch(signal)
    emb = emb.squeeze().cpu().numpy().astype(np.float32)
    emb = emb / (np.linalg.norm(emb) + 1e-8)

    # Quality heuristic (informational, not a gate): duration relative to the minimum,
    # saturating at 1.0 once the recording is comfortably longer than required. Cheap and
    # deterministic; not derived from real calibration data (that's Milestone 3).
    quality = float(min(1.0, duration / (VOICE_MIN_DURATION_SEC * 2)))

    return {"embedding": emb.tolist(), "dim": int(emb.shape[0]),
            "duration_sec": round(duration, 2), "quality": round(quality, 4)}
