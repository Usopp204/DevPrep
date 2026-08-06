# DevPrep

DevPrep is an AI-powered mock interview simulator and IT competency evaluation system built as a Pure Microservices monorepo. The current source base is in infrastructure/scaffold stabilization: service projects, routing, environment layout, and contracts are aligned, while business entities, migrations, authentication enforcement, and real business endpoints are intentionally not implemented yet.

## Repository Structure

```text
DevPrep/
|-- source/        # ASP.NET Core APIs, YARP API Gateway, and MVC WebClient
|-- ai-service/    # FastAPI AI service, internal-only
|-- contracts/     # Source-of-truth API/data contracts tracked by Git
|-- docs/          # Architecture and engineering documentation
|-- .env.example   # Shared runtime configuration template
`-- .env           # Local runtime configuration, never committed
```

## Backend Architecture

Source uses Pure Microservices for APIs. `source/DevPrep.sln` contains 11 API/Gateway projects plus `WebClient`:

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
12. `WebClient`

Admin and Manager are implemented as roles and route namespaces. Manager access is read-only through `/api/admin/manager/*` routes where needed.

## Gateway Route Ownership

WebClient calls go through `ApiGateway`; browser clients must not call downstream services directly.

| Public route | Downstream service |
| --- | --- |
| `/api/auth/*` | `AuthAPI` |
| `/api/users/*` | `UserAPI` |
| `/api/document/*` | `CvJdAPI` |
| `/api/questions/*` | `QuestionAPI` |
| `/api/interview/*` | `InterviewAPI` |
| `/api/evaluation/*` | `EvaluationAPI` |
| `/api/feedback/*` | `FeedbackAPI` |
| `/api/payment/*` | `PaymentAPI` |
| `/api/news/*` | `NewsAPI` |
| `/api/notifications/*` | `NotificationAPI` |

Admin routes such as `/api/admin/questions/*`, `/api/admin/feedbacks/*`, `/api/admin/news/*`, `/api/admin/payments/*` are routed to the appropriate domain service. Manager routes such as `/api/admin/manager/questions/*` follow the same domain-service routing model.

The AI service is internal-only. Do not document or expose public WebClient-facing AI gateway endpoints.

## Local Setup

1. Copy `.env.example` to `.env` and set local values. Keep `.env` out of Git.
2. Build backend:

```powershell
dotnet build source\DevPrep.sln
```

3. Run the gateway:

```powershell
dotnet run --project source\ApiGateway\ApiGateway.csproj
```

4. Run an individual service when needed:

```powershell
dotnet run --project source\AuthAPI\AuthAPI.csproj
```

5. Run WebClient:

```powershell
dotnet run --project source\WebClient\WebClient.csproj
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


