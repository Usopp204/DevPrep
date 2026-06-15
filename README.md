# DevPrep

DevPrep is an AI-powered mock interview simulator and IT competency evaluation system built as a Pure Microservices monorepo. The current source base is in infrastructure/scaffold stabilization: service projects, routing, environment layout, and contracts are aligned, while business entities, migrations, authentication enforcement, and real business endpoints are intentionally not implemented yet.

## Repository Structure

```text
DevPrep/
|-- backend/       # ASP.NET Core Pure Microservices + YARP API Gateway
|-- frontend/      # Vite React application
|-- ai-service/    # FastAPI AI service, internal-only
|-- contracts/     # Source-of-truth API/data contracts tracked by Git
|-- docs/          # Architecture and engineering documentation
|-- .env.example   # Shared runtime configuration template
`-- .env           # Local runtime configuration, never committed
```

## Backend Architecture

Backend uses Pure Microservices. `backend/DevPrep.sln` contains exactly 12 API projects:

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

## Gateway Route Ownership

Frontend calls go through `DevPrep.ApiGateway`; browser clients must not call downstream services directly.

| Public route | Downstream service |
| --- | --- |
| `/api/auth/*` | `DevPrep.AuthService` |
| `/api/users/*` | `DevPrep.UserProfileService` |
| `/api/document/*` | `DevPrep.DocumentService` |
| `/api/questions/*` | `DevPrep.QuestionBankService` |
| `/api/interview/*` | `DevPrep.InterviewService` |
| `/api/evaluation/*` | `DevPrep.EvaluationService` |
| `/api/feedback/*` | `DevPrep.FeedbackService` |
| `/api/payment/*` | `DevPrep.PaymentService` |
| `/api/news/*` | `DevPrep.NewsService` |
| `/api/notifications/*` | `DevPrep.NotificationService` |
| `/api/statistics/*` | `DevPrep.StatisticsService` |

Admin routes such as `/api/admin/questions/*`, `/api/admin/feedbacks/*`, `/api/admin/news/*`, `/api/admin/payments/*`, and `/api/admin/statistics/*` are routed to the appropriate domain service. Manager routes such as `/api/admin/manager/questions/*` follow the same domain-service routing model.

The AI service is internal-only. Do not document or expose public frontend-facing AI gateway endpoints.

## Local Setup

1. Copy `.env.example` to `.env` and set local values. Keep `.env` out of Git.
2. Build backend:

```powershell
dotnet build backend\DevPrep.sln
```

3. Run the gateway:

```powershell
dotnet run --project backend\DevPrep.ApiGateway\DevPrep.ApiGateway.csproj
```

4. Run an individual service when needed:

```powershell
dotnet run --project backend\DevPrep.AuthService\DevPrep.AuthService.csproj
```

5. Run frontend:

```powershell
cd frontend
npm install
npm run dev
```

6. Run AI service for internal backend integration work:

```powershell
cd ai-service
python -m pip install -r requirements.txt
python -m uvicorn app.main:app --reload
```

## Current Implementation Status

- Service projects are scaffolded and independently runnable.
- Each business service has a local `DbContext`.
- Each service reads its own `*_API_PORT` and `*_DB_CONNECTION_STRING` from environment configuration.
- Gateway routing is aligned to the current service boundaries.
- Contracts are tracked under `contracts/`.
- Real business endpoints, persistence models, migrations, JWT enforcement, and cross-service workflows remain TODO.
