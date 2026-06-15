# AI Service (FastAPI)

## Purpose

Python FastAPI service for future NLP/RAG/question/evaluation support. The AI service is internal-only and is not a public frontend-facing API surface.

Current implementation is scaffold-level and exposes only `/health`.

## Local Run

1. Ensure `.env` exists at the repository root if runtime configuration is needed.
2. Install dependencies:

```powershell
python -m pip install -r ai-serviceequirements.txt
```

3. Run:

```powershell
cd ai-service
python -m uvicorn app.main:app --reload
```

4. Health check:

```text
http://localhost:8000/health
```

## Rules

- Keep `/health` available.
- Do not hard-code provider keys or secrets.
- Do not add external model/API dependencies without approval.
- Backend services may call AI internally in later business implementation phases.
