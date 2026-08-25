import json
import os

INTERNAL_API_KEY = os.getenv("INTERNAL_API_KEY", "")

FACE_MODEL_PACK = os.getenv("FACE_MODEL_PACK", "buffalo_l")
FACE_MODEL_ROOT = os.getenv("INSIGHTFACE_ROOT", "models/insightface")
VOICE_MODEL_SOURCE = os.getenv("VOICE_MODEL_SOURCE", "speechbrain/spkrec-ecapa-voxceleb")
VOICE_MODEL_SAVEDIR = os.getenv("ECAPA_SAVEDIR", "models/ecapa-voxceleb")

FACE_MODEL_VERSION = "insightface-buffalo_l-arcface-r50-v1"
VOICE_MODEL_VERSION = "speechbrain-ecapa-voxceleb-v1"

# Fixed fallback thresholds; overridden by models/calibration.json when present (Milestone 3).
FACE_MATCH_THRESHOLD = float(os.getenv("FACE_MATCH_THRESHOLD", "0.35"))
VOICE_MATCH_THRESHOLD = float(os.getenv("VOICE_MATCH_THRESHOLD", "0.25"))

# Quality gates
FACE_MIN_DET_SCORE = 0.60
FACE_MIN_FACE_RATIO = 0.05
FACE_MIN_BLUR_VAR = 40.0
VOICE_MIN_DURATION_SEC = 3.0
VOICE_TARGET_SR = 16000

# Speech-presence gate (as opposed to VOICE_MIN_DURATION_SEC above, which only checks the
# recording is long enough — silence padded out to 4+ seconds by the browser's own
# MIN_RECORD_SECONDS passes that check easily). Env vars, not fixed constants like the pair
# above, because the right values can only be found by testing against real recordings
# (quiet rooms, weak mics) and rebuilding the ai-service image per trial is the slowest
# feedback loop in this project.
#
# ABSOLUTE-LEVEL THRESHOLDS CANNOT DO THIS JOB — that was the first attempt's mistake. A
# desk fan produces continuous energy above any absolute threshold set low enough to accept a
# quiet speaker, so raising the bar rejects real users before it rejects the fan. What
# actually separates the two is the SHAPE of the energy envelope over time, not its height:
# speech is bursty (syllables and pauses give a wide spread between quiet and loud frames),
# steady noise is flat (every frame sits at roughly the same level). The gate below therefore
# measures a per-frame dB range and an adaptive, noise-floor-relative speech duration — both
# level-independent, so a quiet speaker and a loud speaker are judged the same way, and a fan
# fails no matter how loud it is. See voice_engine.py's per-sample log line for the measured
# numbers to tune these from.
#
# Started deliberately permissive: rejecting a legitimate user at this MANDATORY enrolment
# step blocks them from interviewing at all, which is far worse than occasionally accepting a
# marginal recording. Real speech typically measures 15-30 dB of range; steady noise measures
# 1-3 dB — the 6.0 dB default sits in the wide gap between them, not near either.
#
# Shared by extract_voice_embedding() — see that function's docstring for why tightening any
# of these also affects VerifyVoiceAnswerAsync's in-session probe extraction, not just
# enrolment.
VOICE_MIN_SPEECH_SEC = float(os.getenv("VOICE_MIN_SPEECH_SEC", "1.0"))

# How far above the recording's OWN noise floor a frame must sit to count as speech. Relative,
# so the background level (silent room, or a fan) becomes the baseline rather than a pass.
VOICE_SPEECH_FLOOR_MARGIN_DB = float(os.getenv("VOICE_SPEECH_FLOOR_MARGIN_DB", "6.0"))

# Minimum spread between the quiet and loud parts of the recording (p95 - p10 of frame dB).
# This is the signal that actually separates speech from steady noise.
VOICE_MIN_DYNAMIC_RANGE_DB = float(os.getenv("VOICE_MIN_DYNAMIC_RANGE_DB", "6.0"))

# Absolute dead-mic / digital-silence guard ONLY, applied to the loudest frame. Deliberately
# very low (~-50 dBFS): discrimination is the dynamic-range test's job, and a high value here
# is exactly what would reject the quiet legitimate speaker. NOTE: this value was a per-frame
# speech threshold in the first version of this gate; it is a peak-level sanity check now, so
# a previously tuned value in .env means something different and should be re-checked.
VOICE_SPEECH_ENERGY_THRESHOLD = float(os.getenv("VOICE_SPEECH_ENERGY_THRESHOLD", "0.003"))

# Liveness (Milestone 2)
LIVENESS_MIN_FRAMES = 5
LIVENESS_MIN_YAW_RANGE_DEG = 18.0   # a static photo shows ~0 range
LIVENESS_MAX_IDENTITY_DRIFT = 0.25  # min cosine between frame embeddings to accept same subject
VOICE_CONTENT_MIN_SIMILARITY = 0.70 # transcript vs challenge phrase

_CALIB_PATH = os.getenv("CALIBRATION_PATH", "models/calibration.json")


def load_calibration():
    try:
        with open(_CALIB_PATH, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return None
