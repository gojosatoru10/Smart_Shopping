# Quick Reference: Face Recognition with Manual Gender

## 🚀 Quick Start

### Enroll a Person
```bash
# Male
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "YourName" --gender male --show-preview

# Female
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "YourName" --gender female --show-preview
```

### Run Detection
```bash
python bridge/face_recognition_gender_bridge.py --watch --show-preview
```

### Check Output
```bash
cat .runtime/face_detection.json
```

## 📋 Command Reference

| Command | Description |
|---------|-------------|
| `--enroll-person` | Activate enrollment mode |
| `--person-name "Name"` | Name of person to enroll (required with --enroll-person) |
| `--gender male\|female` | Gender of person (required with --enroll-person) |
| `--watch` | Run continuously until Ctrl+C |
| `--show-preview` | Display video preview window |
| `--camera-index N` | Use camera N (default: 0) |
| `--samples N` | Capture N face samples (default: 5) |
| `--list-cameras` | List available cameras |

## 📊 Output Format

```json
{
  "person_identity": "YourName",
  "recognition_confidence": 0.85,
  "gender": "male",
  "gender_confidence": 1.0,
  "face_detected": true,
  "timestamp": "2026-05-09T12:34:56.789Z"
}
```

## 🔧 Troubleshooting

### Error: "--gender is required"
**Solution:** Add `--gender male` or `--gender female` when enrolling

### Error: "Cannot open camera"
**Solution:** Check camera index with `--list-cameras`

### Person not recognized
**Solution:** Re-enroll with more samples: `--samples 10`

### Need to update gender for existing person
**Solution:** Re-enroll with correct gender (will overwrite)

## 📁 Important Files

| File | Purpose |
|------|---------|
| `bridge/face_recognition_gender_bridge.py` | Main script |
| `models/known_faces.json` | Person database |
| `.runtime/face_detection.json` | Detection output |
| `migrate_add_gender.py` | Migration helper |

## 💡 Tips

1. **Enrollment**: Move your head slightly between samples for better accuracy
2. **Lighting**: Ensure good lighting during enrollment
3. **Distance**: Stay 1-2 feet from camera
4. **Multiple people**: Enroll each person separately
5. **Updates**: To change gender, re-enroll the person

## 🎯 What Changed

- ❌ Removed automatic gender detection (60-75% accuracy)
- ✅ Added manual gender input (100% accuracy)
- ✅ Gender stored in database during enrollment
- ✅ Gender retrieved during recognition
- ✅ Simpler, faster, more accurate

## 📞 Integration with C#

Your C# application reads `.runtime/face_detection.json` - no changes needed!

The only difference:
- `gender_confidence` is now 1.0 for recognized people (instead of 0.6-0.85)
- Gender is always accurate for enrolled people
