"""
main.py — FastAPI application entrypoint.
Location: ai-service/app/main.py
Run: uvicorn app.main:app --host 0.0.0.0 --port 8000
"""
import asyncio
import logging
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


def _configure_logging() -> None:
    """Nothing in this service configured logging before this function existed. Python's root
    logger defaults to WARNING with no handler when nobody calls basicConfig/dictConfig, so
    every logging.getLogger(__name__).info(...) call anywhere in the codebase — biometrics.router's
    "Biometrics models warmed up.", app.services.voice's "TTS provider ... warmed up.", and any
    other info line already written for debugging — was silently discarded from the day this
    project started. Only WARNING+ calls were ever visible, and only by accident: Python's
    logging.lastResort handler prints unhandled records to stderr, but only at WARNING or above.
    The "INFO:" lines already in `docker logs` are uvicorn's own access/error loggers, which
    uvicorn configures for itself at startup — that configuration never touched this app's loggers.
    If someone added an info() line to debug something and concluded their code wasn't running,
    this is why: the line ran, it just had nowhere to go.

    Scoped to the "app" and "biometrics" namespaces ONLY (every logger in this codebase is
    logging.getLogger(__name__) inside one of those two packages) — deliberately NOT root, so
    third-party libraries (torch, speechbrain, transformers, httpx, ...) stay at their own
    defaults instead of flooding the log at INFO and burying our own lines in noise, which would
    defeat the point of turning this on.

    Level is LOG_LEVEL (env var, default INFO) rather than a fixed value so it can be dropped to
    WARNING without a rebuild if library-adjacent noise (there isn't any today, since only our
    two namespaces are scoped in — but a future logger.info added in either package would count)
    turns out to be a problem in practice.
    """
    level = getattr(logging, config.LOG_LEVEL.upper(), logging.INFO)
    handler = logging.StreamHandler()
    handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s %(name)s: %(message)s"))
    for namespace in ("app", "biometrics"):
        ns_logger = logging.getLogger(namespace)
        ns_logger.setLevel(level)
        # Idempotent: a reload/reimport of this module (uvicorn --reload, or a test importing
        # app.main twice) must not stack a second StreamHandler and double-print every line.
        if not ns_logger.handlers:
            ns_logger.addHandler(handler)
        # Don't also hand records up to root — avoids double-printing if root ever gets
        # configured later for an unrelated reason.
        ns_logger.propagate = False


_configure_logging()

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
