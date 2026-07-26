import numpy as np

from .config import LIVENESS_MIN_FRAMES, LIVENESS_MIN_YAW_RANGE_DEG, LIVENESS_MAX_IDENTITY_DRIFT
from .face_engine import face_frame_features, FaceQualityError
from .similarity import cosine_similarity


def verify_face_live(frames: list[bytes], stored_embedding, challenge: dict) -> dict:
    """frames: sampled burst/video frames. challenge e.g. {"type":"turn_head","direction":"right"}.

    NOTE: the returned `live` (and yaw_range/moved_enough/direction_ok) are diagnostics only —
    router.py's /face/verify-live no longer factors `live` into pass/fail. See the comment there
    for why (yaw measurement is unreliable on real hardware). Kept computing them so Detail JSON
    still has data to investigate with if someone revisits this."""
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

    # Liveness motion: a real head turn should produce yaw range; a still photo ~flat.
    # In practice the signal itself is broken (real-rig data, 2026-07-24: YawRange of just
    # 0.55-1.61° during a clear, deliberate head turn — not a threshold problem, the measurement
    # itself isn't moving). Kept for diagnostics; not used to gate entry (see router.py).
    yaw_range = float(np.ptp(yaws))
    moved_enough = yaw_range >= LIVENESS_MIN_YAW_RANGE_DEG

    # Direction check (turned left vs right) is computed and recorded for diagnostics only —
    # NOT used to decide `live`. Empirically (real rig data, 2026-07-24) it disagreed with the
    # requested direction ~80% of the time on BOTH "left" and "right" challenges, so the sign
    # convention isn't simply flipped (that would just move the ~80% failure rate to the other
    # side) — the bug is deeper (e.g. face.pose[1] may not be a reliable yaw signal, or
    # np.argmax(|yaw|) can pick the frame where the user is swinging back through center rather
    # than the sustained turn). TODO: revisit with real-rig experiments before re-enabling.
    direction_ok = True
    if challenge.get("type") == "turn_head":
        swing = float(yaws[np.argmax(np.abs(yaws))])
        if challenge.get("direction") == "right":
            direction_ok = swing > 0
        elif challenge.get("direction") == "left":
            direction_ok = swing < 0

    identity = float(np.mean([cosine_similarity(e, stored_embedding) for e in embs]))
    live = bool(moved_enough)
    return {"live": live, "identity_score": round(identity, 4),
            "yaw_range": round(yaw_range, 2), "moved_enough": moved_enough,
            "direction_ok": direction_ok,
            "reason": None if live else "No natural head motion detected"}
