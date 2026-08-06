# =====================================================================
# DevPrep - Run the whole system with one command
# Usage:  cd into the repo root, then run:  .\run-all.ps1
# Each service opens in its own PowerShell window (to watch logs/errors).
# Close all those windows to shut the system down.
#
# WDAC NOTE (important on this machine):
#   Some services (confirmed: EvaluationAPI, WebClient) are blocked when
#   launched via the app host that `dotnet run` produces
#   (Win32Exception 4551 / 0x800711C7). WDAC behavior is inconsistent
#   per-service and per-rebuild, so this script runs EVERY .NET service the
#   safe way: `dotnet build` once, then `dotnet <Service>.dll`. Running the
#   DLL through dotnet.exe is not blocked, so this works for every service
#   whether or not WDAC currently blocks its app host.
#
# Prerequisites:
#   - SQL Server running with all required databases (DevPrep_Auth,
#     DevPrep_User, DevPrep_Payment, DevPrep_Question, DevPrep_Interview,
#     DevPrep_Evaluation, DevPrep_Feedback, DevPrep_News, DevPrep_Notification,
#     DevPrep_CvJd auto-creates if missing).
#     Connection details come from .env (already standardized by the team).
#   - ai-service venv installed (.venv) with dependencies
#     (pip install -r requirements.txt).
# =====================================================================

$root = $PSScriptRoot

# When running a DLL directly, launchSettings.json is ignored, so the
# environment and URL must be set explicitly. Target framework is net8.0.
$Tfm = "net8.0"

# ---------------------------------------------------------------------
# Pre-build phase: build everything ONCE, sequentially, so build errors
# surface here (in one place) instead of flashing by in 12 separate
# windows. This also avoids concurrent-build file-lock races on shared
# project references.
# ---------------------------------------------------------------------
$sln = Get-ChildItem -Path $root -Recurse -Depth 2 -Filter *.sln -File | Select-Object -First 1
if ($sln) {
    Write-Host "Pre-building solution: $($sln.Name) ..." -ForegroundColor Cyan
    dotnet build $sln.FullName -c Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "" 
        Write-Host "Solution build FAILED. Fix the errors above before starting services." -ForegroundColor Red
        exit 1
    }
    Write-Host "Solution build OK." -ForegroundColor Green
} else {
    Write-Host "No .sln found; each service window will build its own project on first run." -ForegroundColor Yellow
}

