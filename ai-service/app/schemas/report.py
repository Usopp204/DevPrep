"""
report.py — Pydantic schemas for the evaluation report.
Location: ai-service/app/schemas/report.py
Maps directly to interviewreport.html.
"""
from __future__ import annotations

from pydantic import BaseModel, Field



class QuestionReport(BaseModel):
    qid: str
    question: str
    user_answer: str
    category: str = ""
    difficulty: str = ""
    score: int
    criteria: dict[str, float] = Field(default_factory=dict)
    analysis: str
    suggested_improvement: str
    praise: str = ""


class CategoryScore(BaseModel):
    name: str
    score: int
    comment: str = ""


class Summary(BaseModel):
    strengths: list[str] = Field(default_factory=list)
    areas_to_improve: list[str] = Field(default_factory=list)
    praise: str = ""
    advice: str = ""


class EvaluationReport(BaseModel):
    overall_score: int
    duration: str = ""
    num_questions: int
    categories: list[CategoryScore]
    questions: list[QuestionReport]
    summary: Summary