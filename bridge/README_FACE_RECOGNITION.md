# Face Recognition and Gender Detection Bridge

Standalone Python bridge for face recognition and gender detection. Detects faces through the camera, identifies specific individuals from a database, classifies gender, and writes results to `.runtime/current_user.json`.

## Features

- **Face Detection**: Detects human faces using HOG-based detection (5+ FPS)
- **Face Recognition**: Identifies individuals by matching 128-d face encodings against a database
- **Gender Classification**: Classifies detected faces as male or female using DeepFace
- **Person Enrollment**: Add new people to the database with multiple face samples
- **Temporal Smoothing**: EMA smoothing to reduce jitter in detection results
- **Privacy-First**: All processing happens locally, no external API calls
- **Standalone Operation**: Runs independently without requiring other bridges

## Installation

### 1. Install Dependencies

```bash
# Install all required packages
python -m pip install -r requirements-face-recognition.txt

# Or install individually
python -m pip install face-recognition opencv-python numpy Pillow deepface
```

**Note for Windows users**: You may need Visual C++ 14.0+ build tools for dlib compilation. Alternatively, install pre-built wheels:

```bash
python -m pip install dlib-binary
```

### 2. Verify Installation

```bash
# List available cameras
python bridge/face_recognition_gender_bridge.py --list-cameras
```

## Usage

### Enrollment Mode

Add new people to the recognition database:

```bash
# Enroll a person (captures 5 face samples from different angles)
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Ahmed" --show-preview

# Enroll with custom number of samples
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Youssef" --samples 10 --show-preview
```

**During enrollment:**
- Position your face in the camera
- Move your head to different angles (left, right, up, down)
- The system will capture samples automatically
- Press 'q' to quit enrollment early

### Detection Mode

Run face recognition and gender detection:

```bash
# Single frame detection
python bridge/face_recognition_gender_bridge.py --camera-index 0

# Continuous detection with preview
python bridge/face_recognition_gender_bridge.py --watch --show-preview

# Continuous detection without preview (background mode)
python bridge/face_recognition_gender_bridge.py --watch
```

### Command-Line Options

#### Detection Mode Options

- `--camera-index INT`: Camera device index (default: 0)
- `--show-preview`: Display video preview window with overlays
- `--watch`: Run continuously until Ctrl+C
- `--interval FLOAT`: Seconds between detections (default: 0.5)
- `--database PATH`: Path to known faces database (default: models/known_faces.json)
- `--recognition-threshold FLOAT`: Minimum confidence for recognition (default: 0.6)
- `--gender-threshold FLOAT`: Minimum confidence for gender (default: 0.5)

#### Enrollment Mode Options

- `--enroll-person`: Activate enrollment mode
- `--person-name STRING`: Name of person to enroll (required with --enroll-person)
- `--samples INT`: Number of encoding samples to capture (default: 5)

#### Utility Options

- `--list-cameras`: List available camera indices and exit

## Output Format

The bridge writes detection results to `.runtime/current_user.json`:

```json
{
  "person_identity": "Ahmed",
  "recognition_confidence": 0.8234,
  "gender": "male",
  "gender_confidence": 0.9123,
  "face_detected": true,
  "timestamp": "2026-05-09T10:35:42.123Z"
}
```

**Fallback state** (no face detected):

```json
{
  "person_identity": "unknown",
  "recognition_confidence": 0.0,
  "gender": "unknown",
  "gender_confidence": 0.0,
  "face_detected": false,
  "timestamp": "2026-05-09T10:35:42.123Z"
}
```

## Database Format

Known faces are stored in `models/known_faces.json`:

```json
{
  "version": "1.0",
  "people": [
    {
      "person_id": "uuid-string",
      "name": "Ahmed",
      "encodings": [
        [0.123, -0.456, 0.789, ...],  // 128-d vector
        [0.124, -0.455, 0.788, ...]   // Multiple samples
      ],
      "created_at": "2026-05-09T10:30:00Z",
      "updated_at": "2026-05-09T10:30:00Z"
    }
  ]
}
```

## Integration with C# Application

The C# application can read `.runtime/current_user.json` to access detection results:

```csharp
// Example C# code to read detection results
string jsonPath = Path.Combine(runtimeDir, "current_user.json");
if (File.Exists(jsonPath))
{
    string json = File.ReadAllText(jsonPath);
    var result = JsonConvert.DeserializeObject<UserDetection>(json);
    
    if (result.face_detected)
    {
        Console.WriteLine($"Person: {result.person_identity}");
        Console.WriteLine($"Gender: {result.gender}");
    }
}
```

## Troubleshooting

### Camera Not Opening

```bash
# List available cameras
python bridge/face_recognition_gender_bridge.py --list-cameras

# Try different camera index
python bridge/face_recognition_gender_bridge.py --camera-index 1 --watch
```

### DeepFace Not Installed

If gender detection is disabled:

```bash
python -m pip install deepface
```

### Face Recognition Accuracy Issues

- Enroll with more samples: `--samples 10`
- Ensure good lighting conditions
- Capture samples from multiple angles
- Re-enroll if person's appearance changes significantly

### Performance Issues

- Use HOG-based detection (default, faster)
- Reduce detection interval: `--interval 1.0`
- Close preview window if not needed (remove `--show-preview`)

## Privacy and Security

- **Local Processing**: All face detection, recognition, and gender classification happen locally
- **No External Calls**: No data is transmitted to external services
- **No Image Storage**: Camera frames are not stored to disk (except temporarily during enrollment)
- **Encoding Only**: Database stores only face encodings (128-d vectors), not images
- **Automatic Cleanup**: Output file is deleted on application exit

## Performance

- **Frame Rate**: 5-10 FPS (typical hardware)
- **Face Detection**: < 50ms per frame (HOG-based)
- **Face Encoding**: < 100ms per face
- **Face Recognition**: < 50ms (distance computation)
- **Gender Classification**: < 100ms per face
- **Total Latency**: < 300ms from frame capture to JSON write

## Architecture

The bridge follows a modular component-based architecture:

1. **PersonDatabase**: JSON file management for known faces
2. **FaceDetector**: Face detection using face_recognition library
3. **FaceRecognizer**: Person identification using face encodings
4. **GenderClassifier**: Gender classification using DeepFace
5. **ExponentialSmoother**: Temporal smoothing for stability
6. **OutputWriter**: Atomic JSON file writing
7. **EnrollmentMode**: Face sample collection for new people

## Future Integration

This standalone bridge can be integrated into `iriun_combined.py` in the future to share camera streams with hand tracking and emotion detection. The standalone architecture allows:

- Independent testing and debugging
- Flexible deployment (standalone or integrated)
- Clear separation of concerns during development

## License

See main project LICENSE.txt

## Support

For issues or questions, refer to the main project documentation or contact the development team.
