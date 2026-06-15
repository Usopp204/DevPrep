# DevPrep Backend

## Purpose

The backend is an ASP.NET Core .NET 8 Pure Microservices backend composed of one YARP API Gateway and 11 business microservices.

The current backend is scaffold/stabilization level. Service projects, ports, gateway routes, and local `DbContext` placeholders are in place. Business entities, migrations, authentication enforcement, and real business endpoints are intentionally not implemented yet.

## Solution Projects

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

## Build and Run

Build the full backend solution:

```powershell
dotnet build backend\DevPrep.sln
```

Run the API Gateway:

```powershell
dotnet run --project backend\DevPrep.ApiGateway\DevPrep.ApiGateway.csproj
```

Run an individual service:

```powershell
dotnet run --project backend\DevPrep.AuthService\DevPrep.AuthService.csproj
dotnet run --project backend\DevPrep.UserProfileService\DevPrep.UserProfileService.csproj
dotnet run --project backend\DevPrep.QuestionBankService\DevPrep.QuestionBankService.csproj
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
| `/api/auth/*` | AuthService |
| `/api/users/*` | UserProfileService |
| `/api/document/*` | DocumentService |
| `/api/questions/*` | QuestionBankService |
| `/api/interview/*` | InterviewService |
| `/api/evaluation/*` | EvaluationService |
| `/api/feedback/*` | FeedbackService |
| `/api/payment/*` | PaymentService |
| `/api/news/*` | NewsService |
| `/api/notifications/*` | NotificationService |
| `/api/statistics/*` | StatisticsService |

Admin and Manager namespaces route to domain services. For example, `/api/admin/questions/*` and `/api/admin/manager/questions/*` route to QuestionBankService.

## Structure

```text
backend/
|-- DevPrep.sln
|-- DevPrep.ApiGateway/
|-- DevPrep.AuthService/
|-- DevPrep.UserProfileService/
|-- DevPrep.DocumentService/
|-- DevPrep.QuestionBankService/
|-- DevPrep.InterviewService/
|-- DevPrep.EvaluationService/
|-- DevPrep.FeedbackService/
|-- DevPrep.PaymentService/
|-- DevPrep.NewsService/
|-- DevPrep.NotificationService/
`-- DevPrep.StatisticsService/
```

Each business service owns its own data model and `DbContext`. Do not add shared backend class libraries without explicit approval.
