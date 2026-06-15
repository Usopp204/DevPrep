# DevPrep Contracts

`contracts/` is the source-of-truth location for cross-service schemas and interface definitions. This directory must be tracked by Git.

## Current Contract Files

```text
contracts/
|-- README.md
|-- feedback/
|   |-- interview-feedback.schema.json
|   `-- system-feedback.schema.json
`-- interview/
    `-- score-v2.schema.json
```

## Current Architecture Context

Backend uses 11 business service boundaries behind `DevPrep.ApiGateway`:

- Auth
- UserProfile
- Document
- QuestionBank
- Interview
- Evaluation
- Feedback
- Payment
- News
- Notification
- Statistics

Admin and Manager are implemented as roles and route namespaces. Manager access is read-only through `/api/admin/manager/*` routes where needed. Contract changes that affect request/response shapes, route ownership, or service boundaries must be updated here together with the relevant documentation.

## Implementation Status

Contracts exist before full business implementation. Service entities, migrations, and real endpoint contracts are still incomplete and should be added deliberately as features are implemented.
