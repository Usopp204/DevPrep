# DevPrep Contracts — Core State (Contract-First)

> **Single Source of Truth** for cross-service contracts in the Pure Microservices architecture.

## 1) Architecture Shift (Hybrid → Pure Microservices)
- **Old concept:** Hybrid Modular Monolithic (deprecated)
- **Current:** **Pure Microservices**
  - Backend: **.NET 8** (6 services) + **YARP API Gateway**
  - Gateway Port: **7000**
  - AuthService Port: **7001**
  - AI Service: **Python 3.11 + FastAPI**, Port **8000**

## 2) Recent Accomplishments (Baseline Sync)
- Base source code scaffolded for backend, gateway, AI service, frontend.
- **YARP Gateway** configured to route `/api/auth/*` → **AuthService** (7001).
- Root **.env** finalized and used across services.

## 3) Crucial Logic — Dynamic Message Broker
MassTransit transport is **dynamic** based on `USE_RABBITMQ`:
- `USE_RABBITMQ=false` → **In-Memory Bus** (local dev, no RabbitMQ required)
- `USE_RABBITMQ=true` → **RabbitMQ** via `UsingRabbitMq(...)`

## 4) Blacklist / Anti‑Patterns (Hard Rules)
- **Do NOT upgrade MassTransit above 8.1.3** (license enforcement issues in newer versions).
- **Do NOT remove `extra="ignore"`** in Python Pydantic settings (FastAPI must ignore extra env vars).
- **Do NOT run servers in hidden/background mode** (must keep visible logs during dev).
- **Do NOT change Gateway ↔ Service port mappings** without updating contracts/docs first.

---

## Audit Status
At the time of this update, the `contracts/` directory contained **no existing contract files**.
This README is now the anchor for all future contract work.

**Contract‑First rule:** Always update contracts/docs **before or with** code changes.
