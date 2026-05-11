# Smart Shopping System - Start Here

## 🚀 Quick Start

### Easiest Way (Recommended)
```powershell
.\start_system.ps1
```
This automatically detects which camera is working and starts everything!

### Manual Start
```powershell
# With Iriun camera (if streaming from phone)
.\run_all.ps1 -ShowPreview

# With laptop camera (works immediately)
.\run_all.ps1 -ShowPreview -IriunCameraIndex 0
```

## ⚠️ Important: Iriun Camera Issue

**Problem:** When using Iriun camera (index 1), hand gestures, emotion detection, and face recognition don't work because the camera is not streaming.

**Solution:** Start the Iriun Webcam app on your phone BEFORE running the system.

### How to Fix Iriun Camera

1. **Open Iriun Webcam app on your phone**
2. **Wait for "Connected" status**
3. **Verify Iriun PC software is running** (check system tray)
4. **Ensure same Wi-Fi network** or use USB cable
5. **Run the system:**
   ```powershell
   .\start_system.ps1
   ```

**Can't get Iriun working?** Use laptop camera instead:
```powershell
.\start_system.ps1 -UseLaptopCamera
```

## 📋 System Requirements

- **Python 3.9** (installed and working)
- **All dependencies installed** (mediapipe, fer, face-recognition, opencv, etc.)
- **TuioDemo.exe built** (in bin/Debug or bin/Release)
- **Working camera** (laptop built-in OR Iriun from phone)

## 🔧 Diagnostic Tools

### Check System Health
```powershell
.\diagnose_system.ps1
```
Shows complete system status including cameras, dependencies, and files.

### Check Cameras
```powershell
.\run_all.ps1 -ListCameras
```
Lists all detected cameras with brightness levels.

### Monitor Iriun Camera
```powershell
.\test_iriun_camera.ps1
```
Continuously monitors Iriun camera status.

## 🎯 Features

The system provides three integrated features on a single camera:

### 1. Hand Gesture Tracking
- Swipe left/right to navigate pages
- Select gesture to click
- Clockwise/anticlockwise rotation
- Outputs TUIO messages to UDP port 3333

### 2. Face Emotion Detection
- Real-time emotion recognition
- 7 emotions: happy, sad, angry, surprise, fear, disgust, neutral
- UI adapts based on detected emotion
- Outputs to `.runtime/current_emotion.json`

### 3. Face Recognition with Gender
- Identifies enrolled users
- Retrieves gender from enrollment data
- Personalized content per user
- Outputs to `.runtime/face_detection.json`

## 👤 Enroll Users

Before face recognition works, you need to enroll users:

```powershell
# Enroll a male user
py -3.9 bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Ahmed" --gender male --show-preview --camera-index 0

# Enroll a female user
py -3.9 bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Sara" --gender female --show-preview --camera-index 0
```

**Note:** Use `--camera-index 0` for laptop camera or `--camera-index 1` for Iriun (if streaming).

## 📊 System Architecture

```
start_system.ps1 (Auto-detects camera)
    ↓
run_all.ps1 (Orchestrates all components)
    ├── Bluetooth Watcher (device detection)
    ├── iriun_combined.py (main bridge)
    │   ├── Hand Tracking → TUIO (UDP 3333)
    │   ├── Face Emotion → current_emotion.json
    │   └── Face Recognition → face_detection.json
    ├── reacTIVision (marker tracking)
    └── TuioDemo.exe (GUI application)
```

## 🎮 Usage

### Start the System
```powershell
# Auto-detect best camera
.\start_system.ps1

# Force laptop camera
.\start_system.ps1 -UseLaptopCamera

# Force Iriun camera
.\start_system.ps1 -ForceIriun
```

### What You Should See

**Console Output:**
```
>> Starting Bluetooth watcher...
>> Starting Iriun combined (hand + face)...
[camera] Index 0 is live.
Ready: hand TUIO + face emotion + face recognition all running on the same camera.
>> Starting reacTIVision...
>> Launching GUI: bin\Debug\TuioDemo.exe 3333
```

**Preview Window:**
- Live camera feed
- Green hand skeleton when you wave
- Emotion label: `emotion: happy (0.85)`
- Person name: `person: Ahmed (male)` (if enrolled)

**Runtime Files:**
```powershell
# Check emotion detection
Get-Content .runtime/current_emotion.json

# Check face recognition
Get-Content .runtime/face_detection.json

# Check TUIO configuration
Get-Content .runtime/tuio_port.json
```

## 🐛 Troubleshooting

### Issue: Iriun camera not working
**Symptoms:** Hand gestures, emotion, and face recognition don't work

