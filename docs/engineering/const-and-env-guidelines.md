# Constants & Environment Guidelines

## Goals

- Keep runtime values out of source code.
- Use `.env.example` as the shared template and `.env` as local-only configuration.
- Keep configuration ownership clear per service.

## Environment Prefixes

Each backend service reads `GLOBAL_` values plus its own prefix:

| Service | Prefix |
| --- | --- |
| ApiGateway | `GLOBAL_` |
| AuthService | `AUTH_` |
| UserProfileService | `USER_` |
| DocumentService | `DOC_` |
| QuestionBankService | `QUESTION_` |
| InterviewService | `INTERVIEW_` |
| EvaluationService | `EVALUATION_` |
| FeedbackService | `FEEDBACK_` |
| PaymentService | `PAYMENT_` |
| NewsService | `NEWS_` |
| NotificationService | `NOTIFICATION_` |
| StatisticsService | `STATISTICS_` |

Each business service must read its own `*_API_PORT` and `*_DB_CONNECTION_STRING`.

## Backend Rules

- Do not hard-code ports, connection strings, secrets, provider keys, scoring thresholds, or service URLs.
- Do not create `DevPrep.Shared`, `BuildingBlocks`, `Common`, `SharedKernel`, or a shared backend class library unless explicitly approved.
- Keep constants and configuration models service-local.
- Do not query another service's database directly.
- Prefer async I/O for HTTP, DB, and AI calls.

## Frontend Rules

- Keep API client/config logic centralized.
- Use one gateway base URL value.
- Do not call downstream services or the internal AI service directly from the browser.

## AI Service Rules

- Use environment variables for runtime configuration.
- Keep `/health` available.
- Do not hard-code model/provider secrets.
