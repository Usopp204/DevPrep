import asyncio
from concurrent.futures import ThreadPoolExecutor
from contextlib import asynccontextmanager

from dotenv import load_dotenv
from fastapi import FastAPI

from app.core.config import ROOT_ENV_PATH, settings

load_dotenv(ROOT_ENV_PATH)
from app.core.model_manager import get_model_and_tokenizer
from app.cv.router import router as cv_router
from app.jd.router import router as jd_router
from app.match.router import router as match_router

_executor = ThreadPoolExecutor(max_workers=1)


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Start loading model in background immediately on server startup
    loop = asyncio.get_event_loop()
    loop.run_in_executor(_executor, get_model_and_tokenizer)
    yield


app = FastAPI(title=settings.app_name, lifespan=lifespan)

app.include_router(cv_router)
app.include_router(jd_router)
app.include_router(match_router)


@app.get("/health")
def health_check():
    return {"status": "ok"}
