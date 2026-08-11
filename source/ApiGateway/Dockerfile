# syntax=docker/dockerfile:1
#
# ApiGateway — build context is the REPOSITORY ROOT, not source/.
# See source/ApiGateway/Dockerfile.dockerignore and phase1-checklist.md blocker B-1.
#
# Build from the repo root:
#   docker build -f source/ApiGateway/Dockerfile -t devprep-apigateway:test .

FROM mcr.microsoft.com/dotnet/sdk:8.0.404-noble AS build
WORKDIR /src

COPY source/DevPrep.sln source/
COPY source/ApiGateway/ApiGateway.csproj source/ApiGateway/
RUN dotnet restore source/ApiGateway/ApiGateway.csproj

COPY source/ source/
RUN dotnet publish source/ApiGateway/ApiGateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0.11-noble AS final
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && groupadd --system appgroup \
 && useradd --system --gid appgroup --shell /usr/sbin/nologin appuser
WORKDIR /app
COPY --from=build --chown=appuser:appgroup /app/publish .
USER appuser

# --- B-2: port only; the gateway already binds 0.0.0.0 --------------------------
# Program.cs:11 calls UseUrls($"http://0.0.0.0:{gatewayPort}") — the host is already
# correct, but gatewayPort reads GLOBAL_API_GATEWAY_PORT (Program.cs:10) and defaults
# to 7000. Without this the container would listen on 7000 while the rest of the
# stack expects 8080. SERVICE_BIND_HOST is deliberately absent: this service does not
# read it. Local dev keeps its 7000 default because run-all.ps1 never reads this file.
ENV GLOBAL_API_GATEWAY_PORT=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# --- B-3: no HEALTHCHECK yet ----------------------------------------------------
# ApiGateway has NO /health endpoint — Program.cs maps only the reverse proxy, so a
# `curl /health` probe would fail forever and the container would sit permanently
# `unhealthy`, which would in turn block every `depends_on: condition: service_healthy`
# in Batch H.
#
# Adding /health means editing source/ApiGateway/Program.cs, which is NOT part of
# Batch B's file list, so it is deliberately NOT pulled forward here. When it is added
# it must be registered BEFORE app.MapReverseProxy() or the proxy catch-all swallows it.
#
# Batch H must not use `condition: service_healthy` against this service until then.

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
