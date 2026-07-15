import numpy as np

from .config import LIVENESS_MIN_FRAMES, LIVENESS_MIN_YAW_RANGE_DEG, LIVENESS_MAX_IDENTITY_DRIFT
from .face_engine import face_frame_features, FaceQualityError
from .similarity import cosine_similarity


def verify_face_live(frames: list[bytes], stored_embedding, challenge: dict) -> dict:
    """frames: sampled burst/video frames. challenge e.g. {"type":"turn_head","direction":"right"}.
    Rejects static photos (no head motion) and enforces same-subject identity across frames."""
    feats = [f for f in (face_frame_features(b) for b in frames) if f is not None]
    if len(feats) < LIVENESS_MIN_FRAMES:
        raise FaceQualityError("Not enough usable frames with a clear face")

    embs = [e for e, _ in feats]
    yaws = np.array([y for _, y in feats], dtype=np.float32)

    # Identity consistency: reject frame-to-frame drift (someone swapping in front of camera).
    ref = embs[0]
    drift = min(cosine_similarity(ref, e) for e in embs[1:])
    if drift < LIVENESS_MAX_IDENTITY_DRIFT:
        return {"live": False, "reason": "Inconsistent identity across frames",
                "identity_score": None, "yaw_range": float(np.ptp(yaws))}

    # Liveness motion: a real head turn produces yaw range; a still photo is ~flat.
    yaw_range = float(np.ptp(yaws))
    moved_enough = yaw_range >= LIVENESS_MIN_YAW_RANGE_DEG
    # Direction check (sign convention may need flipping after one empirical test on the rig).
    direction_ok = True
    if challenge.get("type") == "turn_head":
        swing = float(yaws[np.argmax(np.abs(yaws))])
        if challenge.get("direction") == "right":
            direction_ok = swing > 0
        elif challenge.get("direction") == "left":
            direction_ok = swing < 0

    identity = float(np.mean([cosine_similarity(e, stored_embedding) for e in embs]))
    live = bool(moved_enough and direction_ok)
    return {"live": live, "identity_score": round(identity, 4),
            "yaw_range": round(yaw_range, 2), "moved_enough": moved_enough,
            "direction_ok": direction_ok,
            "reason": None if live else "No natural head motion / wrong direction"}
