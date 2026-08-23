<#
.SYNOPSIS
    Renders every diagrams/*.mmd file in this folder to an .svg using mermaid-cli (mmdc).

.DESCRIPTION
    This script is written for reference only, per the report mandate ("Write the script; DO NOT
    run it and DO NOT install mermaid-cli"). It has not been executed as part of producing this
    report. Diagrams are validated visually by their inline ```mermaid``` fences in the chapter
    markdown files, which render natively wherever the report is viewed.

    Requires Node.js and @mermaid-js/mermaid-cli (mmdc) to be installed separately:
        npm install -g @mermaid-js/mermaid-cli

.NOTES
    ASCII-only labels and no parentheses in node/participant labels are assumed by the .mmd
    sources in this folder, per the report's diagram-hygiene rule — no custom mermaid theme
    config is applied here so the default renderer output is used as-is.
#>

param(
    [string]$SourceDir = $PSScriptRoot,
    [string]$OutDir = (Join-Path $PSScriptRoot "..\out"),
    [string]$Theme = "default"
)

if (-not (Get-Command mmdc -ErrorAction SilentlyContinue)) {
    Write-Error "mmdc (mermaid-cli) was not found on PATH. Install it with: npm install -g @mermaid-js/mermaid-cli"
    exit 1
}

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
}

$mmdFiles = Get-ChildItem -Path $SourceDir -Filter "*.mmd" -File

if ($mmdFiles.Count -eq 0) {
    Write-Warning "No .mmd files found in $SourceDir"
    exit 0
}

foreach ($file in $mmdFiles) {
    $outFile = Join-Path $OutDir ($file.BaseName + ".svg")
    Write-Host "Rendering $($file.Name) -> $outFile"
    mmdc -i $file.FullName -o $outFile -t $Theme -b transparent
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "mmdc failed for $($file.Name) (exit code $LASTEXITCODE)"
    }
}

Write-Host "Done. Rendered $($mmdFiles.Count) diagram(s) to $OutDir"
