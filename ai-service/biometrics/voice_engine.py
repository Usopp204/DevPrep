import logging
import os
import tempfile
import threading

import numpy as np

from .config import (
    VOICE_MODEL_SOURCE, VOICE_MODEL_SAVEDIR,
    VOICE_MIN_DURATION_SEC, VOICE_TARGET_SR,
    VOICE_MIN_SPEECH_SEC, VOICE_SPEECH_ENERGY_THRESHOLD,
    VOICE_SPEECH_FLOOR_MARGIN_DB, VOICE_MIN_DYNAMIC_RANGE_DB,
)

logger = logging.getLogger(__name__)

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


# 30ms frames: long enough for a stable RMS estimate, short enough to resolve the 3-8 Hz
# syllable modulation that distinguishes speech from steady noise. Shared by _frame_rms and
# analyze_speech_presence so the frame duration used to measure and to convert frame counts
# back into seconds can never drift apart.
_FRAME_MS = 30.0


def _frame_rms(wav: np.ndarray, sr: int, frame_ms: float = _FRAME_MS) -> np.ndarray:
    """Per-frame RMS amplitude (non-overlapping frames), used to tell speech from silence.
    Frames shorter than one full window at the tail are dropped rather than padded — a partial
    frame's RMS would be biased by zero-padding, and losing <30ms off the end doesn't matter."""
    frame_len = max(1, int(sr * frame_ms / 1000.0))
    n_frames = len(wav) // frame_len
    if n_frames == 0:
        return np.array([], dtype=np.float64)
    trimmed = wav[: n_frames * frame_len].astype(np.float64).reshape(n_frames, frame_len)
    return np.sqrt(np.mean(trimmed ** 2, axis=1))


def analyze_speech_presence(wav: np.ndarray, sr: int) -> dict:
    """Measure whether a recording contains speech, as opposed to silence OR steady noise.

    An absolute energy threshold cannot make this distinction: a desk fan puts continuous
    energy above any level low enough to still accept a quiet speaker, so tuning on loudness
    trades one failure for the other. What separates them is the ENVELOPE SHAPE over time —
    speech alternates syllables and pauses, giving a wide spread between its quiet and loud
    frames, while a fan sits at one level for the whole recording. Both measures below are
    therefore relative to the recording's own levels, which is what makes them independent of
    how loud the speaker is:

      dynamic_range_db : p95 - p10 of per-frame dB. The primary speech/noise discriminator.
                         Speech typically 15-30 dB; steady noise 1-3 dB; digital silence 0.
      speech_sec       : time in frames sitting VOICE_SPEECH_FLOOR_MARGIN_DB above this
                         recording's own noise floor (p10). For a fan every frame is at the
                         floor, so this lands near zero however loud the fan is; for someone
                         speaking over a running fan, the fan becomes the floor and the speech
                         still rises above it — which is the case that must keep working.

    Honest limits: this detects MODULATED SOUND, not speech specifically. Music, typing, or a
    dog barking would also clear it, and it says nothing about WHAT was said (the challenge
    phrase check in liveness_voice.py is the only thing that does). It is a quality gate for
    "did the microphone capture a voice", not an anti-spoofing control.
    """
    frames = _frame_rms(wav, sr)
    frame_len = max(1, int(sr * _FRAME_MS / 1000.0))
    if frames.size == 0:
        return {"speech_sec": 0.0, "dynamic_range_db": 0.0, "noise_floor_db": -120.0,
                "peak_db": -120.0, "mean_rms": 0.0, "peak_rms": 0.0}

    # +1e-10 keeps true digital silence (rms == 0) finite instead of -inf, so the percentile
    # arithmetic below stays well-defined and simply reports a 0 dB range for it.
    frames_db = 20.0 * np.log10(frames + 1e-10)
    noise_floor_db = float(np.percentile(frames_db, 10))
    peak_db = float(np.percentile(frames_db, 95))

    speech_frames = int(np.sum(frames_db >= noise_floor_db + VOICE_SPEECH_FLOOR_MARGIN_DB))
    return {
        "speech_sec": float(speech_frames * frame_len / sr),
        "dynamic_range_db": peak_db - noise_floor_db,
        "noise_floor_db": noise_floor_db,
        "peak_db": peak_db,
        "mean_rms": float(np.mean(frames)),
        "peak_rms": float(np.max(frames)),
    }


