import os
import shutil
import tempfile
from pathlib import Path

from fastapi import APIRouter, File, Form, HTTPException, UploadFile
from pydantic import BaseModel

from app.core.model_manager import get_model_and_tokenizer
from app.cv.parser import read_cv
from app.jd.parser import parse_jd

router = APIRouter(prefix="/parse", tags=["jd"])


class JdRequest(BaseModel):
    text: str


@router.post("/jd")
async def parse_jd_endpoint(body: JdRequest):
    if not body.text or not body.text.strip():
        raise HTTPException(status_code=400, detail="JD text cannot be empty")
    model, tokenizer = get_model_and_tokenizer()
    result = parse_jd(body.text, model, tokenizer)
    return result


@router.post("/jd-file")
async def parse_jd_file_endpoint(
    file: UploadFile = File(...),
    position: str = Form(default=""),
):
    ext = Path(file.filename or "").suffix.lower()
    if ext not in (".pdf", ".doc", ".docx"):
        raise HTTPException(status_code=400, detail="Only PDF/DOC/DOCX supported")

    tmp = tempfile.mktemp(suffix=ext)
    try:
        with open(tmp, "wb") as f:
            shutil.copyfileobj(file.file, f)

        text = read_cv(tmp)
        if not text:
            raise HTTPException(status_code=422, detail="Could not extract text from file")

        # Prepend position hint if provided
        full_text = f"Position: {position}\n\n{text}" if position else text

        model, tokenizer = get_model_and_tokenizer()
        result = parse_jd(full_text, model, tokenizer)
        return result
    finally:
        if os.path.exists(tmp):
            os.remove(tmp)
