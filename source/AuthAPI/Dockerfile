# syntax=docker/dockerfile:1
#
# AuthAPI — build context is the REPOSITORY ROOT, not source/.
# See source/AuthAPI/Dockerfile.dockerignore and phase1-checklist.md blocker B-1.
#
# Build from the repo root:
#   docker build -f source/AuthAPI/Dockerfile -t devprep-authapi:test .

FROM mcr.microsoft.com/dotnet/sdk:8.0.404-noble AS build
WORKDIR /src

COPY source/DevPrep.sln source/
COPY source/AuthAPI/AuthAPI.csproj source/AuthAPI/
# AuthAPI.csproj:22 references ..\..\contracts\auth\auth.csproj, which lives OUTSIDE
# source/. This is exactly why the build context has to be the repository root
# (blocker B-1) — Docker cannot COPY from outside the context.
COPY contracts/auth/auth.csproj contracts/auth/
RUN dotnet restore source/AuthAPI/AuthAPI.csproj

COPY source/ source/
COPY contracts/ contracts/
RUN dotnet publish source/AuthAPI/AuthAPI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0.11-noble AS final
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && groupadd --system appgroup \
 && useradd --system --gid appgroup --shell /usr/sbin/nologin appuser
WORKDIR /app
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser

# --- B-2: AuthAPI is one of the two services that CAN use ASPNETCORE_URLS ---------
# AuthAPI/Program.cs contains NO UseUrls() call and reads no *_API_PORT variable —
# it is the exception to the "every backend service calls UseUrls" rule. Nothing
# overrides ASPNETCORE_URLS here, so the standard variable is honoured normally.
# (The nine services that DO call UseUrls need SERVICE_BIND_HOST + <SVC>_API_PORT
# instead; setting those here would have no effect at all.)
# Local dev is unaffected: run-all.ps1:79 passes `--urls http://localhost:7001`
# explicitly and never reads this file.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# AuthAPI does have a /health endpoint (Program.cs:191), unlike ApiGateway and WebClient.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "AuthAPI.dll"]