def extract_voice_embedding(audio_bytes: bytes) -> dict:
    """Decode, quality-gate, and embed one voice sample.

    Shared by both callers: profile enrolment (ProfileBiometricsService.EnrollAsync, via
    /internal/biometrics/voice/embed) and BiometricVerificationService.VerifyVoiceAnswerAsync's
    in-session probe (via /internal/biometrics/verify). The speech-presence gate below therefore
    applies to both — tightening VOICE_MIN_SPEECH_SEC / VOICE_SPEECH_ENERGY_THRESHOLD later to
    catch more silence at enrolment also makes VerifyVoiceAnswerAsync more likely to reject a
    genuine but quiet/weak-mic answer as an identity-mismatch strike. That path already has no
    challenge phrase to fall back on for the quiet-mic case, so raise these thresholds carefully.
    """
    try:
        wav = load_audio_mono_16k(audio_bytes)
    except Exception as exc:
        raise VoiceQualityError("Cannot decode audio") from exc
    duration = float(len(wav) / VOICE_TARGET_SR)
    if duration < VOICE_MIN_DURATION_SEC:
        raise VoiceQualityError(f"Audio too short ({duration:.1f}s, need >= {VOICE_MIN_DURATION_SEC}s)")

    # Speech-presence gate: VOICE_MIN_DURATION_SEC above only checks the recording is long
    # enough — a silent recording padded to 4+ seconds by the browser's own MIN_RECORD_SECONDS
    # clears it easily and, before this gate existed, went straight into a "valid" embedding.
    # Every measured value is logged at Information on every call that reaches here, pass or
    # fail: these thresholds can only be tuned from real recordings, not guessed, and this line
    # is the evidence for that. See analyze_speech_presence for why the discriminating measure
    # is dB range rather than absolute level.
    m = analyze_speech_presence(wav, VOICE_TARGET_SR)

    # Three independent reasons to reject, evaluated together so the log always reports all of
    # them rather than only the first to trip.
    dead_mic = m["peak_rms"] < VOICE_SPEECH_ENERGY_THRESHOLD
    too_flat = m["dynamic_range_db"] < VOICE_MIN_DYNAMIC_RANGE_DB
    too_little_speech = m["speech_sec"] < VOICE_MIN_SPEECH_SEC
    passed = not (dead_mic or too_flat or too_little_speech)

    if passed:
        verdict = "PASS"
    elif dead_mic:
        verdict = "REJECTED (dead mic / digital silence)"
    elif too_flat:
        verdict = "REJECTED (steady noise, no speech modulation)"
    else:
        verdict = "REJECTED (too little speech)"

    logger.info(
        "Voice sample quality: duration=%.2fs speech=%.2fs (min=%.2fs) "
        "dynamic_range=%.1fdB (min=%.1fdB) noise_floor=%.1fdB peak=%.1fdB "
        "mean_rms=%.4f peak_rms=%.4f (dead_mic_floor=%.4f, speech_margin=%.1fdB) -> %s",
        duration, m["speech_sec"], VOICE_MIN_SPEECH_SEC,
        m["dynamic_range_db"], VOICE_MIN_DYNAMIC_RANGE_DB,
        m["noise_floor_db"], m["peak_db"], m["mean_rms"], m["peak_rms"],
        VOICE_SPEECH_ENERGY_THRESHOLD, VOICE_SPEECH_FLOOR_MARGIN_DB, verdict)

    if not passed:
        # Two different user-facing messages: "nothing came through at all" and "something came
        # through but it wasn't a voice" call for different corrective action from the user.
        if too_flat and not dead_mic:
            raise VoiceQualityError(
                "No speech detected — the microphone picked up steady background noise only. "
                "Please speak clearly during the recording.")
        raise VoiceQualityError(
            "No speech detected in this recording. Please speak clearly and make sure your "
            "microphone is on.")

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
