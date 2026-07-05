"""
main.py — FastAPI application entrypoint.
Location: ai-service/app/main.py
Run: uvicorn app.main:app --host 0.0.0.0 --port 8000
"""
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api import catalog, evaluation, interview, internal
from app.core import config

app = FastAPI(title="DevPrep AI Service")

app.add_middleware(
    CORSMiddleware,
    allow_origins=config.ALLOWED_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(interview.router)
app.include_router(evaluation.router)
app.include_router(catalog.router)
app.include_router(internal.router)


@app.get("/health")
def health():
    return {"status": "ok"}


# Serve the test UI (test-ui/*.html) same-origin at /ui, e.g. /ui/interviewdashboard.html
import os

_ui_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "test-ui")
if os.path.isdir(_ui_dir):
    from fastapi.staticfiles import StaticFiles

    app.mount("/ui", StaticFiles(directory=_ui_dir, html=True), name="ui")