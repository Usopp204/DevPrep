# DevPrep Backend

## Purpose

The backend is an ASP.NET Core .NET 8 Pure Microservices backend composed of one YARP API Gateway and 11 business microservices.

The current backend is scaffold/stabilization level. Service projects, ports, gateway routes, and local `DbContext` placeholders are in place. Business entities, migrations, authentication enforcement, and real business endpoints are intentionally not implemented yet.

## Solution Projects

`source/DevPrep.sln` contains 12 API/Gateway projects plus `WebClient`:

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
13. `WebClient`

Admin and Manager are implemented as roles and route namespaces. Manager access is read-only through `/api/admin/manager/*` routes where needed.

## Build and Run

Build the full backend solution:

```powershell
dotnet build source\DevPrep.sln
```

Run the API Gateway:

```powershell
dotnet run --project source\ApiGateway\ApiGateway.csproj
```

Run an individual service:

```powershell
dotnet run --project source\AuthAPI\AuthAPI.csproj
dotnet run --project source\UserAPI\UserAPI.csproj
dotnet run --project source\QuestionAPI\QuestionAPI.csproj
```

Service ports are configured through `.env` using each service's `*_API_PORT` key. `.env.example` is the shared template; `.env` is local-only and must not be committed.

## Health Checks

Each downstream service exposes a scaffold health endpoint at `/health` on its own configured port. Example defaults:

| Service | Default URL |
| --- | --- |
| Gateway | `http://localhost:7000` |
| Auth | `http://localhost:7001/health` |
| UserProfile | `http://localhost:7002/health` |
| Document | `http://localhost:7003/health` |
| QuestionBank | `http://localhost:7004/health` |
| Interview | `http://localhost:7005/health` |
| Evaluation | `http://localhost:7006/health` |
| Feedback | `http://localhost:7007/health` |
| Payment | `http://localhost:7008/health` |
| News | `http://localhost:7009/health` |
| Notification | `http://localhost:7010/health` |
| Statistics | `http://localhost:7011/health` |

## Gateway Routes

| Public route | Downstream service |
| --- | --- |
| `/api/auth/*` | AuthAPI |
| `/api/users/*` | UserAPI |
| `/api/document/*` | CvJdAPI |
| `/api/questions/*` | QuestionAPI |
| `/api/interview/*` | InterviewAPI |
| `/api/evaluation/*` | EvaluationAPI |
| `/api/feedback/*` | FeedbackAPI |
| `/api/payment/*` | PaymentAPI |
| `/api/news/*` | NewsAPI |
| `/api/notifications/*` | NotificationAPI |
| `/api/statistics/*` | StatisticsAPI |

Admin and Manager namespaces route to domain services. For example, `/api/admin/questions/*` and `/api/admin/manager/questions/*` route to QuestionAPI.

## Structure

```text
source/
|-- DevPrep.sln
|-- ApiGateway/
|-- AuthAPI/
|-- UserAPI/
|-- CvJdAPI/
|-- QuestionAPI/
|-- InterviewAPI/
|-- EvaluationAPI/
|-- FeedbackAPI/
|-- PaymentAPI/
|-- NewsAPI/
|-- NotificationAPI/
|-- StatisticsAPI/
`-- WebClient/
```

Each business service owns its own data model and `DbContext`. Do not add shared backend class libraries without explicit approval.


