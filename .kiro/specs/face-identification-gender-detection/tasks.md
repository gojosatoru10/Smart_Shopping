# Implementation Plan: Face Identification and Gender Detection Bridge

## Overview

This implementation plan breaks down the face identification and gender detection bridge into discrete coding tasks. The bridge will be implemented as a standalone Python script (`bridge/face_recognition_gender_bridge.py`) that detects faces, recognizes individuals from a database, classifies gender, and writes results to `.runtime/current_user.json`.

The implementation follows the same architectural pattern as `Face emotion bridge.py`, ensuring consistency with existing bridge scripts while adding face recognition and gender detection capabilities.

## Tasks

- [x] 1. Set up project structure and dependencies
  - Create `bridge/face_recognition_gender_bridge.py` as the main script
  - Add dependency specifications for face_recognition, opencv-python, numpy, Pillow, and deepface
  - Create `models/` directory for storing the known faces database
  - Ensure `.runtime/` directory exists for output files
  - _Requirements: 5.1, 5.9_

- [x] 2. Implement Person Database component
  - [x] 2.1 Create PersonDatabase class for JSON file management
    - Implement `__init__()` to load or create database file at `models/known_faces.json`
    - Implement `load()` method to read database from disk with error handling
    - Implement `save()` method with atomic write (temp file + rename)
    - Implement `add_encoding()` to add face encodings for a person
    - Implement `get_all_encodings()` to retrieve all (name, encoding) pairs
    - Handle corrupted database files by creating new empty database
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_
  
  - [ ]* 2.2 Write unit tests for PersonDatabase
    - Test database creation when file is missing
    - Test loading valid database with multiple people
    - Test handling corrupted JSON files
    - Test atomic save operation
    - Test adding encodings for new and existing people
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 3. Implement Face Detector component
  - [x] 3.1 Create FaceDetector class using face_recognition library
    - Implement `detect_faces()` using `face_recognition.face_locations()` with HOG model
    - Implement `get_largest_face()` to select face with maximum bounding box area
    - Return face locations as (top, right, bottom, left) tuples
    - _Requirements: 1.1, 1.2, 1.3, 1.5_
  
  - [ ]* 3.2 Write unit tests for FaceDetector
    - Test face detection with sample images containing single face
    - Test face detection with multiple faces (verify largest selected)
    - Test face detection with no faces (verify empty result)
    - Test bounding box area calculation
    - _Requirements: 1.1, 1.5_

- [x] 4. Implement Face Recognizer component
  - [x] 4.1 Create FaceRecognizer class for person identification
    - Implement `__init__()` to load PersonDatabase
    - Implement `encode_face()` using `face_recognition.face_encodings()`
    - Implement `recognize()` to match encoding against database using Euclidean distance
    - Use distance threshold of 0.4 (equivalent to confidence > 0.6)
    - Convert distance to confidence: `confidence = max(0.0, 1.0 - distance)`
    - Return ("unknown", 0.0) when no match above threshold
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 8.1, 8.2, 8.3_
  
  - [x] 4.2 Implement enrollment methods in FaceRecognizer
    - Implement `add_person()` to add or update person in database
    - Implement `save_database()` to persist changes to disk
    - Support multiple encodings per person for different angles
    - _Requirements: 7.2, 7.3, 7.4, 7.5_
  
  - [ ]* 4.3 Write unit tests for FaceRecognizer
    - Test face encoding extraction from sample images
    - Test recognition with known encodings (match above threshold)
    - Test recognition with unknown encodings (no match)
    - Test distance-to-confidence conversion
    - Test adding new person to database
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 5. Implement Gender Classifier component
  - [x] 5.1 Create GenderClassifier class using DeepFace
    - Implement `__init__()` to initialize DeepFace analyzer
    - Implement `classify()` to analyze face region and extract gender prediction
    - Implement `preprocess_face()` to extract and prepare face region from frame
    - Return ("unknown", 0.0) when confidence below 0.5 threshold
    - Handle model loading errors gracefully (disable gender detection if model unavailable)
    - _Requirements: 3.1, 3.2, 3.3, 9.1, 9.2, 9.3_
  
  - [ ]* 5.2 Write unit tests for GenderClassifier
    - Test gender classification with sample face images
    - Test preprocessing of face regions
    - Test handling of low-confidence predictions (< 0.5)
    - Test error handling when model is unavailable
    - _Requirements: 3.1, 3.2, 3.3_

- [x] 6. Implement Exponential Smoothing component
  - [x] 6.1 Create ExponentialSmoother class for temporal smoothing
    - Implement `__init__()` with alpha and stability_window parameters
    - Implement `update()` to apply EMA smoothing to confidence scores
    - Implement stability check: reject value changes within stability window
    - Implement `reset()` to clear smoother state
    - Use alpha=0.3 for recognition smoother (2-second window, 1.5s stability)
    - Use alpha=0.4 for gender smoother (2-second window, 1.0s stability)
    - _Requirements: 2.5, 2.6, 3.4, 3.5_
  
  - [ ]* 6.2 Write unit tests for ExponentialSmoother
    - Test EMA calculation with known input sequences
    - Test stability window (reject rapid changes)
    - Test reset functionality
    - Test different alpha values
    - _Requirements: 2.5, 2.6, 3.4, 3.5_

