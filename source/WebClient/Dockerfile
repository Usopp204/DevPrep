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

# --- H-2 (corrected): ASPNETCORE_URLS does NOT work here — use UseUrls() instead --
# appsettings.json used to carry its own "Urls" key, which occupies the exact
# configuration key ASPNETCORE_URLS would need to override. The unprefixed
# environment-variable provider that builds the app's IConfiguration reads
# ASPNETCORE_URLS under the literal key "ASPNETCORE_URLS", never "urls" — so it
# never touched appsettings.json's entry, and the container listened on
# localhost:5130 (unreachable from the host) no matter what ASPNETCORE_URLS said.
# Program.cs now calls UseUrls() explicitly, reading SERVICE_BIND_HOST + WEBCLIENT_PORT
# — the same pattern the nine backend services use for the same reason (an explicit
# UseUrls() call is the one thing config-key collisions like this can't defeat) — and
# the stale "Urls" key has been removed from appsettings.json. Local dev is unaffected:
# run-all.ps1 passes `--urls http://localhost:5130` on the command line, which still
# wins there, and the code defaults (localhost:5130) match it even if it didn't.
ENV SERVICE_BIND_HOST=0.0.0.0 \
    WEBCLIENT_PORT=8080 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# /health is a middleware short-circuit registered before UseHttpsRedirection
# (Program.cs) specifically so this probe hits it directly over plain HTTP with no
# redirect — a curl -f against a redirect would report success even if the app past
# that point were broken, defeating the healthcheck. See Program.cs's comment.
HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "WebClient.dll"]
