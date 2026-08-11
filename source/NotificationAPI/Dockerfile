# syntax=docker/dockerfile:1
#
# NotificationAPI — build context is the REPOSITORY ROOT, not source/.
# See source/NotificationAPI/Dockerfile.dockerignore and phase1-checklist.md blocker B-1.
#
# Build from the repo root:
#   docker build -f source/NotificationAPI/Dockerfile -t devprep-notificationapi:test .

FROM mcr.microsoft.com/dotnet/sdk:8.0.404-noble AS build
WORKDIR /src

# Restore layer first, keyed only on project files, so editing source code does not
# invalidate the (slow) restore.
COPY source/DevPrep.sln source/
COPY source/NotificationAPI/NotificationAPI.csproj source/NotificationAPI/
# NotificationAPI.csproj:19 references ..\..\contracts\auth\auth.csproj, which lives
# OUTSIDE source/. This is exactly why the build context has to be the repository root
# (blocker B-1) — Docker cannot COPY from outside the context.
COPY contracts/auth/auth.csproj contracts/auth/
RUN dotnet restore source/NotificationAPI/NotificationAPI.csproj

COPY source/ source/
COPY contracts/ contracts/
RUN dotnet publish source/NotificationAPI/NotificationAPI.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0.11-noble AS final
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && groupadd --system appgroup \
 && useradd --system --gid appgroup --shell /usr/sbin/nologin appuser
WORKDIR /app
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser

# --- B-2: bind 0.0.0.0 inside the container -----------------------------------
# Program.cs:22-23 calls UseUrls($"http://{bindHost}:{servicePort}"), and an explicit
# UseUrls() overrides ASPNETCORE_URLS — so setting ASPNETCORE_URLS here would be
# silently ignored and the service would bind container-loopback only, unreachable
# from sibling containers. These two variables are the inputs UseUrls actually reads.
# Local dev is unaffected: run-all.ps1 does not read this file, so the code defaults
# (localhost:7010) still apply there.
ENV SERVICE_BIND_HOST=0.0.0.0 \
    NOTIFICATION_API_PORT=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "NotificationAPI.dll"]
