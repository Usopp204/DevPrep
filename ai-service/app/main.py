"""
main.py — FastAPI application entrypoint.
Location: ai-service/app/main.py
Run: uvicorn app.main:app --host 0.0.0.0 --port 8000
"""
import asyncio
import os
from concurrent.futures import ThreadPoolExecutor
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from app.api import catalog, evaluation, interview, internal
from app.core import config
from biometrics.router import router as biometrics_router
from biometrics.router import warm_up as biometrics_warm_up
from app.core.config import settings
from app.core.model_manager import get_model_and_tokenizer
from app.cv.router import router as cv_router
from app.jd.router import router as jd_router
from app.match.router import router as match_router
from app.services.voice import warm_up_tts

_executor = ThreadPoolExecutor(max_workers=1)


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Start loading model in background immediately on server startup
    loop = asyncio.get_event_loop()
    loop.run_in_executor(_executor, get_model_and_tokenizer)
    # Warm up biometric models on the default executor (separate from the LLM one,
    # which is single-worker and would serialize this behind the LLM load).
    loop.run_in_executor(None, biometrics_warm_up)
    # Same reasoning for TTS: load the Piper voice model off the LLM executor so it
    # doesn't queue behind the (slower) LLM load.
    loop.run_in_executor(None, warm_up_tts)
    yield


app = FastAPI(title=settings.app_name, lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=config.ALLOWED_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(cv_router)
app.include_router(jd_router)
app.include_router(match_router)
app.include_router(interview.router)
app.include_router(evaluation.router)
app.include_router(catalog.router)
app.include_router(internal.router)
app.include_router(biometrics_router)


@app.get("/health")
def health():
    return {"status": "ok"}

_ui_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "test-ui")
if os.path.isdir(_ui_dir):
    from fastapi.staticfiles import StaticFiles

    app.mount("/ui", StaticFiles(directory=_ui_dir, html=True), name="ui")
