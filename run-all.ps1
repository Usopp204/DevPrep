# =====================================================================
# DevPrep - Chay toan bo he thong bang 1 lenh
# Cach dung:  cd vao thu muc goc repo, roi chay:  .\run-all.ps1
# Moi service se mo trong 1 cua so PowerShell rieng (de xem log/loi).
# Dong tat ca cua so do la tat toan bo he thong.
#
# Yeu cau truoc khi chay:
#   - SQL Server (LAPTOP-ULLKR9SD\SQLEXPRESS) dang chay, da co du cac DB:
#     DevPrep_Auth, DevPrep_User, DevPrep_Payment, DevPrep_Question,
#     DevPrep_Interview, DevPrep_Evaluation, DevPrep_Feedback, DevPrep_News,
#     DevPrep_Notification, DevPrep_Statistics (rieng DevPrep_CvJd se tu
#     dong tao neu chua co).
#   - ai-service da cai xong venv (.venv) va dependencies (pip install -r requirements.txt).
# =====================================================================

$root = $PSScriptRoot

function Start-DotNetService {
    param(
        [string]$Name,
        [string]$Project,
        [int]$Port
    )
    Write-Host "Starting $Name on port $Port..." -ForegroundColor Cyan
    Start-Process powershell -ArgumentList @(
        "-NoExit",
        "-Command",
        "cd '$root'; `$host.UI.RawUI.WindowTitle = '$Name (port $Port)'; dotnet run --project source/$Project --urls http://localhost:$Port"
    )
}

# --- Backend microservices ---
Start-DotNetService -Name "AuthAPI"         -Project "AuthAPI/AuthAPI.csproj"                 -Port 7001
Start-DotNetService -Name "UserAPI"         -Project "UserAPI/UserAPI.csproj"                 -Port 7002
Start-DotNetService -Name "CvJdAPI"         -Project "CvJdAPI/CvJdAPI.csproj"                 -Port 7003
Start-DotNetService -Name "QuestionAPI"     -Project "QuestionAPI/QuestionAPI.csproj"         -Port 7004
Start-DotNetService -Name "InterviewAPI"    -Project "InterviewAPI/InterviewAPI.csproj"       -Port 7005
Start-DotNetService -Name "EvaluationAPI"   -Project "EvaluationAPI/EvaluationAPI.csproj"     -Port 7006
Start-DotNetService -Name "FeedbackAPI"     -Project "FeedbackAPI/FeedbackAPI.csproj"         -Port 7007
Start-DotNetService -Name "PaymentAPI"      -Project "PaymentAPI/PaymentAPI.csproj"           -Port 7008
Start-DotNetService -Name "NewsAPI"         -Project "NewsAPI/NewsAPI.csproj"                 -Port 7009
Start-DotNetService -Name "NotificationAPI" -Project "NotificationAPI/NotificationAPI.csproj" -Port 7010
Start-DotNetService -Name "StatisticsAPI"   -Project "StatisticsAPI/StatisticsAPI.csproj"     -Port 7011

Start-Sleep -Seconds 2

# --- API Gateway (phai chay sau cac service tren, nhung truoc WebClient) ---
Write-Host "Starting ApiGateway on port 7000..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd '$root'; `$host.UI.RawUI.WindowTitle = 'ApiGateway (port 7000)'; dotnet run --project source/ApiGateway/ApiGateway.csproj"
)

Start-Sleep -Seconds 2

# --- ai-service (Python/FastAPI) - can cho model load, co the mat 1-2 phut ---
Write-Host "Starting ai-service on port 8000..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd '$root\ai-service'; `$host.UI.RawUI.WindowTitle = 'ai-service (port 8000)'; .\.venv\Scripts\Activate.ps1; uvicorn app.main:app --host 0.0.0.0 --port 8000"
)

Start-Sleep -Seconds 2

# --- WebClient (MVC frontend) ---
Write-Host "Starting WebClient on port 5130..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd '$root'; `$host.UI.RawUI.WindowTitle = 'WebClient (port 5130)'; dotnet run --project source/WebClient/WebClient.csproj --urls http://localhost:5130"
)

Write-Host ""
Write-Host "=== Da mo tat ca cua so. Doi moi cua so hien 'Now listening on...' roi vao: ===" -ForegroundColor Green
Write-Host "  WebClient:  http://localhost:5130" -ForegroundColor Yellow
Write-Host "  ApiGateway: http://localhost:7000" -ForegroundColor Yellow
Write-Host "  ai-service: http://localhost:8000/docs (Swagger, load model co the mat vai phut)" -ForegroundColor Yellow