**Solution:**
1. Start Iriun Webcam app on phone
2. Ensure "Connected" status
3. Check Iriun PC software is running
4. Use same Wi-Fi or USB connection

**Quick Fix:** Use laptop camera instead
```powershell
.\start_system.ps1 -UseLaptopCamera
```

### Issue: "ModuleNotFoundError: No module named 'mediapipe'"
**Solution:** Install dependencies
```powershell
py -3.9 -m pip install mediapipe==0.10.21 fer==22.4.0 face-recognition opencv-python "tensorflow<2.16" python-osc dollarpy
```

### Issue: "TuioDemo.exe not built"
**Solution:** Build the C# project in Visual Studio
1. Open `TUIO_DEMO.csproj` in Visual Studio
2. Build → Build Solution (or press F6)
3. Check `bin/Debug/TuioDemo.exe` exists

### Issue: Python version mismatch
**Check which Python is being used:**
```powershell
py -3.9 --version  # Should show Python 3.9.x
python --version   # Might show different version
```

**Solution:** The scripts use `py -3.9` to ensure Python 3.9 is used.

## 📚 Documentation

- **`FIX_IRIUN_CAMERA.md`** - Detailed Iriun camera troubleshooting
- **`SOLUTION_SUMMARY.md`** - Complete problem analysis
- **`TROUBLESHOOTING_GUIDE.md`** - Comprehensive troubleshooting
- **`QUICK_FIX.md`** - Quick reference guide
- **`README_CAMERA_FIX.md`** - Camera-specific fixes

## 🧪 Test Individual Components

### Test Hand Tracking Only
```powershell
py -3.9 bridge/hand_tuio_bridge.py --camera-index 0 --show-preview
```

### Test Face Emotion Only
```powershell
py -3.9 "bridge/Face emotion bridge.py" --camera-index 0 --show-preview --watch
```

### Test Face Recognition Only
```powershell
py -3.9 bridge/face_recognition_gender_bridge.py --camera-index 0 --show-preview --watch
```

### Test All Combined
```powershell
py -3.9 bridge/iriun_combined.py --camera-index 0 --show-preview
```

## ✅ Success Indicators

When everything works correctly:

✅ **Console shows:**
- "Ready: hand TUIO + face emotion + face recognition all running on the same camera."
- No error messages or timeouts

✅ **Preview window shows:**
- Live camera feed
- Green hand skeleton overlay
- Emotion label updating
- Person name and gender (if enrolled)

✅ **Runtime files update:**
- `.runtime/current_emotion.json` updates every 0.7 seconds
- `.runtime/face_detection.json` updates when face detected
- `.runtime/tuio_port.json` contains port configuration

✅ **TuioDemo GUI:**
- Responds to hand gestures
- UI adapts to emotions
- Shows personalized content

## 🎯 Quick Commands Reference

```powershell
# Start system (auto-detect camera)
.\start_system.ps1

# Start with laptop camera
.\start_system.ps1 -UseLaptopCamera

# Check system health
.\diagnose_system.ps1

# List cameras
.\run_all.ps1 -ListCameras

# Monitor Iriun status
.\test_iriun_camera.ps1

# Enroll user
py -3.9 bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Name" --gender male --camera-index 0 --show-preview

# Test combined bridge
py -3.9 bridge/iriun_combined.py --camera-index 0 --show-preview
```

## 💡 Tips

1. **USB is more stable than Wi-Fi** for Iriun
2. **Keep phone screen on** while using Iriun
3. **Good lighting improves** emotion and face recognition
4. **Enroll multiple samples** from different angles for better recognition
5. **Position camera** to see both hands and face
6. **Use laptop camera** for development/testing (always available)
7. **Use Iriun camera** for demos (better positioning)

## 🆘 Still Having Issues?

1. **Run diagnostics:**
   ```powershell
   .\diagnose_system.ps1
   ```

2. **Check documentation:**
   - Read `FIX_IRIUN_CAMERA.md` for camera issues
   - Read `TROUBLESHOOTING_GUIDE.md` for detailed help

3. **Use fallback:**
   ```powershell
   .\start_system.ps1 -UseLaptopCamera
   ```

4. **Test components individually** (see "Test Individual Components" section above)

## 🎉 Summary

Your system is **fully functional**! The main issue is that the Iriun camera needs to be actively streaming from your phone. If you can't get Iriun working, just use the laptop camera - all features work exactly the same way.

**Easiest way to start:**
```powershell
.\start_system.ps1
```

This automatically picks the best working camera and starts everything! 🚀
