param(
    [string]$PythonExe = "python",
    [int]$IriunCameraIndex = 1,
    [int]$TuioPort = 3333,
    [switch]$ShowPreview = $true,
    [switch]$SkipReacTIVision,
    [switch]$SkipBluetooth,
    [switch]$SkipFaceHand
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $RepoRoot

$RuntimeDir = Join-Path $RepoRoot ".runtime"
New-Item -ItemType Directory -Force -Path $RuntimeDir | Out-Null

$Procs = @()

function Start-Bg {
    param(
        [string]$Title,
        [string]$File,
        [string[]]$ArgList = @()
    )
    Write-Host ">> Starting $Title..." -ForegroundColor Cyan
    if ($ArgList -and $ArgList.Count -gt 0) {
        $p = Start-Process -FilePath $File -ArgumentList $ArgList -PassThru -WindowStyle Minimized
    } else {
        $p = Start-Process -FilePath $File -PassThru -WindowStyle Minimized
    }
    $script:Procs += [pscustomobject]@{ Name = $Title; Proc = $p }
    return $p
}

if (-not $SkipBluetooth) {
    $btScript = Join-Path $RepoRoot "Pybluez2 Bluetooth.py"
    if (Test-Path $btScript) {
        Start-Bg -Title "Bluetooth watcher" -File $PythonExe -ArgList @("`"$btScript`"", "--watch") | Out-Null
    } else {
        Write-Host "Bluetooth script not found, skipped." -ForegroundColor Yellow
    }
}

if (-not $SkipFaceHand) {
    $combined = Join-Path $RepoRoot "bridge\iriun_combined.py"
    if (-not (Test-Path $combined)) {
        throw "Missing $combined. Cannot run combined hand + face bridge."
    }
    $combinedArgs = @(
        "`"$combined`"",
        "--camera-index", "$IriunCameraIndex",
        "--tuio-port",   "$TuioPort",
        "--wait-for-camera",
        "--use-dshow"
    )
    if ($ShowPreview) { $combinedArgs += "--show-preview" }
    Start-Bg -Title "Iriun combined (hand + face)" -File $PythonExe -ArgList $combinedArgs | Out-Null
}

if (-not $SkipReacTIVision) {
    $reactivision = Join-Path $RepoRoot "reacTIVision-1.5.1-win64\reacTIVision-1.5.1-win64\reacTIVision.exe"
    if (Test-Path $reactivision) {
        Start-Bg -Title "reacTIVision" -File $reactivision | Out-Null
    } else {
        Write-Host "reacTIVision.exe not found, skipped." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Waiting briefly so background services initialize..." -ForegroundColor Cyan
Start-Sleep -Seconds 4

$gui = $null
$candidates = @(
    (Join-Path $RepoRoot "bin\Debug\TuioDemo.exe"),
    (Join-Path $RepoRoot "bin\Release\TuioDemo.exe")
)
foreach ($c in $candidates) {
    if (Test-Path $c) { $gui = $c; break }
}

if (-not $gui) {
    Write-Host "TuioDemo.exe not built. Build TUIO_DEMO.csproj first." -ForegroundColor Red
} else {
    Write-Host ">> Launching GUI: $gui $TuioPort" -ForegroundColor Green
    $guiProc = Start-Process -FilePath $gui -ArgumentList @("$TuioPort") -PassThru
    Wait-Process -Id $guiProc.Id
}

Write-Host ""
Write-Host "GUI exited. Stopping background processes..." -ForegroundColor Cyan
foreach ($entry in $Procs) {
    try {
        if (-not $entry.Proc.HasExited) {
            Stop-Process -Id $entry.Proc.Id -Force -ErrorAction SilentlyContinue
            Write-Host "  stopped: $($entry.Name)" -ForegroundColor DarkGray
        }
    } catch {}
}
Write-Host "All done." -ForegroundColor Green
