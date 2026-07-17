"""
schemas.py — Pydantic response models for the /internal/biometrics endpoints.
Location: ai-service/biometrics/schemas.py
"""
from pydantic import BaseModel


class FaceEmbedResponse(BaseModel):
    embedding: list[float]
    dim: int
    quality: float
    det_score: float
    model_version: str


class VoiceEmbedResponse(BaseModel):
    embedding: list[float]
    dim: int
    duration_sec: float
    quality: float
    model_version: str


class VerifyResponse(BaseModel):
    match: bool
    score: float
    threshold: float


class FaceVerifyLiveResponse(BaseModel):
    passed: bool                       # live AND identity_score >= threshold
    live: bool
    identity_score: float | None
    threshold: float
    yaw_range: float
    moved_enough: bool | None = None   # absent when rejected early on identity drift
    direction_ok: bool | None = None
    reason: str | None = None


class VoiceVerifyChallengeResponse(BaseModel):
    passed: bool                       # speaker_score >= threshold AND content_ok
    speaker_score: float
    content_score: float
    content_ok: bool
    threshold: float
    transcript: str
