import asyncio
import os
import shutil
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from fastapi import APIRouter, File, HTTPException, UploadFile

from app.core.model_manager import get_model_and_tokenizer
from app.cv.parser import parse_cv, read_cv

router = APIRouter(prefix="/parse", tags=["cv"])
_executor = ThreadPoolExecutor(max_workers=1)


def _run_parse(tmp: str, ext: str) -> dict:
    text = read_cv(tmp)
    if not text:
        return {"error": "Could not extract text from file"}
    model, tokenizer = get_model_and_tokenizer()
    return parse_cv(text, model, tokenizer)


@router.post("/cv")
async def parse_cv_endpoint(file: UploadFile = File(...)):
    ext = Path(file.filename or "").suffix.lower()
    if ext not in (".pdf", ".doc", ".docx"):
        raise HTTPException(status_code=400, detail="Only PDF/DOC/DOCX supported")

    tmp = tempfile.mktemp(suffix=ext)
    try:
        with open(tmp, "wb") as f:
            shutil.copyfileobj(file.file, f)

        loop = asyncio.get_event_loop()
        result = await loop.run_in_executor(_executor, _run_parse, tmp, ext)
        return result
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)
