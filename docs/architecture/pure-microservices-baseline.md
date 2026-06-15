# Pure Microservices Baseline

This document records the confirmed backend source-base architecture for DevPrep.

## Backend Projects

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

## Gateway Route Ownership

| Route namespace | Downstream service |
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

Admin route namespaces are routed to domain services, for example:

- `/api/admin/questions/*` and `/api/admin/manager/questions/*` -> QuestionBankService
- `/api/admin/feedbacks/*` and `/api/admin/manager/feedbacks/*` -> FeedbackService
- `/api/admin/news/*` and `/api/admin/manager/news/*` -> NewsService
- `/api/admin/payments/*` and `/api/admin/manager/payments/*` -> PaymentService
- `/api/admin/statistics/*` and `/api/admin/manager/statistics/*` -> StatisticsService

Gateway routes should apply prefix removal consistently so downstream services expose clean internal routes.

## Database Boundaries

| Database | Service |
| --- | --- |
| `DevPrep_Auth` | AuthService |
| `DevPrep_User` | UserProfileService |
| `DevPrep_CvJd` | DocumentService |
| `DevPrep_Question` | QuestionBankService |
| `DevPrep_Interview` | InterviewService |
| `DevPrep_Evaluation` | EvaluationService |
| `DevPrep_Feedback` | FeedbackService |
| `DevPrep_Payment` | PaymentService |
| `DevPrep_News` | NewsService |
| `DevPrep_Notification` | NotificationService |
| `DevPrep_Statistics` | StatisticsService |

Each business service owns its local `DbContext` and reads its own `*_DB_CONNECTION_STRING` and `*_API_PORT` from environment configuration.

## AI Service

The FastAPI AI service is internal-only. Do not document or expose public frontend-facing AI API routes through the gateway.

## Current Source Status

This source base is scaffold/stabilization level. Service projects, local `DbContext` placeholders, health endpoints, environment keys, and gateway routes are aligned. Business entities, migrations, JWT enforcement, real business endpoints, event consumers, and AI workflows remain intentionally unimplemented.
