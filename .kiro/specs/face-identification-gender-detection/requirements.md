# Requirements Document

## Introduction

This document specifies requirements for a **standalone face identification and gender detection bridge** that operates independently from other camera bridges. The bridge detects faces through the camera, identifies specific individuals from a known database, classifies gender, and writes detection results to .runtime/current_user.json for consumption by other system components.

This bridge follows the same standalone architecture as "Face emotion bridge.py", allowing it to be tested and run independently before any future integration into iriun_combined.py. The standalone design enables isolated testing of face recognition and gender detection functionality without affecting other bridges.

## Glossary

- **Face_Detector**: The component responsible for detecting human faces in camera frames
- **Face_Recognizer**: The component that identifies specific individuals by matching detected faces against a database of known people
- **Gender_Classifier**: The component that classifies detected faces as male or female
- **Person_Database**: A persistent storage system containing face encodings and metadata for known individuals
- **Face_Encoding**: A numerical vector representation of a person's facial features used for recognition
- **Face_Recognition_Gender_Bridge**: The standalone Python script (face_recognition_gender_bridge.py) that captures video frames from the camera and performs face recognition and gender detection
- **Runtime_State**: JSON files in the .runtime directory that communicate state between Python and C# components
- **Detection_Confidence**: A numerical score (0.0 to 1.0) indicating the classifier's certainty in its prediction
- **Recognition_Confidence**: A numerical score (0.0 to 1.0) indicating the Face_Recognizer's certainty in person identification
- **Face_Present**: A boolean state indicating whether at least one face is currently detected in the camera frame
- **Person_Identity**: The name or unique identifier of a recognized individual (e.g., "Ahmed", "Youssef") or "unknown" if not recognized

## Requirements

### Requirement 1: Face Detection

**User Story:** As a user, I want the system to detect when I am present in front of the camera, so that the interface can respond to my presence.

#### Acceptance Criteria

1. WHEN a camera frame is received, THE Face_Detector SHALL process the frame to identify human faces
2. WHEN at least one face is detected, THE Face_Detector SHALL set Face_Present to true
3. WHEN no faces are detected, THE Face_Detector SHALL set Face_Present to false
4. THE Face_Detector SHALL process camera frames at a minimum rate of 5 frames per second
5. WHEN multiple faces are detected, THE Face_Detector SHALL select the largest face by bounding box area for gender classification

### Requirement 2: Face Recognition and Person Identification

**User Story:** As a user, I want the system to recognize who I am from a database of known people, so that the interface can provide personalized experiences based on my identity.

#### Acceptance Criteria

1. WHEN Face_Present is true, THE Face_Recognizer SHALL extract Face_Encoding from the detected face
2. THE Face_Recognizer SHALL compare the Face_Encoding against all entries in the Person_Database
3. WHEN a match is found with Recognition_Confidence above 0.6, THE Face_Recognizer SHALL output the Person_Identity
4. WHEN no match is found or Recognition_Confidence is below 0.6, THE Face_Recognizer SHALL output "unknown" as the Person_Identity
5. THE Face_Recognizer SHALL apply exponential moving average smoothing to recognition results over a 2-second window
6. WHEN the recognized Person_Identity changes, THE Face_Recognizer SHALL maintain the new identity for at least 1.5 seconds before accepting another change

### Requirement 3: Gender Classification

**User Story:** As a user, I want the system to identify my gender, so that the interface can be personalized for me.

#### Acceptance Criteria

1. WHEN Face_Present is true, THE Gender_Classifier SHALL classify the detected face as male or female
2. THE Gender_Classifier SHALL output a Detection_Confidence score between 0.0 and 1.0
3. WHEN Detection_Confidence is below 0.5, THE Gender_Classifier SHALL output "unknown" as the gender classification
4. THE Gender_Classifier SHALL apply exponential moving average smoothing to classification results over a 2-second window
5. WHEN the detected gender changes, THE Gender_Classifier SHALL maintain the new classification for at least 1.0 seconds before accepting another change

### Requirement 4: Runtime State Communication

**User Story:** As a developer, I want face recognition and gender detection results communicated through runtime state files, so that other system components can access detection information.

#### Acceptance Criteria

1. THE Face_Recognition_Gender_Bridge SHALL write detection results to .runtime/current_user.json
2. THE Runtime_State file SHALL contain fields for person_identity, recognition_confidence, gender, gender_confidence, face_detected, and timestamp
3. WHEN Face_Present is false, THE Face_Recognition_Gender_Bridge SHALL write person_identity as "unknown", gender as "unknown", and face_detected as false
4. THE Face_Recognition_Gender_Bridge SHALL write the Runtime_State file atomically using a temporary file and rename operation
5. THE Runtime_State file SHALL be updated at least once per second when the Face_Recognition_Gender_Bridge is running

### Requirement 5: Standalone Bridge Script

**User Story:** As a developer, I want face recognition and gender detection to run as a standalone bridge script, so that I can test it independently before future integration.

#### Acceptance Criteria

1. THE Face_Recognition_Gender_Bridge SHALL be implemented as a standalone Python script named face_recognition_gender_bridge.py in the bridge/ directory
2. THE Face_Recognition_Gender_Bridge SHALL support --camera-index command-line argument to specify which camera to use (default 0)
3. THE Face_Recognition_Gender_Bridge SHALL support --show-preview command-line argument to display a video preview window with overlays
4. THE Face_Recognition_Gender_Bridge SHALL support --watch command-line argument to run continuously until stopped by the user
5. THE Face_Recognition_Gender_Bridge SHALL support --enroll-person command-line argument to activate enrollment mode
6. THE Face_Recognition_Gender_Bridge SHALL support --person-name command-line argument to specify the name during enrollment
7. WHEN --show-preview is enabled, THE Face_Recognition_Gender_Bridge SHALL overlay the Person_Identity, gender, and confidence scores on the video preview
8. WHEN the camera is unavailable, THE Face_Recognition_Gender_Bridge SHALL write a fallback state with face_detected as false
9. THE Face_Recognition_Gender_Bridge SHALL run independently without requiring iriun_combined.py or other bridges to be running

