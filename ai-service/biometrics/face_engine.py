import io
import threading

import cv2
import numpy as np
from PIL import Image

from .config import (
    FACE_MODEL_PACK, FACE_MODEL_ROOT,
    FACE_MIN_DET_SCORE, FACE_MIN_FACE_RATIO, FACE_MIN_BLUR_VAR,
)

_lock = threading.Lock()
_analyzer = None


def _get_analyzer():
    global _analyzer
    if _analyzer is None:
        with _lock:
            if _analyzer is None:
                from insightface.app import FaceAnalysis
                a = FaceAnalysis(name=FACE_MODEL_PACK, root=FACE_MODEL_ROOT,
                                 providers=["CPUExecutionProvider"])
                a.prepare(ctx_id=-1, det_size=(640, 640))
                _analyzer = a
    return _analyzer


class FaceQualityError(ValueError):
    pass


def _blur_variance(bgr):
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    return float(cv2.Laplacian(gray, cv2.CV_64F).var())


def _decode(image_bytes):
    try:
        pil = Image.open(io.BytesIO(image_bytes)).convert("RGB")
    except Exception as exc:
        raise FaceQualityError("Cannot decode image") from exc
    return cv2.cvtColor(np.array(pil), cv2.COLOR_RGB2BGR)


def _primary_face(bgr, enforce_single=True):
    faces = _get_analyzer().get(bgr)
    if not faces:
        raise FaceQualityError("No face detected")
    if enforce_single and len(faces) > 1:
        raise FaceQualityError("Multiple faces detected")

    def area(f):
        x1, y1, x2, y2 = f.bbox
        return max(0.0, x2 - x1) * max(0.0, y2 - y1)

    return max(faces, key=area)


def extract_face_embedding(image_bytes: bytes) -> dict:
    bgr = _decode(image_bytes)
    h, w = bgr.shape[:2]
    face = _primary_face(bgr, enforce_single=True)

    det = float(face.det_score)
    x1, y1, x2, y2 = face.bbox
    ratio = (max(0.0, x2 - x1) * max(0.0, y2 - y1)) / float(w * h)
    blur = _blur_variance(bgr)
    if det < FACE_MIN_DET_SCORE:
        raise FaceQualityError(f"Low detection confidence ({det:.2f})")
    if ratio < FACE_MIN_FACE_RATIO:
        raise FaceQualityError("Face too small / too far from camera")
    if blur < FACE_MIN_BLUR_VAR:
        raise FaceQualityError("Image too blurry")

    emb = np.asarray(face.normed_embedding, dtype=np.float32)  # 512-d, L2-normalized
    quality = float(min(1.0, det) * min(1.0, blur / 150.0))
    return {"embedding": emb.tolist(), "dim": int(emb.shape[0]),
            "quality": round(quality, 4), "det_score": round(det, 4)}


def face_frame_features(image_bytes: bytes):
    """For liveness: return (embedding, yaw) from a single frame, or None if no usable face."""
    try:
        bgr = _decode(image_bytes)
        face = _primary_face(bgr, enforce_single=False)
    except FaceQualityError:
        return None
    yaw = float(face.pose[1]) if getattr(face, "pose", None) is not None else 0.0
    emb = np.asarray(face.normed_embedding, dtype=np.float32)
    return emb, yaw
