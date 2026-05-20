# Constants & Environment Guidelines

## Goals

- Eliminate hardcoded values across all Microservices.
- Centralize configuration via environment variables.
- Ensure strict configuration isolation for each independent service.

## Rules

1. **Isolated Configurations:** Each microservice MUST have its own independent configuration section. An environment variable for `PaymentService` must not bleed into `AuthService`.
2. **No hardcoded values** (ports, URLs, feature flags, routing paths, thresholds).
3. **Secrets must live in `.env` or environment variables**.
4. **Config binding** should happen at startup only.
5. **Root .env + Prefix Policy (Locked):** Use a single `.env` at repo root with explicit prefixes: `GLOBAL_`, `AUTH_`, `DOC_`, `INTERVIEW_`, `EVALUATION_`, `PAYMENT_`, `ADMIN_`. Each service may read only `GLOBAL_` and its own prefix.

## Backend (.NET Microservices)

- **No Global Shared Libraries:** Do not create a global `DevPrep.Shared` project or `BuildingBlocks` folder.
- **Constants:** Must be defined locally within each specific microservice folder (e.g., `backend/DevPrep.AuthService/Constants/`, `backend/DevPrep.InterviewService/Constants/`).
- **Config objects:** Create configuration models locally inside each service, e.g., `backend/DevPrep.EvaluationService/Configuration/`.
- Environment bindings via `IOptions<T>` pattern during service registration.

## AI Service (Python)

- Constants: `ai-service/app/core/constants.py`
- Env settings: `ai-service/app/core/config.py` (using Pydantic BaseSettings)

## Frontend (React)

- Constants: `frontend/src/constants/`
- Env wrapper: `frontend/src/config/env.ts`

## Cross-Service Communication Notes

- RabbitMQ queue names must be defined in the constants of the specific services that publish or consume them.
- External Service URLs (used for HTTPClient sync calls) must be injected via `.env`, allowing the API Gateway and downstream services to communicate dynamically regardless of environment.

## Performance Note

- Avoid synchronous/blocking calls in request handlers.
- Use async IO for HTTP, DB, and AI service calls.
