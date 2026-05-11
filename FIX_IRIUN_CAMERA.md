# Fix: Iriun Camera Not Working

## Problem
When you run the system with Iriun camera, hand gestures, emotion detection, and face recognition don't work because the Iriun camera is not streaming.

## Quick Solution

### Option 1: Use Laptop Camera (Works Immediately)
```powershell
.\start_system.ps1 -UseLaptopCamera
```
or
```powershell
.\run_all.ps1 -ShowPreview -IriunCameraIndex 0
```

### Option 2: Fix Iriun Camera (Better Quality)

**Step 1: Start Iriun on Your Phone**
1. Open **Iriun Webcam** app on your phone
2. Wait until it shows **"Connected"** status
3. You should see the camera preview on your phone

**Step 2: Verify Iriun PC Software**
1. Check Windows system tray (bottom-right) for Iriun icon
2. If not running, start **"Iriun Webcam"** from Start menu
3. The PC software must be running

**Step 3: Ensure Connection**
- **Wi-Fi**: Phone and PC must be on the **same network**
- **USB**: Connect phone to PC via USB cable (more stable)

**Step 4: Test Camera**
```powershell
python bridge/iriun_combined.py --list-cameras --use-dshow
```

Look for:
```
  [1] OK  640x480  mean=120.0  live    # Brightness should be > 50
```

**Step 5: Run System**
```powershell
.\start_system.ps1
```
This will automatically detect which camera is working and use it.

## Easy Commands

```powershell
# Auto-detect and use best camera
.\start_system.ps1

# Force laptop camera
.\start_system.ps1 -UseLaptopCamera

# Force Iriun camera (will wait if not ready)
.\start_system.ps1 -ForceIriun

# Check camera status
python bridge/iriun_combined.py --list-cameras --use-dshow

# Run diagnostics
.\diagnose_system.ps1
```

## Why Iriun Doesn't Work

Common reasons:
1. **Iriun app not running on phone** - Open the app
2. **Different Wi-Fi networks** - Connect both to same network
3. **Iriun PC software not running** - Start from Start menu
4. **Firewall blocking** - Allow Iriun in Windows Firewall
5. **Phone in power-saving mode** - Disable temporarily

## Troubleshooting

### Issue: "Searching for server"
**Fix:**
- Restart Iriun PC software
- Restart Iriun app on phone
- Try USB connection instead

### Issue: Camera shows but is black
**Fix:**
- Close other apps using camera (Zoom, Teams, Skype)
- Restart both Iriun app and PC software
- Restart your phone

### Issue: Frequent disconnections
**Fix:**
- Use USB cable instead of Wi-Fi
- Keep phone screen on
- Disable battery optimization for Iriun app

## What Works with Each Camera

### Laptop Camera (Index 0)
✅ Hand gesture tracking
✅ Face emotion detection
✅ Face recognition with gender
✅ Always available
⚠️ Limited positioning (fixed to laptop)

### Iriun Camera (Index 1)
✅ Hand gesture tracking
✅ Face emotion detection
✅ Face recognition with gender
✅ Better positioning (phone is movable)
✅ Better angle for hand tracking
⚠️ Requires phone app streaming

## Recommended Setup

**For Development/Testing:**
Use laptop camera - it's always available
```powershell
.\start_system.ps1 -UseLaptopCamera
```

**For Demo/Production:**
Use Iriun camera - better positioning and quality
```powershell
# Start Iriun app on phone first, then:
.\start_system.ps1
```

## System Architecture

```
start_system.ps1 (Auto-detects camera)
    ↓
run_all.ps1 (Starts all components)
    ↓
bridge/iriun_combined.py (Single camera for all features)
    ├── Hand Tracking → TUIO messages
    ├── Face Emotion → .runtime/current_emotion.json
    └── Face Recognition → .runtime/face_detection.json
```

## Success Indicators

When working correctly:

✅ **Console shows:**
```
[camera] Index X is live.
Ready: hand TUIO + face emotion + face recognition all running on the same camera.
```

✅ **Preview window shows:**
- Live camera feed
- Green hand skeleton when you wave
- Emotion label: `emotion: happy (0.85)`
- Person name: `person: Ahmed (male)` (if enrolled)

✅ **Runtime files update:**
```powershell
Get-Content .runtime/current_emotion.json
Get-Content .runtime/face_detection.json
```

## Quick Test

Test if everything works:
```powershell
# Test with laptop camera
python bridge/iriun_combined.py --camera-index 0 --show-preview

# Test with Iriun camera (if streaming)
python bridge/iriun_combined.py --camera-index 1 --show-preview --wait-for-camera
```

Wave your hand and show your face. You should see:
- Green hand skeleton overlay
- Emotion label updating
- Your name if enrolled

Press 'q' to quit.

## Still Not Working?

1. **Check Python installation:**
   ```powershell
   python --version
   ```
   Should show Python 3.9 or higher

2. **Check dependencies:**
   ```powershell
   python -m pip list | Select-String "mediapipe|fer|face-recognition|opencv"
   ```

3. **Run full diagnostics:**
   ```powershell
   .\diagnose_system.ps1
   ```

4. **Use fallback:**
   ```powershell
   .\start_system.ps1 -UseLaptopCamera
   ```

## Summary

**The system is fully functional!** The only issue is that Iriun camera needs to be actively streaming from your phone. If you can't get Iriun working right now, just use the laptop camera - all features work the same way.

**Easiest solution:**
```powershell
.\start_system.ps1
```
This automatically picks the best working camera!
