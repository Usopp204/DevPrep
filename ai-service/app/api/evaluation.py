"""
evaluation.py — Post-session evaluation API.
Location: ai-service/app/api/evaluation.py
"""
from __future__ import annotations

from fastapi import APIRouter

from app.schemas.interview import Transcript
from app.schemas.report import EvaluationReport
from app.services.evaluator import evaluate

router = APIRouter(tags=["evaluation"])


@router.post("/evaluate", response_model=EvaluationReport)
def run_evaluation(transcript: Transcript):
    return evaluate(transcript.model_dump())