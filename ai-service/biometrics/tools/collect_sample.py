"""
collect_sample.py — Helper for building the M3 calibration CSV by hand.

Usage:
    Enroll/verify samples normally (embeddings live in the DB already for your own
    face+voice). For each NEW sample you want to score (another genuine try, an
    impostor face/voice from a friend, or a spoof attempt), call the ai-service
    embed endpoint yourself, then run this script to compute the score against
    your OWN enrolled embedding and append a labeled row to scores.csv.

Steps:
    1. Get your own enrolled embedding once (see get_my_embedding.py or query the DB
       directly — see instructions below), save as my_face.json / my_voice.json.
    2. For every new sample (yours again, a friend's, or a spoof), call:

        curl -X POST http://localhost:8080/internal/biometrics/face/embed \
             -H "X-Internal-Api-Key: <your key>" \
             -F "file=@sample.jpg" > sample_response.json

       (swap face/embed for voice/embed with a .wav/.webm file for voice)

    3. Run:
        python collect_sample.py --modality face --label 1 \
            --reference my_face.json --sample sample_response.json --out scores.csv

       label = 1 for genuine (same person as reference), 0 for impostor/spoof.

The script appends one row "modality,label,score" to --out (creates the file with a
header if it doesn't exist yet). Re-run it once per sample you collect — by the end
you'll have a scores.csv ready for calibrate.py.
"""
import argparse
import csv
import json
import os

import numpy as np


def cosine_similarity(a, b) -> float:
    va = np.asarray(a, dtype=np.float32)
    vb = np.asarray(b, dtype=np.float32)
    denom = (np.linalg.norm(va) * np.linalg.norm(vb)) + 1e-8
    return float(np.dot(va, vb) / denom)


def load_embedding(path: str):
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    if "embedding" not in data:
        raise ValueError(f"{path} does not look like an ai-service embed response (no 'embedding' key)")
    return data["embedding"]


def main():
    ap = argparse.ArgumentParser(description="Compute a similarity score and append it to the calibration CSV.")
    ap.add_argument("--modality", required=True, choices=["face", "voice"])
    ap.add_argument("--label", required=True, type=int, choices=[0, 1],
                     help="1 = genuine (same person as reference), 0 = impostor or spoof")
    ap.add_argument("--reference", required=True, help="Path to YOUR saved embedding JSON (the enrolled one)")
    ap.add_argument("--sample", required=True, help="Path to the new sample's embed response JSON")
    ap.add_argument("--out", default="scores.csv", help="CSV file to append to (default: scores.csv)")
    ap.add_argument("--note", default="", help="Optional free-text note (e.g. 'friend A', 'printed photo')")
    args = ap.parse_args()

    ref_emb = load_embedding(args.reference)
    sample_emb = load_embedding(args.sample)
    score = cosine_similarity(ref_emb, sample_emb)

    is_new = not os.path.exists(args.out)
    with open(args.out, "a", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        if is_new:
            writer.writerow(["modality", "label", "score"])
        writer.writerow([args.modality, args.label, round(score, 6)])

    kind = "GENUINE" if args.label == 1 else "IMPOSTOR/SPOOF"
    print(f"[{kind}] {args.modality} score = {score:.4f}  (note: {args.note or '-'})")
    print(f"Appended to {args.out}")


if __name__ == "__main__":
    main()
