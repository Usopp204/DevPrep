# DevPrep Project Overview

DevPrep is an AI-powered mock interview simulator and IT competency evaluation system built as a Pure Microservices monorepo. This document describes the current confirmed source-base architecture and scaffold status.

## Repository

```text
DevPrep/
|-- backend/
|-- frontend/
|-- ai-service/
|-- contracts/
`-- docs/
```

## Product Direction

The intended product supports interview preparation flows such as account/profile setup, CV/JD input, interview sessions, evaluation, feedback, payments, news, notifications, and statistics. These flows are not yet fully implemented in code.

## Current Backend Architecture

Backend uses Pure Microservices. The solution contains one gateway and 11 business services:

- `DevPrep.ApiGateway`
- `DevPrep.AuthService`
- `DevPrep.UserProfileService`
- `DevPrep.DocumentService`
- `DevPrep.QuestionBankService`
- `DevPrep.InterviewService`
- `DevPrep.EvaluationService`
- `DevPrep.FeedbackService`
- `DevPrep.PaymentService`
- `DevPrep.NewsService`
- `DevPrep.NotificationService`
- `DevPrep.StatisticsService`

Admin and Manager are implemented as roles and route namespaces. Manager access is read-only through `/api/admin/manager/*` routes where needed.

## Service Boundaries

| Capability | Service |
| --- | --- |
| Authentication and token-related behavior | AuthService |
| User profile data | UserProfileService |
| CV/JD upload, parsing metadata, matching inputs | DocumentService |
| Question bank management/read models | QuestionBankService |
| Interview/session orchestration | InterviewService |
| Scoring and reports | EvaluationService |
| System/interview feedback | FeedbackService |
| Credits, plans, payment records | PaymentService |
| News/articles | NewsService |
| Email templates, logs, notifications | NotificationService |
| Metrics and dashboard statistics | StatisticsService |

## Current Implementation Status

This is source-base stabilization. Infrastructure shape is aligned, but business implementation is incomplete. Entities, migrations, endpoint handlers, auth enforcement, integrations, and workflows must be implemented in later feature work.

## Contracts

`contracts/` is tracked by Git and is the source-of-truth for cross-service schemas. Update contracts and docs when route ownership or request/response shapes change.
