# Quick Start: Face Recognition & Gender Detection

## Installation

```bash
# Install dependencies
python -m pip install -r requirements-face-recognition.txt
```

## Usage

### 1. List Available Cameras

```bash
python bridge/face_recognition_gender_bridge.py --list-cameras
```

### 2. Enroll People

```bash
# Enroll Ahmed
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Ahmed" --show-preview

# Enroll Youssef
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Youssef" --show-preview
```

**Tips during enrollment:**
- Move your head to different angles (left, right, up, down)
- The system captures 5 samples automatically
- Press 'q' to quit early

### 3. Run Detection

```bash
# Continuous detection with preview
python bridge/face_recognition_gender_bridge.py --watch --show-preview

# Background mode (no preview)
python bridge/face_recognition_gender_bridge.py --watch
```

### 4. Check Output

```bash
# View detection results
cat .runtime/current_user.json
```

Example output:
```json
{
  "person_identity": "Ahmed",
  "recognition_confidence": 0.85,
  "gender": "male",
  "gender_confidence": 0.92,
  "face_detected": true,
  "timestamp": "2026-05-09T10:35:42.123Z"
}
```

## Troubleshooting

### Camera not opening?
```bash
# Try different camera index
python bridge/face_recognition_gender_bridge.py --camera-index 1 --watch
```

### Gender detection disabled?
```bash
# Install DeepFace
python -m pip install deepface
```

### Poor recognition accuracy?
- Enroll with more samples: `--samples 10`
- Ensure good lighting
- Re-enroll if appearance changes

## Next Steps

- See `bridge/README_FACE_RECOGNITION.md` for full documentation
- Integrate with C# application by reading `.runtime/current_user.json`
- Future: Integrate into `iriun_combined.py` to share camera with other bridges
