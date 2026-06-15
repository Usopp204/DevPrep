# DevPrep Architecture Baseline

DevPrep is an AI-powered mock interview simulator and IT competency evaluation system built as a Pure Microservices monorepo.

## Current Architecture

- Backend: Pure Microservices using ASP.NET Core and YARP.
- Frontend: React + Vite.
- AI service: FastAPI, internal-only.
- Contracts: JSON/schema source-of-truth tracked in `contracts/`.
- Configuration: `.env.example` is the shared template; `.env` is local-only and must not be committed.

## Backend Solution

`backend/DevPrep.sln` contains exactly 12 API projects:

1. `DevPrep.ApiGateway`
2. `DevPrep.AuthService`
3. `DevPrep.UserProfileService`
4. `DevPrep.DocumentService`
5. `DevPrep.QuestionBankService`
6. `DevPrep.InterviewService`
7. `DevPrep.EvaluationService`
8. `DevPrep.FeedbackService`
9. `DevPrep.PaymentService`
10. `DevPrep.NewsService`
11. `DevPrep.NotificationService`
12. `DevPrep.StatisticsService`

Admin and Manager are implemented as roles and route namespaces. Manager access is read-only through `/api/admin/manager/*` routes where needed.

## Gateway Routes

The gateway owns public route namespaces and forwards requests to domain services:

- `/api/auth/*` -> AuthService
- `/api/users/*` -> UserProfileService
- `/api/document/*` -> DocumentService
- `/api/questions/*` -> QuestionBankService
- `/api/interview/*` -> InterviewService
- `/api/evaluation/*` -> EvaluationService
- `/api/feedback/*` -> FeedbackService
- `/api/payment/*` -> PaymentService
- `/api/news/*` -> NewsService
- `/api/notifications/*` -> NotificationService
- `/api/statistics/*` -> StatisticsService

Admin and Manager namespaces route to the correct domain service rather than to a separate admin or manager service.

## Source-Base Status

The repository is currently stabilized at scaffold level. Do not describe business features as complete unless implementation exists in source. The following remain TODO:

- Business entities and EF migrations.
- JWT generation, validation, and enforcement.
- Real business endpoints.
- Cross-service event contracts and consumers.
- AI-backed question/evaluation workflows.
- Payment provider integration.

## Build

```powershell
dotnet build backend\DevPrep.sln
```

Run the gateway:

```powershell
dotnet run --project backend\DevPrep.ApiGateway\DevPrep.ApiGateway.csproj
```
