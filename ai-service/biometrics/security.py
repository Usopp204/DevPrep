"""
security.py — Internal-key FastAPI dependency for the biometrics router.
Location: ai-service/biometrics/security.py

Same semantics as _check_key in app/api/internal.py: if a key is configured,
require it; an empty key means open access (local dev only).
"""
from fastapi import Header, HTTPException

from .config import INTERNAL_API_KEY


def require_internal_api_key(x_internal_api_key: str | None = Header(default=None)) -> None:
    if INTERNAL_API_KEY and x_internal_api_key != INTERNAL_API_KEY:
        raise HTTPException(status_code=401, detail="Invalid internal API key.")