### Requirement 6: Person Database Management

**User Story:** As a developer, I want to manage a database of known people with their face encodings, so that the system can recognize specific individuals.

#### Acceptance Criteria

1. THE Face_Recognizer SHALL load the Person_Database from a JSON file at models/known_faces.json on initialization
2. THE Person_Database file SHALL contain entries with person_id, name, and face_encodings fields
3. WHEN the Person_Database file is missing, THE Face_Recognizer SHALL create an empty database file
4. THE Face_Recognizer SHALL support storing multiple Face_Encoding entries per person to handle different angles and expressions
5. THE Person_Database SHALL persist across application restarts

### Requirement 7: Person Enrollment

**User Story:** As an administrator, I want to enroll new people into the system with their photos, so that they can be recognized in future sessions.

#### Acceptance Criteria

1. THE Face_Recognizer SHALL provide an enrollment mode activated via --enroll-person command-line argument
2. WHEN enrollment mode is active, THE Face_Recognizer SHALL capture Face_Encoding from the detected face
3. THE Face_Recognizer SHALL accept a person name via command-line argument --person-name
4. WHEN a face is detected in enrollment mode, THE Face_Recognizer SHALL add the Face_Encoding to the Person_Database with the provided name
5. THE Face_Recognizer SHALL capture at least 5 Face_Encoding samples from different angles before completing enrollment
6. WHEN enrollment is complete, THE Face_Recognizer SHALL save the updated Person_Database to disk and exit enrollment mode

### Requirement 8: Face Recognition Model

**User Story:** As a developer, I want to use a pre-trained face recognition model, so that I can achieve accurate identification without training from scratch.

#### Acceptance Criteria

1. THE Face_Recognizer SHALL use a pre-trained deep learning model for face encoding extraction
2. THE Face_Recognizer SHALL use Euclidean distance or cosine similarity to compare Face_Encoding vectors
3. WHEN comparing encodings, THE Face_Recognizer SHALL select the closest match from the Person_Database
4. THE Face_Recognizer SHALL process a face recognition in less than 150 milliseconds on typical hardware
5. THE Face_Recognizer SHALL support face recognition models compatible with the face_recognition or dlib libraries

### Requirement 9: Gender Detection Model

**User Story:** As a developer, I want to use a pre-trained gender classification model, so that I can achieve accurate results without training from scratch.

#### Acceptance Criteria

1. THE Gender_Classifier SHALL use a pre-trained deep learning model for gender classification
2. THE Gender_Classifier SHALL load the model from the models/ directory on initialization
3. WHEN the model file is missing, THE Gender_Classifier SHALL log an error and disable gender detection
4. THE Gender_Classifier SHALL support models in ONNX or PyTorch format
5. THE Gender_Classifier SHALL process a face detection in less than 100 milliseconds on typical hardware

### Requirement 10: Privacy and Data Handling

**User Story:** As a user, I want my facial data to be processed locally, so that my privacy is protected.

#### Acceptance Criteria

1. THE Face_Detector SHALL process all camera frames locally without transmitting data to external services
2. THE Face_Recognizer SHALL process all face recognition locally without transmitting data to external services
3. THE Gender_Classifier SHALL process all classifications locally without transmitting data to external services
4. THE Face_Recognition_Gender_Bridge SHALL NOT store camera frames or facial images to disk except during enrollment mode
5. WHEN enrollment mode is active, THE Face_Recognition_Gender_Bridge MAY temporarily store face images for encoding extraction
6. THE Runtime_State file SHALL contain only classification results and metadata, not image data
7. WHEN the application exits, THE Face_Recognition_Gender_Bridge SHALL delete the Runtime_State file

### Requirement 11: Error Handling and Fallback

**User Story:** As a user, I want the system to handle errors gracefully, so that the application remains usable when face recognition or gender detection fails.

#### Acceptance Criteria

1. WHEN the Face_Recognizer encounters an error, THE Face_Recognition_Gender_Bridge SHALL write person_identity as "unknown" to the Runtime_State file
2. WHEN the Gender_Classifier encounters an error, THE Face_Recognition_Gender_Bridge SHALL write gender as "unknown" to the Runtime_State file
3. WHEN the camera becomes unavailable during operation, THE Face_Recognition_Gender_Bridge SHALL write face_detected as false and continue polling for camera availability
4. THE Face_Recognition_Gender_Bridge SHALL log all errors to standard error output with descriptive messages
5. WHEN face recognition or gender detection is disabled or unavailable, THE Face_Recognition_Gender_Bridge SHALL write a fallback state with face_detected as false, person_identity as "unknown", and gender as "unknown"


---

## Future Integration Note

This specification defines a **standalone bridge script** that can be tested and run independently. Future integration into iriun_combined.py (to share camera streams with hand tracking and emotion detection) is possible but is **not part of this specification**. The standalone architecture allows:

- Independent testing of face recognition and gender detection
- Isolated debugging without affecting other bridges
- Flexible deployment (can run standalone or be integrated later)
- Clear separation of concerns during development

Integration into iriun_combined.py would be a separate feature specification that would involve:
- Sharing camera frames between multiple detection pipelines
- Coordinating multiple output files (.runtime/current_user.json, .runtime/current_emotion.json)
- Managing multiple preview windows or combining overlays
- Handling command-line arguments for multiple features
