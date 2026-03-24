param(
    [string]$PythonExe = "py",
    [string]$PythonVersion = "-3.9",
    [string]$MSBuildExe = "",
    [int]$CameraIndex = 1,
    [string]$TuioPort = "3333",
    [switch]$ShowPreview,
    [switch]$NoGesture,
    [switch]$SkipReacTIVision,
    [float]$SendFps = 30.0
)

$ErrorActionPreference = "Stop"

function Resolve-MSBuildPath {
    if ($MSBuildExe -and (Test-Path $MSBuildExe)) {
        return $MSBuildExe
    }

    $msbuildCmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($msbuildCmd) {
        return $msbuildCmd.Source
    }

    $vswhereCandidates = @(
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
        "$env:ProgramFiles\Microsoft Visual Studio\Installer\vswhere.exe"
    )

    $vswhere = $vswhereCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $vswhere) {
        return $null
    }

    $installPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if (-not $installPath) {
        $installPath = & $vswhere -latest -products * -property installationPath
    }

    if (-not $installPath) {
        return $null
    }

    $installPath = $installPath.Trim()

    $possibleMsbuild = @(
        (Join-Path $installPath "MSBuild\Current\Bin\MSBuild.exe"),
        (Join-Path $installPath "MSBuild\15.0\Bin\MSBuild.exe"),
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    )

    $found = $possibleMsbuild | Where-Object { Test-Path $_ } | Select-Object -First 1
    return $found
}

$BridgeDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $BridgeDir "..")
$PortFile = Join-Path $RepoRoot ".runtime/tuio_port.json"
$CurrentUserFile = Join-Path $RepoRoot ".runtime/current_user.json"
$BluetoothCacheFile = Join-Path $RepoRoot ".runtime/bluetooth_seen_cache.json"
$BluetoothStdout = Join-Path $RepoRoot ".runtime/bluetooth_stdout.log"
$BluetoothStderr = Join-Path $RepoRoot ".runtime/bluetooth_stderr.log"
$BluetoothScript = Join-Path $RepoRoot "Pybluez2 Bluetooth.py"
$BridgeStdout = Join-Path $RepoRoot ".runtime/bridge_stdout.log"
$BridgeStderr = Join-Path $RepoRoot ".runtime/bridge_stderr.log"
$BridgeScript = Join-Path $RepoRoot "bridge/hand_tuio_bridge.py"

if (Test-Path $PortFile) {
    Remove-Item $PortFile -Force
}
if (Test-Path $BridgeStdout) {
    Remove-Item $BridgeStdout -Force
}
if (Test-Path $BridgeStderr) {
    Remove-Item $BridgeStderr -Force
}
if (Test-Path $BluetoothStdout) {
    Remove-Item $BluetoothStdout -Force
}
if (Test-Path $BluetoothStderr) {
    Remove-Item $BluetoothStderr -Force
}

$bluetoothProc = $null
if (Test-Path $BluetoothScript) {
    Write-Host "Starting Bluetooth sign-in watcher..."
    $bluetoothArgString = ('{0} "{1}" --watch --db-path "{2}" --output-file "{3}" --cache-file "{4}" --interval 1.5' -f $PythonVersion, $BluetoothScript, (Join-Path $RepoRoot "devices_db.json"), $CurrentUserFile, $BluetoothCacheFile)
    $bluetoothProc = Start-Process -FilePath $PythonExe -ArgumentList $bluetoothArgString -RedirectStandardOutput $BluetoothStdout -RedirectStandardError $BluetoothStderr -PassThru
}

$reactivisionProc = $null
$ReacTIVisionExe = Join-Path $RepoRoot "reacTIVision-1.5.1-win64/reacTIVision-1.5.1-win64/reacTIVision.exe"
if (-not $SkipReacTIVision -and (Test-Path $ReacTIVisionExe)) {
    Write-Host "Starting reacTIVision (camera 0, sends /tuio/2Dobj to port 3333)..."
    $reactivisionProc = Start-Process -FilePath $ReacTIVisionExe -PassThru
    Start-Sleep -Milliseconds 2000
}

