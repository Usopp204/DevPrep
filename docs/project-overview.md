# DevPrep Project Overview

DevPrep is an AI-powered mock interview simulator and IT competency evaluation system built as a Pure Microservices monorepo. This document describes the current confirmed source-base architecture and scaffold status.

## Repository

```text
DevPrep/
|-- source/
|-- ai-service/
|-- contracts/
`-- docs/
```

## Product Direction

The intended product supports interview preparation flows such as account/profile setup, CV/JD input, interview sessions, evaluation, feedback, payments, news, notifications, and statistics. These flows are not yet fully implemented in code.

## Current Source Architecture

Source uses Pure Microservices for APIs and an ASP.NET Core MVC WebClient. The solution contains one gateway, 11 business services, and WebClient:

- `ApiGateway`
- `AuthAPI`
- `UserAPI`
- `CvJdAPI`
- `QuestionAPI`
- `InterviewAPI`
- `EvaluationAPI`
- `FeedbackAPI`
- `PaymentAPI`
- `NewsAPI`
- `NotificationAPI`
- `StatisticsAPI`
- `WebClient`

Admin and Manager are implemented as roles and route namespaces. Manager access is read-only through `/api/admin/manager/*` routes where needed.

## Service Boundaries

| Capability | Service |
| --- | --- |
| Authentication and token-related behavior | AuthAPI |
| User profile data | UserAPI |
| CV/JD upload, parsing metadata, matching inputs | CvJdAPI |
| Question bank management/read models | QuestionAPI |
| Interview/session orchestration | InterviewAPI |
| Scoring and reports | EvaluationAPI |
| System/interview feedback | FeedbackAPI |
| Credits, plans, payment records | PaymentAPI |
| News/articles | NewsAPI |
| Email templates, logs, notifications | NotificationAPI |
| Metrics and dashboard statistics | StatisticsAPI |

## Current Implementation Status

This is source-base stabilization. Infrastructure shape is aligned, but business implementation is incomplete. Entities, migrations, endpoint handlers, auth enforcement, integrations, and workflows must be implemented in later feature work.

## Contracts

`contracts/` is tracked by Git and is the source-of-truth for cross-service schemas. Update contracts and docs when route ownership or request/response shapes change.



