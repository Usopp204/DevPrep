<#
.SYNOPSIS
    Rebuilds the containers changed by the CV-upload permission fix, then proves the fix landed.

.DESCRIPTION
    Uploading a CV failed with:

        Error: Access to the path '/app/uploads' is denied.

    Cause: CvJdAPI's Dockerfile sets WORKDIR /app, which creates that directory owned by
    root with mode 755. The COPY that follows uses --chown=appuser:appgroup, but --chown
    only retitles the files being copied IN — never the directory itself. The container
    then runs as appuser (uid 999), so CvEndpoints' Directory.CreateDirectory(".../uploads/cvs")
    had no permission to create the folder.

    The fix is two coordinated changes, and neither works alone:

      1. source/CvJdAPI/Dockerfile  — creates /app/uploads/cvs owned by appuser at build time.
      2. infra/docker-compose.yml   — mounts the cv-uploads named volume at /app/uploads so
                                      uploaded files survive the next rebuild.

    They are coupled because Docker seeds an empty named volume by copying the image's
    content AND ownership from the mount path. Add the volume without the Dockerfile change
    and the volume comes up owned by root, failing in exactly the same way.

    Because this is an image change, `docker compose restart` is NOT enough — the image has
    to be rebuilt. That is the step people miss, and why this script exists.

.PARAMETER SkipPull
    Rebuild whatever is already checked out instead of pulling origin/dev first.

.EXAMPLE
    cd infra
    .\apply-cv-upload-fix.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipPull
)

$ErrorActionPreference = 'Stop'

function Say  { param($m) Write-Host "[cv-fix] $m" }
function Good { param($m) Write-Host "[cv-fix] $m" -ForegroundColor Green }
function Bad  { param($m) Write-Host "[cv-fix] $m" -ForegroundColor Red }

$infra = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo  = Split-Path -Parent $infra
Set-Location $infra

# --- Preconditions ----------------------------------------------------------------
if (-not (Test-Path (Join-Path $infra 'docker-compose.yml'))) {
    Bad "Khong tim thay docker-compose.yml. Chay script nay tu thu muc infra/."; exit 1
}
if (-not (Test-Path (Join-Path $infra '.env.docker'))) {
    Bad ".env.docker khong ton tai. File nay khong nam trong git - xin cua nguoi trong nhom,"
    Bad "hoac copy tu .env.docker.example roi dien gia tri that."
    exit 1
}
try { docker info --format '{{.ServerVersion}}' | Out-Null }
catch { Bad "Docker chua chay. Mo Docker Desktop roi chay lai."; exit 1 }

# --- 1. Lay code moi --------------------------------------------------------------
if ($SkipPull) {
    Say "Bo qua git pull theo yeu cau."
} else {
    Say "Lay code moi tu origin/dev..."
    Push-Location $repo
    $dirty = git status --porcelain --untracked-files=no
    if ($dirty) {
        Say "Ban dang co thay doi chua commit - git se tu stash truoc khi pull."
        git stash push -m "truoc khi chay apply-cv-upload-fix" | Out-Null
        $stashed = $true
    }
    git pull --ff-only origin dev
    if ($stashed) { Say "Tra lai thay doi da stash."; git stash pop }
    Pop-Location
}

# --- 2. Build lai anh -------------------------------------------------------------
# webclient nam trong danh sach vi cung dot sua co them mot dong ghi chu vao trang
# CV & JD; no khong lien quan toi loi quyen ghi file.
Say "Build lai cvjdapi va webclient (co the mat vai phut)..."
docker compose --env-file .env.docker up -d --build cvjdapi webclient
if ($LASTEXITCODE -ne 0) { Bad "docker compose that bai."; exit 1 }

Say "Cho container san sang..."
Start-Sleep -Seconds 12

# --- 3. Kiem chung ----------------------------------------------------------------
Say "Kiem tra ket qua:"
$fail = 0

$health = (docker inspect -f '{{.State.Health.Status}}' devprep-cvjd 2>$null)
if ($health -eq 'healthy') { Good "  container devprep-cvjd: healthy" }
else { Bad "  container devprep-cvjd: $health"; $fail++ }

# Quyen so huu thu muc - phai la appuser, khong duoc la root.
$owner = (docker exec devprep-cvjd stat -c '%U' /app/uploads 2>$null)
if ($owner -eq 'appuser') { Good "  /app/uploads thuoc appuser" }
else { Bad "  /app/uploads thuoc '$owner' (phai la appuser)"; $fail++ }

# Phep thu quan trong nhat: ghi that bang chinh user cua tien trinh.
docker exec devprep-cvjd sh -c 'touch /app/uploads/cvs/.probe && rm /app/uploads/cvs/.probe' 2>$null
if ($LASTEXITCODE -eq 0) { Good "  ghi thu vao /app/uploads/cvs: thanh cong" }
else { Bad "  ghi thu vao /app/uploads/cvs: VAN BI TU CHOI"; $fail++ }

# Volume phai ton tai, neu khong file CV se mat sau lan build sau.
$vol = docker volume ls --format '{{.Name}}' | Select-String -Pattern 'cv-uploads' -Quiet
if ($vol) { Good "  volume cv-uploads da tao" }
else { Bad "  khong thay volume cv-uploads - file CV se mat khi build lai"; $fail++ }

Write-Host ''
if ($fail -eq 0) {
    Good "XONG. Vao trang CV & JD upload thu mot file CV that de xac nhan."
    exit 0
} else {
    Bad "CON $fail MUC CHUA DAT."
    Bad "Thu buoc nay roi chay lai script:"
    Bad "    docker compose --env-file .env.docker build --no-cache cvjdapi"
    exit 1
}
