# Gender Enrollment Changes

## Summary

The face recognition bridge has been updated to use **manual gender input during enrollment** instead of automatic gender detection. This provides 100% accuracy for gender information since it's provided by the user.

## What Changed

### 1. **Removed Automatic Gender Detection**
   - Deleted the `GenderClassifier` class (150+ lines of heuristic-based detection code)
   - Removed DeepFace dependency (no longer needed)
   - Removed gender smoothing logic (not needed for static data)

### 2. **Added Manual Gender Input**
   - New `--gender` parameter required during enrollment (choices: "male" or "female")
   - Gender is stored in the person database alongside name and face encodings
   - Gender is retrieved from database during recognition (100% confidence)

### 3. **New Component: GenderRetriever**
   - Replaces `GenderClassifier`
   - Retrieves gender from database based on recognized person
   - Returns ("unknown", 0.0) for unrecognized people

### 4. **Updated Database Schema**
   - Added `gender` field to person entries in `known_faces.json`
   - Example:
     ```json
     {
       "person_id": "uuid",
       "name": "Ahmed",
       "gender": "male",
       "encodings": [[...], [...]]
     }
     ```

## New Usage

### Enrollment (Gender Required)
```bash
# Enroll a male person
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Ahmed" --gender male --show-preview

# Enroll a female person
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "Sara" --gender female --show-preview
```

### Detection (No Changes)
```bash
# Run detection - gender is automatically retrieved from database
python bridge/face_recognition_gender_bridge.py --watch --show-preview
```

## Benefits

1. **100% Gender Accuracy**: No more 60-75% heuristic accuracy
2. **Simpler Code**: Removed 150+ lines of complex gender detection logic
3. **Faster Performance**: No gender classification inference needed
4. **No ML Dependencies**: Removed DeepFace/TensorFlow requirement
5. **User Control**: Users specify their own gender during enrollment

## Output Format (Unchanged)

The output JSON format remains the same:
```json
{
  "person_identity": "Ahmed",
  "recognition_confidence": 0.85,
  "gender": "male",
  "gender_confidence": 1.0,
  "face_detected": true,
  "timestamp": "2025-01-15T10:35:42.123Z"
}
```

**Note**: `gender_confidence` is now always 1.0 for recognized people (since it's from enrollment data) and 0.0 for unknown people.

## Migration for Existing Users

If you have existing enrolled people in `known_faces.json` without gender data:

1. **Option A**: Re-enroll them with the new `--gender` parameter
2. **Option B**: Manually edit `known_faces.json` and add `"gender": "male"` or `"gender": "female"` to each person entry

## Files Modified

- `bridge/face_recognition_gender_bridge.py` - Main implementation
  - Removed: `GenderClassifier` class
  - Added: `GenderRetriever` class
  - Updated: `PersonDatabase.add_encoding()` to accept gender parameter
  - Updated: `PersonDatabase.get_person_gender()` method added
  - Updated: `EnrollmentMode` to require gender parameter
  - Updated: Command-line argument parser to include `--gender`
  - Updated: Detection loop to use `GenderRetriever` instead of `GenderClassifier`

## Testing

Test the changes:

```bash
# 1. Enroll a person with gender
python bridge/face_recognition_gender_bridge.py --enroll-person --person-name "TestUser" --gender male --show-preview --camera-index 0

# 2. Run detection
python bridge/face_recognition_gender_bridge.py --watch --show-preview --camera-index 0

# 3. Check output
cat .runtime/face_detection.json
```

Expected output should show:
- `person_identity`: "TestUser"
- `gender`: "male"
- `gender_confidence`: 1.0
