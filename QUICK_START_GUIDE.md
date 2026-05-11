# Quick Start Guide

## 🚀 Get Started in 3 Steps

### Step 1: Enroll Yourself
```bash
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "YourName" --gender male --show-preview --camera-index 0
```

### Step 2: Run the System
```powershell
.\run_all.ps1 -ShowPreview
```

### Step 3: Done!
Your system is now running with:
- ✅ Hand gesture navigation
- ✅ Emotion detection
- ✅ Face recognition
- ✅ Gender-based personalization

## 📊 What You'll See

### In the Preview Window:
- Green circles on your index fingers (hand tracking)
- Yellow text: "emotion: happy (0.85)"
- Green text: "person: YourName (male)"

### Output Files Created:
- `.runtime/current_emotion.json` - Your current emotion
- `.runtime/face_detection.json` - Your identity + gender
- `.runtime/tuio_port.json` - TUIO configuration

## 🎯 Use Cases

### For Your C# Application:

```csharp
// Read emotion
var emotion = ReadJson(".runtime/current_emotion.json");
// Use: emotion.emotion, emotion.adaptive_hint

// Read person + gender
var face = ReadJson(".runtime/face_detection.json");
// Use: face.person_identity, face.gender

// Personalize UI
if (face.person_identity != "unknown") {
    ShowGreeting($"Welcome, {face.person_identity}!");
    
    if (face.gender == "male") {
        ShowMaleProducts();
    } else if (face.gender == "female") {
        ShowFemaleProducts();
    }
}

// Adapt based on emotion
if (emotion.adaptive_hint == "frustrated") {
    SimplifyUI();
} else if (emotion.adaptive_hint == "engaged") {
    ShowMoreOptions();
}
```

## 🔧 Common Commands

### Enroll More People
```bash
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Sara" --gender female --show-preview --camera-index 0
```

### List Available Cameras
```bash
python bridge/iriun_combined.py --list-cameras --use-dshow
```

### Run Without Preview
```powershell
.\run_all.ps1
```

### Test Just the Combined Bridge
```bash
python bridge/iriun_combined.py --camera-index 1 --tuio-port 3333 --show-preview
```

## 📝 Notes

- **Enrollment**: Use camera index 0 (laptop camera)
- **Running**: Use camera index 1 (Iriun camera)
- **Gender**: Always 100% accurate (from enrollment data)
- **Emotion**: Updates every 0.7 seconds
- **Hand tracking**: Real-time at 30 FPS

## ❓ Troubleshooting

### "Camera not found"
```bash
python bridge/iriun_combined.py --list-cameras --use-dshow
```

### "Person not recognized"
Re-enroll with more samples:
```bash
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Name" --gender male --samples 10 --show-preview
```

### "Module not found"
Install dependencies:
```bash
pip install face-recognition opencv-python numpy Pillow mediapipe fer tensorflow
```

## 🎉 That's It!

You're ready to use your smart shopping system with full personalization!
