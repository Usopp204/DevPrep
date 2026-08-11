# syntax=docker/dockerfile:1
#
# WebClient — build context is the REPOSITORY ROOT, not source/.
# See source/WebClient/Dockerfile.dockerignore and phase1-checklist.md blocker B-1.
#
# Build from the repo root:
#   docker build -f source/WebClient/Dockerfile -t devprep-webclient:test .

FROM mcr.microsoft.com/dotnet/sdk:8.0.404-noble AS build
WORKDIR /src

COPY source/DevPrep.sln source/
COPY source/WebClient/WebClient.csproj source/WebClient/
RUN dotnet restore source/WebClient/WebClient.csproj

COPY source/ source/
RUN dotnet publish source/WebClient/WebClient.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0.11-noble AS final
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && groupadd --system appgroup \
 && useradd --system --gid appgroup --shell /usr/sbin/nologin appuser
WORKDIR /app
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser

# --- B-2: this service is the exception that CAN use ASPNETCORE_URLS -------------
# WebClient/Program.cs contains no UseUrls() call, so nothing overrides the standard
# ASPNETCORE_URLS variable and it is honoured normally. (The nine backend services that
# do call UseUrls need SERVICE_BIND_HOST + <SVC>_API_PORT instead — see their
# Dockerfiles.) Local dev is unaffected: run-all.ps1 passes `--urls http://localhost:5130`
# explicitly and never reads this file.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# --- B-3: no HEALTHCHECK yet ----------------------------------------------------
# WebClient has no /health endpoint, so a `curl /health` probe would fail forever and
# leave the container permanently `unhealthy`. Probing `/` instead is unreliable —
# the root path redirects to the login page for anonymous users, and curl -f treats
# the response inconsistently depending on the redirect.
#
# Adding /health means editing source/WebClient/Program.cs, which is NOT part of
# Batch B's file list, so it is deliberately NOT pulled forward here.
#
# Batch H must not use `condition: service_healthy` against this service until then.

ENTRYPOINT ["dotnet", "WebClient.dll"]
