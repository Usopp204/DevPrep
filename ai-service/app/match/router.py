from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from app.match.scorer import calculate_score

router = APIRouter(prefix="/match", tags=["match"])


class MatchRequest(BaseModel):
    cv_json: dict
    jd_json: dict


@router.post("")
async def match_endpoint(body: MatchRequest):
    if not body.cv_json or not body.jd_json:
        raise HTTPException(status_code=400, detail="Both cv_json and jd_json are required")
    result = calculate_score(body.cv_json, body.jd_json)
    return result
