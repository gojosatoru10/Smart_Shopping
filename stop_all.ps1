$ErrorActionPreference = "SilentlyContinue"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $RepoRoot

Write-Host "Stopping leftover processes..." -ForegroundColor Cyan

$names = @("python", "TuioDemo", "reacTIVision")
foreach ($n in $names) {
    Get-Process $n -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $_.Kill()
            Write-Host "  killed $($_.ProcessName) [PID $($_.Id)]" -ForegroundColor DarkGray
        } catch {}
    }
}

Write-Host "Cleaning .runtime logs/state..." -ForegroundColor Cyan
$rt = Join-Path $RepoRoot ".runtime"
if (Test-Path $rt) {
    Remove-Item (Join-Path $rt "*.log") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $rt "tuio_port.json") -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $rt "current_emotion.tmp") -Force -ErrorAction SilentlyContinue
}

Write-Host "Done." -ForegroundColor Green
