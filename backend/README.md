# Backend (ASP.NET Core)

## Purpose
Modular monolith API server for DevPrep.

## Local Run (No Docker)
1. Ensure `.env` exists at repo root (copy from `.env.example`).
2. Build:
   ```
   dotnet build backend/DevPrep.sln
   ```
3. Run:
   ```
   dotnet run --project backend/src/DevPrep.Api/DevPrep.Api.csproj
   ```
4. Health check:
   ```
   http://localhost:5000/health
   ```

## Structure
```
backend/
├─ DevPrep.sln
└─ src/
   ├─ DevPrep.Api/
   ├─ DevPrep.Core/
   ├─ DevPrep.Infrastructure/
   └─ DevPrep.Shared/
```
