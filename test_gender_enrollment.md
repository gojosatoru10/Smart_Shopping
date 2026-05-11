# Testing Gender Enrollment Feature

## Quick Test Guide

### Step 1: Enroll a Person with Gender

```bash
# Enroll yourself or a test person
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "YourName" --gender male --show-preview --camera-index 0

# Or for a female person
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Sara" --gender female --show-preview --camera-index 0
```

**What happens:**
- Camera opens with preview window
- System captures 5 face samples from different angles
- Gender is stored with the person's data
- Console shows: `[INFO] Enrollment complete for 'YourName' (male) with 5 samples`

### Step 2: Run Detection

```bash
python bridge/face_recognition_gender_bridge.py --watch --show-preview --camera-index 0
```

**What happens:**
- Camera opens and continuously detects faces
- When you're recognized, your name and gender appear
- Gender is retrieved from database (not detected automatically)
- Output written to `.runtime/face_detection.json`

### Step 3: Check the Output

```bash
# View the detection output
cat .runtime/face_detection.json
```

**Expected output:**
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

**Note:** `gender_confidence` is 1.0 because gender comes from enrollment data (100% accurate).

### Step 4: Check the Database

```bash
# View the stored data
cat models/known_faces.json
```

**Expected format:**
```json
{
  "version": "1.0",
  "people": [
    {
      "person_id": "some-uuid",
      "name": "YourName",
      "gender": "male",
      "encodings": [
        [0.123, -0.456, ...],
        [0.124, -0.455, ...],
        ...
      ],
      "created_at": "2026-05-09T12:30:00Z",
      "updated_at": "2026-05-09T12:30:00Z"
    }
  ]
}
```

## Error Cases

### Missing Gender Parameter
```bash
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Test"
```
**Error:** `[ERROR] --gender is required with --enroll-person (choices: 'male', 'female')`

### Invalid Gender Value
```bash
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Test" --gender other
```
**Error:** `error: argument --gender: invalid choice: 'other' (choose from 'male', 'female')`

## Comparison: Before vs After

### Before (Automatic Detection)
- Gender detected using heuristics (60-75% accuracy)
- Required DeepFace/TensorFlow
- Slower performance (100ms per frame)
- Could be wrong for some people

### After (Manual Enrollment)
- Gender specified by user (100% accuracy)
- No ML dependencies needed
- Instant retrieval from database
- Always correct for enrolled people

## Integration with C# Application

Your C# application can read `.runtime/face_detection.json` exactly as before. The only difference:
- `gender_confidence` is now 1.0 for recognized people (instead of 0.6-0.85)
- Gender is always accurate for enrolled people
- Unknown people still show `"gender": "unknown"` with confidence 0.0
