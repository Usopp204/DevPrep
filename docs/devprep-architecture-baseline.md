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

1. `ApiGateway`
2. `AuthAPI`
3. `UserAPI`
4. `CvJdAPI`
5. `QuestionAPI`
6. `InterviewAPI`
7. `EvaluationAPI`
8. `FeedbackAPI`
9. `PaymentAPI`
10. `NewsAPI`
11. `NotificationAPI`
12. `StatisticsAPI`

Admin and Manager are implemented as roles and route namespaces. Manager access is read-only through `/api/admin/manager/*` routes where needed.

## Gateway Routes

The gateway owns public route namespaces and forwards requests to domain services:

- `/api/auth/*` -> AuthAPI
- `/api/users/*` -> UserAPI
- `/api/document/*` -> CvJdAPI
- `/api/questions/*` -> QuestionAPI
- `/api/interview/*` -> InterviewAPI
- `/api/evaluation/*` -> EvaluationAPI
- `/api/feedback/*` -> FeedbackAPI
- `/api/payment/*` -> PaymentAPI
- `/api/news/*` -> NewsAPI
- `/api/notifications/*` -> NotificationAPI
- `/api/statistics/*` -> StatisticsAPI

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
dotnet run --project backend\ApiGateway\ApiGateway.csproj
```

