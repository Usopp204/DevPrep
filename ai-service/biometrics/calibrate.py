"""Calibrate verification thresholds and fit multimodal score fusion.

Per-modality CSV (--scores): columns  modality,label,score   (label 1=genuine, 0=impostor).
Optional fusion CSV (--fusion): columns  label,face_score,voice_score.
Writes models/calibration.json consumed by the runtime.
"""
import argparse
import json

import numpy as np


def _rates(labels, scores, t):
    pred = scores >= t
    tp = int(np.sum(pred & (labels == 1))); fn = int(np.sum(~pred & (labels == 1)))
    fp = int(np.sum(pred & (labels == 0))); tn = int(np.sum(~pred & (labels == 0)))
    far = fp / max(1, fp + tn); frr = fn / max(1, fn + tp)
    return far, frr


def eer(labels, scores):
    ts = np.unique(scores); best = None
    for t in ts:
        far, frr = _rates(labels, scores, t)
        gap = abs(far - frr)
        if best is None or gap < best[0]:
            best = (gap, float(t), (far + frr) / 2.0)
    return {"eer_threshold": best[1], "eer": round(best[2], 4)}


def threshold_at_far(labels, scores, target_far):
    for t in sorted(np.unique(scores), reverse=True):
        far, frr = _rates(labels, scores, t)
        if far <= target_far:
            return {"threshold": float(t), "far": round(far, 4), "frr": round(frr, 4)}
    return {"threshold": float(np.max(scores)), "far": None, "frr": None}


def fit_fusion(labels, face_scores, voice_scores):
    from sklearn.linear_model import LogisticRegression
    X = np.column_stack([face_scores, voice_scores]); y = np.asarray(labels)
    clf = LogisticRegression().fit(X, y)
    return {"bias": float(clf.intercept_[0]),
            "w_face": float(clf.coef_[0][0]),
            "w_voice": float(clf.coef_[0][1]),
            "decision_threshold": 0.5}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scores", required=True)
    ap.add_argument("--fusion")
    ap.add_argument("--target-far", type=float, default=0.01)  # 1% FAR operating point
    ap.add_argument("--out", default="models/calibration.json")
    args = ap.parse_args()

    import csv
    by_mod = {"face": ([], []), "voice": ([], [])}
    with open(args.scores, newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            m = row["modality"].strip().lower()
            if m in by_mod:
                by_mod[m][0].append(int(row["label"])); by_mod[m][1].append(float(row["score"]))

    out = {}
    for m, (lab, sc) in by_mod.items():
        if lab:
            labels, scores = np.array(lab), np.array(sc, dtype=np.float32)
            out[m] = {**threshold_at_far(labels, scores, args.target_far), **eer(labels, scores)}

    if args.fusion:
        lab, fs, vs = [], [], []
        with open(args.fusion, newline="", encoding="utf-8") as f:
            for row in csv.DictReader(f):
                lab.append(int(row["label"])); fs.append(float(row["face_score"])); vs.append(float(row["voice_score"]))
        out["fusion"] = fit_fusion(np.array(lab), np.array(fs), np.array(vs))

    import os
    os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)
    print(json.dumps(out, indent=2))


if __name__ == "__main__":
    main()