$bridgeArgs = @(
    $PythonVersion,
    $BridgeScript,
    "--camera-index", $CameraIndex,
    "--tuio-port", $TuioPort,
    "--port-file", $PortFile,
    "--send-fps", $SendFps
)

if ($ShowPreview) {
    $bridgeArgs += "--show-preview"
}
if ($NoGesture) {
    $bridgeArgs += "--no-gesture"
}

Write-Host "Starting Python bridge service..."
$bridgeProc = Start-Process -FilePath $PythonExe -ArgumentList $bridgeArgs -RedirectStandardOutput $BridgeStdout -RedirectStandardError $BridgeStderr -PassThru

try {
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        if ($bridgeProc.HasExited) {
            break
        }
        if (Test-Path $PortFile) {
            break
        }
        Start-Sleep -Milliseconds 200
    }

    if (-not (Test-Path $PortFile)) {
        $stderrTail = ""
        if (Test-Path $BridgeStderr) {
            $stderrTail = (Get-Content $BridgeStderr -Tail 40) -join "`n"
        }
        if ($bridgeProc.HasExited) {
            throw "Bridge exited early with code $($bridgeProc.ExitCode). See $BridgeStderr`n$stderrTail"
        }
        throw "Bridge did not become ready (no port file). See $BridgeStderr`n$stderrTail"
    }

    Start-Sleep -Milliseconds 500
    if ($bridgeProc.HasExited) {
        $stderrTail = ""
        if (Test-Path $BridgeStderr) {
            $stderrTail = (Get-Content $BridgeStderr -Tail 40) -join "`n"
        }
        throw "Bridge exited after startup with code $($bridgeProc.ExitCode). See $BridgeStderr`n$stderrTail"
    }

    $portInfo = Get-Content $PortFile -Raw | ConvertFrom-Json
    $port = [int]$portInfo.port

    Write-Host "Bridge sending TUIO to port: $port"
    Write-Host "reacTIVision sends to port 3333 (default). TuioDemo listens on $port."

    $demoExeCandidates = @(
        (Join-Path $RepoRoot "bin/Debug/TuioDemo.exe"),
        (Join-Path $RepoRoot "bin/Release/TuioDemo.exe")
    )

    $demoExe = $demoExeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $demoExe) {
        Write-Host "TuioDemo.exe not found. Building Debug target..."
        $msbuildPath = Resolve-MSBuildPath
        if (-not $msbuildPath) {
            throw "MSBuild not found. Install Visual Studio Build Tools with MSBuild, or build TUIO_DEMO.csproj manually, then rerun."
        }

        Write-Host "Using MSBuild: $msbuildPath"
        & $msbuildPath (Join-Path $RepoRoot "TUIO_DEMO.csproj") /p:Configuration=Debug /nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }

        $demoExe = Join-Path $RepoRoot "bin/Debug/TuioDemo.exe"
        if (-not (Test-Path $demoExe)) {
            throw "Build completed but TuioDemo.exe was not found at $demoExe"
        }
    }

    Write-Host "Starting C# TuioDemo on port $port ..."
    $demoProc = Start-Process -FilePath $demoExe -ArgumentList @($port) -PassThru
    Wait-Process -Id $demoProc.Id
}
finally {
    if ($bridgeProc -and -not $bridgeProc.HasExited) {
        Write-Host "Stopping Python bridge service..."
        Stop-Process -Id $bridgeProc.Id -Force
    }
    if ($bluetoothProc -and -not $bluetoothProc.HasExited) {
        Write-Host "Stopping Bluetooth sign-in watcher..."
        Stop-Process -Id $bluetoothProc.Id -Force
    }
    if ($reactivisionProc -and -not $reactivisionProc.HasExited) {
        Write-Host "Stopping reacTIVision..."
        Stop-Process -Id $reactivisionProc.Id -Force
    }
}
