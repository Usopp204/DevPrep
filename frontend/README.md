# Frontend (React + Vite + TypeScript)

## Purpose

Client UI for DevPrep. The frontend must call the backend through `DevPrep.ApiGateway`; it must not call downstream services or the internal AI service directly.

Current implementation is scaffold-level and may only contain smoke-test UI until business flows are implemented.

## Local Run

1. Ensure `.env` exists at the repository root if local runtime values are needed.
2. Install dependencies:

```powershell
cd frontend
npm install
```

3. Run:

```powershell
npm run dev
```

4. Default Vite URL:

```text
http://localhost:5173
```

## Configuration

Use one gateway base URL environment value for API calls. The shared backend gateway default is:

```text
VITE_API_GATEWAY_URL=http://localhost:7000
```
