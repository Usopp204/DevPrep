# Pure Microservices Baseline

This document records the confirmed backend source-base architecture for DevPrep.

## Backend Projects

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

## Gateway Route Ownership

| Route namespace | Downstream service |
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
| `/api/statistics/*` | `StatisticsAPI` |

Admin route namespaces are routed to domain services, for example:

- `/api/admin/questions/*` and `/api/admin/manager/questions/*` -> QuestionAPI
- `/api/admin/feedbacks/*` and `/api/admin/manager/feedbacks/*` -> FeedbackAPI
- `/api/admin/news/*` and `/api/admin/manager/news/*` -> NewsAPI
- `/api/admin/payments/*` and `/api/admin/manager/payments/*` -> PaymentAPI
- `/api/admin/statistics/*` and `/api/admin/manager/statistics/*` -> StatisticsAPI

Gateway routes should apply prefix removal consistently so downstream services expose clean internal routes.

## Database Boundaries

| Database | Service |
| --- | --- |
| `DevPrep_Auth` | AuthAPI |
| `DevPrep_User` | UserAPI |
| `DevPrep_CvJd` | CvJdAPI |
| `DevPrep_Question` | QuestionAPI |
| `DevPrep_Interview` | InterviewAPI |
| `DevPrep_Evaluation` | EvaluationAPI |
| `DevPrep_Feedback` | FeedbackAPI |
| `DevPrep_Payment` | PaymentAPI |
| `DevPrep_News` | NewsAPI |
| `DevPrep_Notification` | NotificationAPI |
| `DevPrep_Statistics` | StatisticsAPI |

Each business service owns its local `DbContext` and reads its own `*_DB_CONNECTION_STRING` and `*_API_PORT` from environment configuration.

## AI Service

The FastAPI AI service is internal-only. Do not document or expose public WebClient-facing AI API routes through the gateway.

## Current Source Status

This source base is scaffold/stabilization level. Service projects, local `DbContext` placeholders, health endpoints, environment keys, and gateway routes are aligned. Business entities, migrations, JWT enforcement, real business endpoints, event consumers, and AI workflows remain intentionally unimplemented.


