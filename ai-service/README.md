# AI Service (FastAPI)

## Purpose
Python service for NLP/RAG and interview evaluation.

## Local Run (No Docker)
1. Ensure `.env` exists at repo root (copy from `.env.example`).
2. Install deps:
   ```
   python -m pip install -r ai-service/requirements.txt
   ```
3. Run:
   ```
   python -m uvicorn app.main:app --reload
   ```
4. Health check:
   ```
   http://localhost:8000/health
   ```

## Structure
```
ai-service/
├─ app/
└─ requirements.txt
```
