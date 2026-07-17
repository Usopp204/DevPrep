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