- [x] 7. Implement Output Writer component
  - [x] 7.1 Create OutputWriter class for JSON file writing
    - Implement `__init__()` with output path `.runtime/current_user.json`
    - Implement `write_detection()` to write full detection results
    - Implement `write_fallback()` to write no-face-detected state
    - Use atomic write strategy: write to `.runtime/current_user.tmp` then rename
    - Include all required fields: person_identity, recognition_confidence, gender, gender_confidence, face_detected, timestamp
    - Format timestamp as ISO 8601 with milliseconds
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_
  
  - [ ]* 7.2 Write unit tests for OutputWriter
    - Test writing detection results with all fields
    - Test writing fallback state
    - Test atomic write operation (temp file + rename)
    - Test JSON format validation
    - Test timestamp format
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [x] 8. Checkpoint - Ensure all core components pass tests
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement Enrollment Mode component
  - [x] 9.1 Create EnrollmentMode class for capturing face samples
    - Implement `__init__()` with person_name, database, and target_samples (default 5)
    - Implement `add_sample()` to collect face encodings
    - Implement `get_progress()` to return (current_samples, target_samples)
    - Track face position variance to ensure samples from different angles
    - Complete enrollment after collecting target number of samples
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_
  
  - [x] 9.2 Implement enrollment workflow in main script
    - Parse `--enroll-person` and `--person-name` command-line arguments
    - Activate enrollment mode when `--enroll-person` is provided
    - Display progress messages in console during enrollment
    - Save database and exit after enrollment completes
    - Handle duplicate person names (prompt for overwrite confirmation)
    - _Requirements: 5.5, 5.6, 7.1, 7.3, 7.4, 7.6_
  
  - [ ]* 9.3 Write integration tests for enrollment mode
    - Test enrollment workflow with simulated camera frames
    - Test progress tracking during enrollment
    - Test database persistence after enrollment
    - Test handling of duplicate person names
    - _Requirements: 7.1, 7.4, 7.5, 7.6_

