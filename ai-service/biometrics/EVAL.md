# Biometric verification — calibration data collection protocol

This documents how to collect **real** data for `calibrate.py` (Milestone 3). No thresholds in
`config.py` (`FACE_MATCH_THRESHOLD`, `VOICE_MATCH_THRESHOLD`) should be changed based on anything
other than this real, human-collected data — see "Do not" at the bottom.

## 1. Subjects

- Collect from **≥ 6 subjects** (team members + volunteers), each with informed consent that their
  face/voice samples are used only for this calibration exercise and are deleted afterward (only the
  resulting embeddings/scores are needed, not the raw media — see Privacy below).
- Per subject, capture:
  - **One enrollment sample** (what `POST /internal/biometrics/{face,voice}/embed` would store).
  - **Several genuine probes**, spread across different sessions, lighting conditions, and — if
    possible — different devices/cameras/microphones. Aim for ≥ 5 genuine probes per subject per
    modality so the genuine-score distribution isn't dominated by a single lucky/unlucky capture.

## 2. Pairs to score

- **Genuine pairs** (label = 1): each subject's enrollment embedding vs each of their own genuine
  probes.
- **Impostor pairs** (label = 0): every subject's probe vs every *other* subject's enrollment. With
  6 subjects × 5 probes each, that's 6×5×5 = 150 impostor pairs — plenty for a first EER estimate.
- **Spoof samples** (also label = 0, scored separately if you want a spoof-specific breakdown):
  - Face: a printed photo, a phone-screen photo, and a video replay of an enrolled subject, held up
    to the camera.
  - Voice: a played-back recording of an enrolled subject's phrase, and an ElevenLabs-TTS reading of
    a challenge phrase (tests whether a synthetic voice is caught).

## 3. Producing the scores CSV

Write a small batch runner (not checked in yet — build it when you start this) that:
1. For each subject, POSTs the enrollment sample to `/internal/biometrics/{face,voice}/embed` and
   keeps the returned `embedding`.
2. For each probe/impostor/spoof sample, POSTs it to `/internal/biometrics/verify` with the relevant
   `stored_embedding`, and records the returned `score`.
3. Appends one row per comparison to a CSV with columns: `modality,label,score`
   (`modality` = `face` or `voice`, `label` = `1` genuine / `0` impostor-or-spoof, `score` = the
   cosine similarity `/verify` returned).

If you also want fusion (Milestone 3's `--fusion` option), additionally produce a CSV with columns
`label,face_score,voice_score` for pairs where you have *both* a face and a voice score for the same
genuine/impostor comparison.

## 4. Running calibration

```bash
python biometrics/calibrate.py --scores scores.csv --target-far 0.01 --out models/calibration.json
# optionally:
python biometrics/calibrate.py --scores scores.csv --fusion fusion.csv --out models/calibration.json
```

This writes `models/calibration.json` (gitignored, loaded by `config.load_calibration()` at
runtime — `/verify` and the Milestone-2 verify endpoints already prefer these thresholds over the
fixed defaults in `config.py` once the file is present).

## 5. What to report in the capstone report

- EER (equal error rate) per modality.
- FAR/FRR at the chosen operating point (default target FAR = 1%).
- The DET curve (FAR vs FRR across thresholds) — plot from the same CSV, `calibrate.py` itself
  doesn't produce a chart.
- Fusion weights, if a fusion CSV was supplied.

## Privacy (Nghị định 13/2023/NĐ-CP)

Keep only what's needed to reproduce the calibration numbers — the scores CSV (floats), not the raw
photos/audio. If raw samples must be kept temporarily to re-run the batch collector, store them
separately from the main repo, encrypted, with a short retention window, and delete them once the
scores CSV is produced.

## Do not

- Do not fabricate genuine/impostor scores to make `calibrate.py` "work" — a smoke test with a
  handful of made-up numbers only proves the *script's mechanics* run end-to-end (CSV in →
  `calibration.json` out); it says nothing about real accuracy and must never be presented as such
  or used to change the fixed defaults in `config.py`.
- Do not fine-tune or fine-tune a passive anti-spoof head on the self-collected spoof set unless
  explicitly asked — that's an optional stretch goal, not part of the core calibration pass.