function Start-DotNetService {
    param(
        [string]$Name,
        [string]$Project,   # relative to source/, e.g. "AuthAPI/AuthAPI.csproj"
        [int]$Port
    )
    $projDir    = "source/" + (Split-Path $Project -Parent)
    $csprojName = Split-Path $Project -Leaf
    $dllName    = [IO.Path]::GetFileNameWithoutExtension($Project) + ".dll"
    $dllPath    = "bin/Debug/$Tfm/$dllName"

    Write-Host "Starting $Name on port $Port (dll: $projDir/$dllPath)..." -ForegroundColor Cyan

    # WDAC-safe launch: run the DLL via dotnet.exe (never the app host).
    # MUST cd into the project dir first: ASP.NET Core's content root is the
    # current directory, and appsettings.json (Jwt config etc.) is only found
    # there. DotNetEnv's TraversePath() still finds the repo-root .env by
    # walking up from the project dir.
    # If the pre-build was skipped and the DLL is missing, build it here.
    $cmd = @"
cd '$root/$projDir'
`$host.UI.RawUI.WindowTitle = '$Name (port $Port)'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
if (-not (Test-Path '$dllPath')) {
    Write-Host 'DLL not found - building $Name...' -ForegroundColor Yellow
    dotnet build '$csprojName' -c Debug
}
dotnet '$dllPath' --urls http://localhost:$Port
"@

    Start-Process powershell -ArgumentList @("-NoExit", "-Command", $cmd)
}

# --- Backend microservices ---
# Staggered by 4s each: launching all 10 at once overwhelmed SQL Server at
# startup (SqlException 233 in NotificationAPI's seed query).
Start-DotNetService -Name "AuthAPI"         -Project "AuthAPI/AuthAPI.csproj"                 -Port 7001
Start-Sleep -Seconds 4
Start-DotNetService -Name "UserAPI"         -Project "UserAPI/UserAPI.csproj"                 -Port 7002
Start-Sleep -Seconds 4
Start-DotNetService -Name "CvJdAPI"         -Project "CvJdAPI/CvJdAPI.csproj"                 -Port 7003
Start-Sleep -Seconds 4
Start-DotNetService -Name "QuestionAPI"     -Project "QuestionAPI/QuestionAPI.csproj"         -Port 7004
Start-Sleep -Seconds 4
Start-DotNetService -Name "InterviewAPI"    -Project "InterviewAPI/InterviewAPI.csproj"       -Port 7005
Start-Sleep -Seconds 4
Start-DotNetService -Name "EvaluationAPI"   -Project "EvaluationAPI/EvaluationAPI.csproj"     -Port 7006
Start-Sleep -Seconds 4
Start-DotNetService -Name "FeedbackAPI"     -Project "FeedbackAPI/FeedbackAPI.csproj"         -Port 7007
Start-Sleep -Seconds 4
Start-DotNetService -Name "PaymentAPI"      -Project "PaymentAPI/PaymentAPI.csproj"           -Port 7008
Start-Sleep -Seconds 4
Start-DotNetService -Name "NewsAPI"         -Project "NewsAPI/NewsAPI.csproj"                 -Port 7009
Start-Sleep -Seconds 4
Start-DotNetService -Name "NotificationAPI" -Project "NotificationAPI/NotificationAPI.csproj" -Port 7010

Start-Sleep -Seconds 2

# --- API Gateway (starts after the services above, before WebClient) ---
Start-DotNetService -Name "ApiGateway" -Project "ApiGateway/ApiGateway.csproj" -Port 7000

Start-Sleep -Seconds 2

# --- ai-service (Python/FastAPI) - model load can take 1-2 minutes ---
# Kept as the venv + uvicorn launch (teammate's method). The Docker path
# runs the SAME app.main:app; both must stay in sync (requirements.txt,
# routers wired in app/main.py, model + Chroma paths usable outside Docker).
Write-Host "Starting ai-service on port 8000..." -ForegroundColor Cyan
$aiCmd = @"
cd '$root\ai-service'
`$host.UI.RawUI.WindowTitle = 'ai-service (port 8000)'
# HF model cache on a drive with space (C: is nearly full; the parser model is 5.2GB).
# Set here because huggingface_hub reads HF_HOME at import time, which can happen
# before the app loads the root .env. Adjust per machine if needed.
`$env:HF_HOME = 'Q:\hf-cache'
if (-not (Test-Path '.venv')) {
    Write-Host '.venv not found. Create it first:' -ForegroundColor Red
    Write-Host '  python -m venv .venv; .\.venv\Scripts\Activate.ps1; pip install -r requirements.txt' -ForegroundColor Yellow
} else {
    .\.venv\Scripts\Activate.ps1
    uvicorn app.main:app --host 0.0.0.0 --port 8000
}
"@
Start-Process powershell -ArgumentList @("-NoExit", "-Command", $aiCmd)

Start-Sleep -Seconds 2

# --- WebClient (MVC frontend) ---
Start-DotNetService -Name "WebClient" -Project "WebClient/WebClient.csproj" -Port 5130

Write-Host ""
Write-Host "=== All windows opened. Wait for each to show 'Now listening on...' then open: ===" -ForegroundColor Green
Write-Host "  WebClient:  http://localhost:5130" -ForegroundColor Yellow
Write-Host "  ApiGateway: http://localhost:7000" -ForegroundColor Yellow
Write-Host "  ai-service: http://localhost:8000/docs (Swagger; model load may take a few minutes)" -ForegroundColor Yellow
