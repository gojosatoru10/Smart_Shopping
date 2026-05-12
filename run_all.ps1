param(
    [string]$PythonExe     = "py",
    [string]$PythonVersion = "-3.9",   # passed as first arg when using the 'py' launcher
    [int]$IriunCameraIndex = 1,
    [int]$TuioPort         = 3333,
    [switch]$ShowPreview   = $true,
    [switch]$SkipReacTIVision,
    [switch]$SkipBluetooth,
    [switch]$SkipFaceHand,
    [switch]$ListCameras   # Run camera probe only, then exit
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $RepoRoot

# ── Build the Python argument prefix ─────────────────────────────────────────
# When using the 'py' launcher, prepend the version flag (e.g. -3.9).
# When pointing directly at python.exe, leave the prefix empty.
$PyPrefix = @()
if ($PythonExe -eq "py" -and $PythonVersion -ne "") {
    $PyPrefix = @($PythonVersion)
}

# Helper: run a Python script as a background process
function Start-PyBg {
    param(
        [string]$Title,
        [string]$Script,
        [string[]]$ScriptArgs = @()
    )
    $argList = $PyPrefix + @("`"$Script`"") + $ScriptArgs
    Write-Host ">> Starting $Title..." -ForegroundColor Cyan
    # Changed from Minimized to Normal so you can see console output
    $p = Start-Process -FilePath $PythonExe -ArgumentList $argList -PassThru -WindowStyle Normal
    $script:Procs += [pscustomobject]@{ Name = $Title; Proc = $p }
    return $p
}

# ── Camera probe mode ─────────────────────────────────────────────────────────
if ($ListCameras) {
    Write-Host "Probing available cameras..." -ForegroundColor Cyan
    $combined = Join-Path $RepoRoot "bridge\iriun_combined.py"
    $argList  = $PyPrefix + @("`"$combined`"", "--list-cameras", "--use-dshow")
    & $PythonExe @argList
    exit 0
}

$RuntimeDir = Join-Path $RepoRoot ".runtime"
New-Item -ItemType Directory -Force -Path $RuntimeDir | Out-Null

$Procs = @()

if (-not $SkipBluetooth) {
    $btScript = Join-Path $RepoRoot "Pybluez2 Bluetooth.py"
    if (Test-Path $btScript) {
        Start-PyBg -Title "Bluetooth watcher" -Script $btScript -ScriptArgs @("--watch") | Out-Null
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
        "--camera-index",    "$IriunCameraIndex",
        "--tuio-port",       "$TuioPort",
        "--wait-for-camera",
        "--wait-timeout-sec","15",
        "--use-dshow",
        "--recognition-interval", "0.5"
    )
    if ($ShowPreview) { $combinedArgs += "--show-preview" }
    Start-PyBg -Title "Iriun combined (hand + face)" -Script $combined -ScriptArgs $combinedArgs | Out-Null
}

if (-not $SkipReacTIVision) {
    $reactivision = Join-Path $RepoRoot "reacTIVision-1.5.1-win64\reacTIVision-1.5.1-win64\reacTIVision.exe"
    if (Test-Path $reactivision) {
        Write-Host ">> Starting reacTIVision..." -ForegroundColor Cyan
        $p = Start-Process -FilePath $reactivision -PassThru -WindowStyle Minimized
        $Procs += [pscustomobject]@{ Name = "reacTIVision"; Proc = $p }
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
