"""
interview.py — Pydantic schemas for the interview API.
Location: ai-service/app/schemas/interview.py
"""
from __future__ import annotations

from typing import Optional

from pydantic import BaseModel, Field


class SessionConfigIn(BaseModel):
    position: str                       # CSV Position track value, e.g. "Backend"
    level: str                          # intern/fresher/junior/middle/senior/techlead
    interview_type: str = "technical"
    focus_areas: list[str] = Field(default_factory=list)
    duration_min: int = 45
    jd: str = ""


class StartResponse(BaseModel):
    session_id: str
    message: str
    question: str = ""
    ended: bool = False
    question_number: int = 0
    total_questions: int = 0
    scenario: list = Field(default_factory=list)   # [{qid, question, category, difficulty}, ...]


class ReportIn(BaseModel):
    duration_actual_sec: int = 0


class AnswerIn(BaseModel):
    answer: str


class TurnResponse(BaseModel):
    message: str
    ended: bool
    question_number: int = 0
    total_questions: int = 0


class VoiceAnswerResponse(BaseModel):
    user_text: str                      # what STT heard
    message: str                        # interviewer reply
    ended: bool
    question_number: int = 0
    total_questions: int = 0


class TTSIn(BaseModel):
    text: str


# --- Transcript models (input to /evaluate) ---
class Followup(BaseModel):
    q: str
    a: Optional[str] = None


class TranscriptItem(BaseModel):
    qid: str
    question: str
    reference_answer: str = ""
    category: str = ""
    difficulty: str = ""
    user_answer: Optional[str] = None
    followups: list[Followup] = Field(default_factory=list)
    skipped: bool = False


class Transcript(BaseModel):
    config: dict = Field(default_factory=dict)
    duration_actual_sec: int = 0
    items: list[TranscriptItem]