- [x] 10. Implement main detection loop
  - [x] 10.1 Create camera capture and frame processing loop
    - Initialize OpenCV VideoCapture with camera index
    - Implement main loop to capture frames continuously in watch mode
    - Call FaceDetector to detect faces in each frame
    - Process largest detected face through FaceRecognizer and GenderClassifier
    - Apply ExponentialSmoother to recognition and gender results
    - Write results to OutputWriter at configured interval
    - _Requirements: 1.1, 1.4, 2.1, 3.1, 4.5, 5.9_
  
  - [x] 10.2 Implement frame rate control and optimization
    - Add frame skipping to maintain target 5-10 FPS
    - Use HOG-based face detection for speed (not CNN)
    - Implement interval-based output writing (default 0.5 seconds)
    - Cache face encodings in memory (don't reload from disk each frame)
    - _Requirements: 1.4, 8.4, 9.5_
  
  - [ ]* 10.3 Write integration tests for detection loop
    - Test detection loop with sample video frames
    - Test frame rate control and skipping
    - Test output writing at correct intervals
    - Test handling of frames with no faces
    - _Requirements: 1.1, 1.4, 4.5_

- [x] 11. Implement command-line interface
  - [x] 11.1 Create argument parser with all required options
    - Add `--camera-index` (default 0) for camera selection
    - Add `--show-preview` flag for video preview window
    - Add `--watch` flag for continuous operation
    - Add `--interval` (default 0.5) for output write frequency
    - Add `--database` (default models/known_faces.json) for database path
    - Add `--recognition-threshold` (default 0.6) for recognition confidence
    - Add `--gender-threshold` (default 0.5) for gender confidence
    - Add `--enroll-person` flag for enrollment mode
    - Add `--person-name` for enrollment person name
    - Add `--samples` (default 5) for enrollment sample count
    - Add `--list-cameras` utility to list available cameras
    - _Requirements: 5.2, 5.3, 5.4, 5.5, 5.6_
  
  - [x] 11.2 Implement preview window with overlays
    - Display video feed when `--show-preview` is enabled
    - Overlay person_identity text on detected face
    - Overlay gender and confidence scores
    - Draw bounding box around detected face
    - Display enrollment progress during enrollment mode
    - _Requirements: 5.7_
  
  - [ ]* 11.3 Write tests for CLI argument parsing
    - Test parsing of all command-line arguments
    - Test default values
    - Test validation of required arguments (e.g., --person-name with --enroll-person)
    - _Requirements: 5.2, 5.3, 5.4, 5.5, 5.6_

- [x] 12. Checkpoint - Ensure detection and enrollment workflows work end-to-end
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. Implement error handling and fallback behavior
  - [x] 13.1 Add camera error handling
    - Handle camera unavailable at startup (write fallback state and exit)
    - Handle camera disconnection during operation (write fallback, retry connection)
    - Implement camera reconnection polling with 2-second intervals
    - Log descriptive error messages to stderr
    - _Requirements: 5.8, 11.3_
  
  - [x] 13.2 Add face recognition error handling
    - Handle face encoding extraction failures (write "unknown" identity)
    - Handle database load failures (create new empty database)
    - Handle empty database (no known faces)
    - Continue operation with fallback values on errors
    - _Requirements: 11.1, 11.5_
  
  - [x] 13.3 Add gender classification error handling
    - Handle model loading failures (disable gender detection, continue with recognition)
    - Handle inference failures (write "unknown" gender)
    - Log warnings when gender detection is disabled
    - _Requirements: 9.3, 11.2_
  
  - [x] 13.4 Add file I/O error handling
    - Handle output file write failures with retry logic (3 attempts, exponential backoff)
    - Handle database save failures during enrollment
    - Handle disk full scenarios
    - Log all I/O errors to stderr
    - _Requirements: 11.4_
  
  - [x] 13.5 Add enrollment error handling
    - Handle no face detected during enrollment (wait and display message)
    - Handle duplicate person names (prompt for overwrite)
    - Handle insufficient samples (continue capturing)
    - _Requirements: 7.3, 7.4_
  
  - [ ]* 13.6 Write integration tests for error handling
    - Test camera unavailable scenario
    - Test database corruption handling
    - Test model loading failure
    - Test output write failure with retry
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

- [x] 14. Implement privacy and cleanup features
  - [x] 14.1 Add privacy safeguards
    - Ensure all processing happens locally (no external API calls)
    - Do not store camera frames to disk except during enrollment
    - Only store face encodings (not images) in database
    - Include only metadata in output JSON (no image data)
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.6_
  
  - [x] 14.2 Add cleanup on exit
    - Delete `.runtime/current_user.json` on application exit
    - Release camera resources properly
    - Close preview window if open
    - Handle Ctrl+C gracefully (KeyboardInterrupt)
    - _Requirements: 10.7_
  
  - [ ]* 14.3 Write tests for privacy features
    - Verify no external network calls are made
    - Verify no image files are created (except during enrollment)
    - Verify output JSON contains only metadata
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

- [x] 15. Add logging and debugging features
  - [x] 15.1 Implement structured logging
    - Log to stderr for errors ([ERROR] prefix)
    - Log to stderr for warnings ([WARNING] prefix)
    - Log to stdout for informational messages ([INFO] prefix)
    - Log detection results (person identity, gender, confidence) to stdout
    - Log enrollment progress to stdout
    - _Requirements: 11.4_
  
  - [x] 15.2 Add utility commands
    - Implement `--list-cameras` to enumerate available camera indices
    - Test each camera index and report availability
    - Display helpful error messages with suggestions
    - _Requirements: 5.2_

- [x] 16. Create documentation and usage examples
  - [x] 16.1 Add docstrings to all classes and methods
    - Document parameters, return values, and exceptions
    - Include usage examples in docstrings
    - Follow Google or NumPy docstring style
    - _Requirements: All_
  
  - [x] 16.2 Create README or usage guide
    - Document installation steps (dependencies, model setup)
    - Provide command-line usage examples
    - Document enrollment workflow
    - Document integration with C# application
    - Include troubleshooting section
    - _Requirements: 5.2, 5.3, 5.4, 5.5, 5.6, 5.7_

- [x] 17. Final integration and testing
  - [x] 17.1 Test standalone operation
    - Run bridge script independently without other bridges
    - Verify output JSON is written correctly
    - Test with real camera in different lighting conditions
    - Test enrollment with multiple people
    - Test recognition accuracy with enrolled people
    - _Requirements: 5.9_
  
  - [ ]* 17.2 Perform manual testing scenarios
    - Test with different camera indices
    - Test preview window overlays
    - Test watch mode (continuous operation)
    - Test graceful handling of camera disconnect
    - Test performance (verify 5+ FPS, < 300ms latency)
    - _Requirements: 1.4, 5.3, 5.4, 5.7, 8.4, 9.5_
  
  - [ ]* 17.3 Test integration with C# application
    - Verify C# application can read `.runtime/current_user.json`
    - Test real-time updates during watch mode
    - Verify JSON format compatibility
    - Test fallback state handling in C# application
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 18. Final checkpoint - Complete implementation verification
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- The implementation follows the same pattern as `Face emotion bridge.py` for consistency
- All processing happens locally for privacy (no external API calls)
- The bridge runs independently and can be tested without other system components
- Property-based testing is not applicable due to non-deterministic ML models and hardware dependencies
- Manual testing with real camera is essential for validation
