# Camera Preview Issue - Fixed

## Problem
When running `run_all.ps1 -ShowPreview`, all Python processes showed status "Completed" immediately instead of running continuously. No camera preview windows appeared even though the Iriun camera was working.

## Root Causes

### 1. **Path Spaces Not Quoted**
The workspace path contains spaces: `D:\Year 4 Term 2 CS\Smart_Shopping`

When `Start-Process` passed unquoted paths to Python, it interpreted them as:
```
python D:\Year 4 Term 2 CS\Smart_Shopping\bridge\script.py
         ^^^^^ ^^^^^ ^^^^^ ^^^ (treated as separate arguments)
```

**Error:** `can't open file 'D:\Year': [Errno 2] No such file or directory`

### 2. **Missing --use-dshow Flag**
The `face_recognition_gender_bridge.py` script didn't support the `--use-dshow` argument that was being passed from `run_all.ps1`.

**Error:** `unrecognized arguments: --use-dshow`

### 3. **Missing --watch Flag**
The face recognition script wasn't being run in continuous watch mode, so it would process once and exit.

### 4. **Process Window Mode**
Using `-NoNewWindow` with output redirection can cause processes to exit prematurely on Windows. Changed to `-WindowStyle Hidden` for better stability.

## Solutions Applied

### 1. Fixed Path Quoting in run_all.ps1
```powershell
# Before
$argList = @($PythonVersion, $Script) + $ScriptArgs

# After
$argList = @($PythonVersion, "`"$Script`"") + $ScriptArgs
```

### 2. Added --use-dshow Support to face_recognition_gender_bridge.py
```python
# Added to parse_args()
parser.add_argument("--use-dshow", action="store_true",
                   help="Windows: use DirectShow backend (more stable for virtual webcams like Iriun)")

# Updated camera opening code
if args.use_dshow:
    cap = cv2.VideoCapture(args.camera_index, cv2.CAP_DSHOW)
else:
    cap = cv2.VideoCapture(args.camera_index)
```

### 3. Added --watch Flag to Face Recognition Launch
```powershell
$faceArgs = @(
    "--camera-index", "$IriunCameraIndex",
    "--use-dshow",
    "--watch"  # Added this
)
```

### 4. Changed Process Window Mode
```powershell
# Before
-NoNewWindow

# After
-WindowStyle Hidden
```

### 5. Improved Error Handling
Added null checks when reading log files to prevent PowerShell errors:
```powershell
$logContent = Get-Content $entry.LogFile -Raw -ErrorAction SilentlyContinue
if ($logContent -and $logContent.Trim()) {
    # Display content
}
```

## Verification

After fixes, all processes run successfully:

```
Background services status:
  Bluetooth watcher: Running ✓
  Iriun combined (hand + emotion): Running ✓
  Face recognition + gender: Running ✓
```

### Log Output Confirms Success

**Iriun Combined:**
```
[camera] Index 1 is live.
TUIO_HOST=127.0.0.1
TUIO_PORT=3333
Ready: hand TUIO + face emotion + face recognition all running on the same camera.
```

**Face Recognition:**
```
[UNKNOWN] recognition=0.00 | [UNKNOWN] gender=0.00
[NO FACE]
```
(Shows it's actively detecting - "UNKNOWN" means no enrolled faces, "NO FACE" means no face in frame)

## How to Use

### Normal Operation (with preview)
```powershell
.\run_all.ps1 -ShowPreview
```

### Without Preview (headless)
```powershell
.\run_all.ps1
```

### Skip Specific Components
```powershell
.\run_all.ps1 -ShowPreview -SkipReacTIVision
.\run_all.ps1 -ShowPreview -SkipBluetooth
```

### List Available Cameras
```powershell
.\run_all.ps1 -ListCameras
```

## Camera Preview Windows

When `-ShowPreview` is used, you should see:

1. **Iriun Combined Window**: Shows hand tracking skeleton + emotion + face recognition overlays
2. **Face Recognition Window**: Shows face bounding boxes with person identity and gender

Both windows process the same Iriun camera feed (index 1) using DirectShow backend for stability.

## Notes

- The camera preview windows will appear in **hidden/background mode** because processes use `-WindowStyle Hidden`
- To see the preview, you may need to check your taskbar or alt-tab to find the OpenCV windows
- Press 'q' in any preview window to stop that specific process
- The GUI (TuioDemo.exe) will launch after all background services initialize
- When you close the GUI, all background processes are automatically stopped

## Troubleshooting

If processes still fail:

1. **Check log files** in `.runtime/` folder:
   - `Bluetooth_watcher.log`
   - `Iriun_combined__hand___emotion_.log`
   - `Face_recognition___gender.log`

2. **Verify camera availability**:
   ```powershell
   .\run_all.ps1 -ListCameras
   ```

3. **Test individual scripts**:
   ```powershell
   py -3.9 "bridge\iriun_combined.py" --camera-index 1 --use-dshow --show-preview
   py -3.9 "bridge\face_recognition_gender_bridge.py" --camera-index 1 --use-dshow --watch --show-preview
   ```

4. **Ensure Iriun webcam is running** before starting the scripts
