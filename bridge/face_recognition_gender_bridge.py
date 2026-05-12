#!/usr/bin/env python3
"""
face_recognition_gender_bridge.py
Standalone bridge for face recognition with manual gender enrollment.
Detects faces, identifies individuals from a database, retrieves gender from enrollment data,
and writes results to .runtime/face_detection.json.

Usage:
    # Detection mode
    python face_recognition_gender_bridge.py --watch --show-preview
    
    # Enrollment mode (gender is required)
    python face_recognition_gender_bridge.py --enroll-person --person-name "Ahmed" --gender male --show-preview
    python face_recognition_gender_bridge.py --enroll-person --person-name "Sara" --gender female --show-preview
    
    # List cameras
    python face_recognition_gender_bridge.py --list-cameras

Requirements:
    python -m pip install face-recognition opencv-python numpy Pillow
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional, Tuple
import uuid

# Dependency checks
try:
    import cv2
except ImportError:
    print("[ERROR] opencv-python not installed. Run: python -m pip install opencv-python", file=sys.stderr)
    sys.exit(1)

try:
    import numpy as np
except ImportError:
    print("[ERROR] numpy not installed. Run: python -m pip install numpy", file=sys.stderr)
    sys.exit(1)

try:
    import face_recognition
except ImportError:
    print("[ERROR] face_recognition not installed. Run: python -m pip install face-recognition", file=sys.stderr)
    sys.exit(1)

# ── Constants ────────────────────────────────────────────────────────────────
REPO_ROOT = Path(__file__).resolve().parent.parent
RUNTIME_DIR = REPO_ROOT / ".runtime"
USER_JSON = RUNTIME_DIR / "face_detection.json"  # Separate file to avoid conflict with Bluetooth
DEFAULT_DATABASE = REPO_ROOT / "models" / "known_faces.json"

# Recognition thresholds
RECOGNITION_DISTANCE_THRESHOLD = 0.4  # Euclidean distance (lower = more similar)
RECOGNITION_CONFIDENCE_THRESHOLD = 0.6  # Minimum confidence to accept match
GENDER_CONFIDENCE_THRESHOLD = 0.5  # Minimum confidence for gender classification

# Smoothing parameters
RECOGNITION_ALPHA = 0.3  # EMA weight for recognition
RECOGNITION_STABILITY_WINDOW = 1.5  # Seconds to maintain identity before change
GENDER_ALPHA = 0.4  # EMA weight for gender
GENDER_STABILITY_WINDOW = 1.0  # Seconds to maintain gender before change

# Performance parameters
DEFAULT_INTERVAL = 0.5  # Seconds between detections
DEFAULT_ENROLLMENT_SAMPLES = 5  # Number of face samples to capture during enrollment


# ══════════════════════════════════════════════════════════════════════════════
# Enrollment Mode Component
# ══════════════════════════════════════════════════════════════════════════════

class EnrollmentMode:
    """
    Captures face samples for enrolling new people into the database.
    
    Collects multiple face encodings from different angles to improve
    recognition accuracy.
    """
    
    def __init__(self, person_name: str, gender: str, recognizer: FaceRecognizer, target_samples: int = DEFAULT_ENROLLMENT_SAMPLES):
        """
        Initialize enrollment for person.
        
        Args:
            person_name: Name of person to enroll
            gender: Gender of person ("male" or "female")
            recognizer: FaceRecognizer instance
            target_samples: Number of samples to capture (default 5)
        """
        self.person_name = person_name
        self.gender = gender
        self.recognizer = recognizer
        self.target_samples = target_samples
        self.samples: List[np.ndarray] = []
        self.face_positions: List[Tuple[int, int]] = []  # Track face positions for variance
    
    def add_sample(self, encoding: np.ndarray, face_location: Tuple[int, int, int, int]) -> bool:
        """
        Add encoding sample.
        
        Args:
            encoding: 128-d face encoding
            face_location: (top, right, bottom, left) bounding box
            
        Returns:
            True if enrollment complete, False if more samples needed
        """
        # Calculate face center position
        top, right, bottom, left = face_location
        center_x = (left + right) // 2
        center_y = (top + bottom) // 2
        
        # Check if this position is sufficiently different from previous samples
        # (ensures we capture from different angles)
        is_different = True
        for prev_x, prev_y in self.face_positions:
            distance = ((center_x - prev_x) ** 2 + (center_y - prev_y) ** 2) ** 0.5
            if distance < 50:  # Minimum pixel distance between samples
                is_different = False
                break
        
        if is_different or len(self.samples) == 0:
            self.samples.append(encoding)
            self.face_positions.append((center_x, center_y))
            print(f"[INFO] Captured sample {len(self.samples)}/{self.target_samples}", flush=True)
        
        return len(self.samples) >= self.target_samples
    
    def get_progress(self) -> Tuple[int, int]:
        """
        Return (current_samples, target_samples).
        
        Returns:
            Tuple of (current, target) sample counts
        """
        return (len(self.samples), self.target_samples)
    
    def save(self) -> None:
        """Save all collected samples to database."""
        for encoding in self.samples:
            self.recognizer.add_person(self.person_name, encoding, self.gender)
        
        self.recognizer.save_database()
        print(f"[INFO] Enrollment complete for '{self.person_name}' ({self.gender}) with {len(self.samples)} samples", flush=True)


# ══════════════════════════════════════════════════════════════════════════════
# Output Writer Component
# ══════════════════════════════════════════════════════════════════════════════

class OutputWriter:
    """
    Writes detection results to JSON file atomically.
    
    Uses temp file + rename strategy to ensure C# application never reads
    partial/corrupted JSON.
    """
    
    def __init__(self, output_path: Path):
        """
        Initialize writer with output file path.
        
        Args:
            output_path: Path to output JSON file
        """
        self.output_path = output_path
        self.output_path.parent.mkdir(parents=True, exist_ok=True)
    
    def write_detection(self, person_identity: str, recognition_conf: float,
                       gender: str, gender_conf: float, face_detected: bool) -> None:
        """
        Write detection results atomically.
        
        Args:
            person_identity: Recognized person name or "unknown"
            recognition_conf: Face recognition confidence (0.0-1.0)
            gender: "male", "female", or "unknown"
            gender_conf: Gender classification confidence (0.0-1.0)
            face_detected: Boolean indicating if face is present
        """
        payload = {
            "person_identity": person_identity,
            "recognition_confidence": round(float(recognition_conf), 4),
            "gender": gender,
            "gender_confidence": round(float(gender_conf), 4),
            "face_detected": bool(face_detected),
            "timestamp": datetime.utcnow().isoformat() + "Z"
        }
        
        self._write_atomic(payload)
    
    def write_fallback(self) -> None:
        """Write no-face-detected state."""
        payload = {
            "person_identity": "unknown",
            "recognition_confidence": 0.0,
            "gender": "unknown",
            "gender_confidence": 0.0,
            "face_detected": False,
            "timestamp": datetime.utcnow().isoformat() + "Z"
        }
        
        self._write_atomic(payload)
    
    def _write_atomic(self, payload: Dict) -> None:
        """
        Write JSON atomically using temp file + rename.
        
        Args:
            payload: Dictionary to write as JSON
        """
        try:
            # Write to temporary file
            tmp_path = self.output_path.with_suffix('.tmp')
            with open(tmp_path, 'w') as f:
                json.dump(payload, f, indent=2)
            
            # Atomic rename
            tmp_path.replace(self.output_path)
        
        except Exception as e:
            print(f"[ERROR] Failed to write output: {e}", file=sys.stderr)


# ══════════════════════════════════════════════════════════════════════════════
# Exponential Smoothing Component
# ══════════════════════════════════════════════════════════════════════════════

class ExponentialSmoother:
    """
    Applies temporal smoothing to reduce jitter in detection results.
    
    Uses exponential moving average (EMA) for confidence scores and
    stability window for categorical values (person identity, gender).
    """
    
    def __init__(self, alpha: float, stability_window: float):
        """
        Initialize smoother.
        
        Args:
            alpha: EMA weight for new values (0.0-1.0)
            stability_window: Seconds to maintain value before accepting change
        """
        self.alpha = alpha
        self.stability_window = stability_window
        
        self.current_value: Optional[str] = None
        self.current_confidence: float = 0.0
        self.last_change_time: float = 0.0
    
    def update(self, value: str, confidence: float, timestamp: float) -> Tuple[str, float]:
        """
        Update with new value and return smoothed result.
        
        Args:
            value: New detection value (person name or gender)
            confidence: Confidence score for new value
            timestamp: Current timestamp
            
        Returns:
            (smoothed_value, smoothed_confidence)
        """
        # Apply EMA to confidence
        if self.current_value is None:
            # First update
            self.current_value = value
            self.current_confidence = confidence
            self.last_change_time = timestamp
            return (self.current_value, self.current_confidence)
        
        # Smooth confidence using EMA
        smoothed_confidence = self.alpha * confidence + (1 - self.alpha) * self.current_confidence
        
        # Check if value changed
        if value != self.current_value:
            # Check stability window
            time_since_change = timestamp - self.last_change_time
            
            if time_since_change >= self.stability_window:
                # Accept change
                self.current_value = value
                self.current_confidence = smoothed_confidence
                self.last_change_time = timestamp
            # else: Reject change, too soon
        else:
            # Value unchanged, update confidence
            self.current_confidence = smoothed_confidence
        
        return (self.current_value, self.current_confidence)
    
    def reset(self) -> None:
        """Reset smoother state."""
        self.current_value = None
        self.current_confidence = 0.0
        self.last_change_time = 0.0


# ══════════════════════════════════════════════════════════════════════════════
# Gender Retrieval Component
# ══════════════════════════════════════════════════════════════════════════════

class GenderRetriever:
    """
    Retrieves gender information from the person database.
    
    Gender is stored during enrollment and retrieved during recognition.
    No automatic gender detection is performed.
    """
    
    def __init__(self, database: 'PersonDatabase'):
        """
        Initialize gender retriever.
        
        Args:
            database: PersonDatabase instance to retrieve gender from
        """
        self.database = database
        print("[INFO] Using manual gender from enrollment data", file=sys.stderr)
    
    def get_gender(self, person_name: str) -> Tuple[str, float]:
        """
        Get gender for a recognized person from database.
        
        Args:
            person_name: Name of the recognized person
            
        Returns:
            (gender, confidence) where gender is "male", "female", or "unknown"
            confidence is 1.0 if gender is stored, 0.0 if unknown
        """
        if person_name == "unknown":
            return ("unknown", 0.0)
        
        gender = self.database.get_person_gender(person_name)
        
        if gender and gender in ["male", "female"]:
            return (gender, 1.0)  # 100% confidence since it's from enrollment
        else:
            return ("unknown", 0.0)


# ══════════════════════════════════════════════════════════════════════════════
# Face Recognizer Component
# ══════════════════════════════════════════════════════════════════════════════

class FaceRecognizer:
    """
    Identifies specific individuals by matching face encodings against a database.
    
    Uses 128-dimensional dlib face embeddings and Euclidean distance for matching.
    """
    
    def __init__(self, database: PersonDatabase):
        """
        Initialize FaceRecognizer with a PersonDatabase.
        
        Args:
            database: PersonDatabase instance for storing/loading known faces
        """
        self.database = database
    
    def encode_face(self, frame: np.ndarray, face_location: Tuple[int, int, int, int]) -> Optional[np.ndarray]:
        """
        Extract 128-dimensional face encoding.
        
        Args:
            frame: BGR image from OpenCV
            face_location: (top, right, bottom, left) bounding box
            
        Returns:
            128-d encoding vector or None if encoding fails
        """
        try:
            # Convert BGR to RGB
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            
            # Extract face encoding
            encodings = face_recognition.face_encodings(rgb_frame, [face_location])
            
            if encodings:
                return encodings[0]
            else:
                return None
        
        except Exception as e:
            print(f"[WARNING] Face encoding failed: {e}", file=sys.stderr)
            return None
    
    def recognize(self, face_encoding: np.ndarray) -> Tuple[str, float]:
        """
        Match encoding against database.
        
        Args:
            face_encoding: 128-d vector from encode_face()
            
        Returns:
            (person_name, confidence) where confidence is 1.0 - distance
            Returns ("unknown", 0.0) if no match above threshold
        """
        known_encodings = self.database.get_all_encodings()
        
        if not known_encodings:
            # Empty database
            return ("unknown", 0.0)
        
        # Extract names and encodings
        known_names = [name for name, _ in known_encodings]
        known_face_encodings = [encoding for _, encoding in known_encodings]
        
        # Compute distances to all known faces
        distances = face_recognition.face_distance(known_face_encodings, face_encoding)
        
        # Find best match
        min_distance_idx = np.argmin(distances)
        min_distance = distances[min_distance_idx]
        
        # Check if distance is below threshold
        if min_distance < RECOGNITION_DISTANCE_THRESHOLD:
            # Convert distance to confidence: confidence = 1.0 - distance
            confidence = max(0.0, 1.0 - min_distance)
            person_name = known_names[min_distance_idx]
            return (person_name, confidence)
        else:
            # No match above threshold
            return ("unknown", 0.0)
    
    def add_person(self, name: str, encoding: np.ndarray, gender: Optional[str] = None) -> None:
        """
        Add or update person in database.
        
        Args:
            name: Person's name
            encoding: 128-d face encoding
            gender: Gender of the person ("male" or "female"), optional
        """
        self.database.add_encoding(name, encoding, gender)
    
    def save_database(self) -> None:
        """Persist database to disk."""
        self.database.save()


# ══════════════════════════════════════════════════════════════════════════════
# Face Detector Component
# ══════════════════════════════════════════════════════════════════════════════

class FaceDetector:
    """
    Detects human faces in camera frames using face_recognition library.
    
    Uses HOG-based detection for speed (5+ FPS on typical hardware).
    """
    
    def detect_faces(self, frame: np.ndarray) -> List[Tuple[int, int, int, int]]:
        """
        Detect faces in frame.
        
        Args:
            frame: BGR image from OpenCV
            
        Returns:
            List of (top, right, bottom, left) bounding boxes
        """
        # Convert BGR to RGB (face_recognition expects RGB)
        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        
        # Detect faces using HOG model (faster than CNN)
        face_locations = face_recognition.face_locations(rgb_frame, model="hog")
        
        return face_locations
    
    def get_largest_face(self, face_locations: List[Tuple[int, int, int, int]]) -> Optional[Tuple[int, int, int, int]]:
        """
        Select the largest face by bounding box area.
        
        Args:
            face_locations: List of (top, right, bottom, left) bounding boxes
            
        Returns:
            Largest face bounding box or None if no faces
        """
        if not face_locations:
            return None
        
        # Calculate area for each face and return the largest
        def face_area(face_loc: Tuple[int, int, int, int]) -> int:
            top, right, bottom, left = face_loc
            return (bottom - top) * (right - left)
        
        return max(face_locations, key=face_area)


# ══════════════════════════════════════════════════════════════════════════════
# Person Database Component
# ══════════════════════════════════════════════════════════════════════════════

class PersonDatabase:
    """
    Manages the JSON database of known faces with their encodings.
    
    The database stores face encodings (128-d vectors) for each person,
    supporting multiple encodings per person for different angles/expressions.
    """
    
    def __init__(self, database_path: Path):
        """
        Initialize PersonDatabase.
        
        Args:
            database_path: Path to the JSON database file
        """
        self.database_path = database_path
        self.data: Dict = {"version": "1.0", "people": []}
        self.load()
    
    def load(self) -> None:
        """Load database from disk, creating empty database if file doesn't exist."""
        if not self.database_path.exists():
            print(f"[INFO] Database not found. Creating new database at {self.database_path}", file=sys.stderr)
            self.database_path.parent.mkdir(parents=True, exist_ok=True)
            self.save()
            return
        
        try:
            with open(self.database_path, 'r') as f:
                self.data = json.load(f)
            
            # Validate schema
            if "people" not in self.data:
                raise ValueError("Invalid database schema: missing 'people' field")
            
            print(f"[INFO] Loaded database with {len(self.data['people'])} people", file=sys.stderr)
        
        except (json.JSONDecodeError, ValueError) as e:
            print(f"[ERROR] Corrupted database file: {e}", file=sys.stderr)
            print("[INFO] Creating new empty database", file=sys.stderr)
            self.data = {"version": "1.0", "people": []}
            self.save()
    
    def save(self) -> None:
        """Save database to disk atomically using temp file + rename."""
        self.database_path.parent.mkdir(parents=True, exist_ok=True)
        
        # Write to temporary file
        tmp_path = self.database_path.with_suffix('.tmp')
        with open(tmp_path, 'w') as f:
            json.dump(self.data, f, indent=2)
        
        # Atomic rename
        tmp_path.replace(self.database_path)
    
    def add_encoding(self, person_name: str, encoding: np.ndarray, gender: Optional[str] = None) -> None:
        """
        Add face encoding for a person.
        
        Args:
            person_name: Name of the person
            encoding: 128-d face encoding vector
            gender: Gender of the person ("male" or "female"), optional
        """
        # Find existing person or create new entry
        person_entry = None
        for person in self.data["people"]:
            if person["name"] == person_name:
                person_entry = person
                break
        
        if person_entry is None:
            # Create new person entry
            person_entry = {
                "person_id": str(uuid.uuid4()),
                "name": person_name,
                "gender": gender if gender else "unknown",
                "encodings": [],
                "created_at": datetime.utcnow().isoformat() + "Z",
                "updated_at": datetime.utcnow().isoformat() + "Z"
            }
            self.data["people"].append(person_entry)
        else:
            # Update existing person
            person_entry["updated_at"] = datetime.utcnow().isoformat() + "Z"
            # Update gender if provided
            if gender:
                person_entry["gender"] = gender
        
        # Add encoding (convert numpy array to list for JSON serialization)
        person_entry["encodings"].append(encoding.tolist())
    
    def get_all_encodings(self) -> List[Tuple[str, np.ndarray]]:
        """
        Get all (name, encoding) pairs for matching.
        
        Returns:
            List of (person_name, encoding) tuples
        """
        result = []
        for person in self.data["people"]:
            name = person["name"]
            for encoding_list in person["encodings"]:
                # Convert list back to numpy array
                encoding = np.array(encoding_list)
                result.append((name, encoding))
        return result
    
    def remove_person(self, person_name: str) -> bool:
        """
        Remove a person from the database.
        
        Args:
            person_name: Name of the person to remove
            
        Returns:
            True if person was found and removed, False otherwise
        """
        for i, person in enumerate(self.data["people"]):
            if person["name"] == person_name:
                del self.data["people"][i]
                return True
        return False
    
    def person_exists(self, person_name: str) -> bool:
        """
        Check if a person exists in the database.
        
        Args:
            person_name: Name to check
            
        Returns:
            True if person exists, False otherwise
        """
        return any(person["name"] == person_name for person in self.data["people"])
    
    def get_person_gender(self, person_name: str) -> Optional[str]:
        """
        Get the gender of a person from the database.
        
        Args:
            person_name: Name of the person
            
        Returns:
            Gender string ("male", "female", or "unknown") or None if person not found
        """
        for person in self.data["people"]:
            if person["name"] == person_name:
                return person.get("gender", "unknown")
        return None


# ══════════════════════════════════════════════════════════════════════════════
# Utility Functions
# ══════════════════════════════════════════════════════════════════════════════

def list_cameras(max_index: int = 6) -> None:
    """List available camera indices."""
    print("Available cameras:")
    for i in range(max_index):
        cap = cv2.VideoCapture(i)
        if cap.isOpened():
            print(f"  index {i}: OK")
            cap.release()
        else:
            print(f"  index {i}: not available")


def draw_preview(frame: np.ndarray, face_location: Optional[Tuple[int, int, int, int]],
                person_identity: str, recognition_conf: float,
                gender: str, gender_conf: float,
                enrollment_progress: Optional[Tuple[int, int]] = None) -> None:
    """
    Draw preview window with overlays.
    
    Args:
        frame: BGR image
        face_location: (top, right, bottom, left) or None
        person_identity: Recognized person name
        recognition_conf: Recognition confidence
        gender: Detected gender
        gender_conf: Gender confidence
        enrollment_progress: (current, target) samples or None
    """
    if face_location is not None:
        top, right, bottom, left = face_location
        
        # Draw bounding box
        cv2.rectangle(frame, (left, top), (right, bottom), (0, 255, 0), 2)
        
        # Draw person identity
        identity_text = f"{person_identity} ({recognition_conf:.2f})"
        cv2.putText(frame, identity_text, (left, top - 10),
                   cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
        
        # Draw gender
        gender_text = f"{gender} ({gender_conf:.2f})"
        cv2.putText(frame, gender_text, (left, bottom + 20),
                   cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 2)
    
    # Draw enrollment progress if in enrollment mode
    if enrollment_progress is not None:
        current, target = enrollment_progress
        progress_text = f"Enrollment: {current}/{target} samples"
        cv2.putText(frame, progress_text, (10, 30),
                   cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 0), 2)
    
    cv2.imshow("Face Recognition & Gender Detection", frame)


# ══════════════════════════════════════════════════════════════════════════════
# Main Script
# ══════════════════════════════════════════════════════════════════════════════

def parse_args() -> argparse.Namespace:
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description="Face recognition and gender detection bridge"
    )
    
    # Detection mode options
    parser.add_argument("--camera-index", type=int, default=0,
                       help="Camera device index (default: 0)")
    parser.add_argument("--show-preview", action="store_true",
                       help="Display video preview window with overlays")
    parser.add_argument("--watch", action="store_true",
                       help="Run continuously until Ctrl+C")
    parser.add_argument("--interval", type=float, default=DEFAULT_INTERVAL,
                       help=f"Seconds between detections (default: {DEFAULT_INTERVAL})")
    parser.add_argument("--database", type=Path, default=DEFAULT_DATABASE,
                       help=f"Path to known faces database (default: {DEFAULT_DATABASE})")
    parser.add_argument("--recognition-threshold", type=float, default=RECOGNITION_CONFIDENCE_THRESHOLD,
                       help=f"Minimum confidence for recognition (default: {RECOGNITION_CONFIDENCE_THRESHOLD})")
    parser.add_argument("--gender-threshold", type=float, default=GENDER_CONFIDENCE_THRESHOLD,
                       help=f"Minimum confidence for gender (default: {GENDER_CONFIDENCE_THRESHOLD})")
    
    # Enrollment mode options
    parser.add_argument("--enroll-person", action="store_true",
                       help="Activate enrollment mode")
    parser.add_argument("--person-name", type=str,
                       help="Name of person to enroll (required with --enroll-person)")
    parser.add_argument("--gender", type=str, choices=["male", "female"],
                       help="Gender of person to enroll: 'male' or 'female' (required with --enroll-person)")
    parser.add_argument("--samples", type=int, default=DEFAULT_ENROLLMENT_SAMPLES,
                       help=f"Number of encoding samples to capture (default: {DEFAULT_ENROLLMENT_SAMPLES})")
    
    # Utility options
    parser.add_argument("--list-cameras", action="store_true",
                       help="List available camera indices and exit")
    
    return parser.parse_args()


def run_enrollment(args: argparse.Namespace) -> int:
    """
    Run enrollment mode to add a new person to the database.
    
    Args:
        args: Parsed command-line arguments
        
    Returns:
        Exit code (0 for success, 1 for error)
    """
    if not args.person_name:
        print("[ERROR] --person-name is required with --enroll-person", file=sys.stderr)
        return 1
    
    if not args.gender:
        print("[ERROR] --gender is required with --enroll-person (choices: 'male', 'female')", file=sys.stderr)
        return 1
    
    print(f"[INFO] Starting enrollment for '{args.person_name}' (gender: {args.gender})", flush=True)
    print(f"[INFO] Will capture {args.samples} face samples from different angles", flush=True)
    
    # Initialize components
    database = PersonDatabase(args.database)
    recognizer = FaceRecognizer(database)
    face_detector = FaceDetector()
    
    # Check for duplicate person
    if database.person_exists(args.person_name):
        response = input(f"[WARNING] Person '{args.person_name}' already exists. Overwrite? (y/n): ")
        if response.lower() != 'y':
            print("[INFO] Enrollment cancelled", flush=True)
            return 0
        database.remove_person(args.person_name)
    
    enrollment = EnrollmentMode(args.person_name, args.gender, recognizer, args.samples)
    
    # Open camera
    cap = cv2.VideoCapture(args.camera_index)
    if not cap.isOpened():
        print(f"[ERROR] Cannot open camera index {args.camera_index}", file=sys.stderr)
        return 1
    
    print("[INFO] Camera opened. Position your face in different angles.", flush=True)
    print("[INFO] Press 'q' to quit enrollment.", flush=True)
    
    try:
        while True:
            ret, frame = cap.read()
            if not ret:
                time.sleep(0.1)
                continue
            
            # Detect faces
            face_locations = face_detector.detect_faces(frame)
            largest_face = face_detector.get_largest_face(face_locations)
            
            if largest_face is not None:
                # Encode face
                encoding = recognizer.encode_face(frame, largest_face)
                
                if encoding is not None:
                    # Add sample
                    complete = enrollment.add_sample(encoding, largest_face)
                    
                    if complete:
                        # Enrollment complete
                        enrollment.save()
                        if args.show_preview:
                            cv2.destroyAllWindows()
                        cap.release()
                        return 0
            else:
                print("[INFO] No face detected. Please position your face in the camera.", flush=True)
            
            # Show preview
            if args.show_preview:
                draw_preview(frame, largest_face, "", 0.0, "", 0.0,
                           enrollment_progress=enrollment.get_progress())
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break
            
            time.sleep(0.2)  # Small delay between captures
    
    except KeyboardInterrupt:
        print("\n[INFO] Enrollment interrupted by user", flush=True)
    
    finally:
        cap.release()
        if args.show_preview:
            cv2.destroyAllWindows()
    
    return 0


def run_detection(args: argparse.Namespace) -> int:
    """
    Run detection mode for face recognition and gender retrieval.
    
    Args:
        args: Parsed command-line arguments
        
    Returns:
        Exit code (0 for success, 1 for error)
    """
    print("[INFO] Starting face recognition with manual gender retrieval", flush=True)
    
    # Initialize components
    database = PersonDatabase(args.database)
    recognizer = FaceRecognizer(database)
    face_detector = FaceDetector()
    gender_retriever = GenderRetriever(database)
    output_writer = OutputWriter(USER_JSON)
    
    # Initialize smoother (only for recognition, not gender since it's from database)
    recognition_smoother = ExponentialSmoother(RECOGNITION_ALPHA, RECOGNITION_STABILITY_WINDOW)
    
    # Open camera
    cap = cv2.VideoCapture(args.camera_index)
    if not cap.isOpened():
        print(f"[ERROR] Cannot open camera index {args.camera_index}", file=sys.stderr)
        output_writer.write_fallback()
        return 1
    
    print(f"[INFO] Camera opened. Writing to {USER_JSON}", flush=True)
    print("[INFO] Press 'q' to quit (if preview enabled) or Ctrl+C", flush=True)
    
    last_write_time = 0.0
    
    try:
        while True:
            ret, frame = cap.read()
            if not ret:
                time.sleep(0.1)
                continue
            
            now = time.time()
            
            # Detect faces
            face_locations = face_detector.detect_faces(frame)
            largest_face = face_detector.get_largest_face(face_locations)
            
            if largest_face is not None:
                # Face detected - perform recognition and get gender from database
                encoding = recognizer.encode_face(frame, largest_face)
                
                if encoding is not None:
                    # Recognize person
                    person_identity, recognition_conf = recognizer.recognize(encoding)
                    
                    # Apply smoothing to recognition
                    person_identity, recognition_conf = recognition_smoother.update(
                        person_identity, recognition_conf, now
                    )
                    
                    # Get gender from database (no smoothing needed, it's static data)
                    gender, gender_conf = gender_retriever.get_gender(person_identity)
                    
                    # Write output at interval
                    if now - last_write_time >= args.interval:
                        output_writer.write_detection(
                            person_identity, recognition_conf,
                            gender, gender_conf, True
                        )
                        last_write_time = now
                        
                        print(f"  [{person_identity.upper():15s}] recognition={recognition_conf:.2f} | "
                              f"[{gender.upper():8s}] gender={gender_conf:.2f}", flush=True)
                else:
                    # Encoding failed
                    person_identity, recognition_conf = "unknown", 0.0
                    gender, gender_conf = "unknown", 0.0
            else:
                # No face detected
                if now - last_write_time >= args.interval:
                    output_writer.write_fallback()
                    last_write_time = now
                    print("  [NO FACE]", flush=True)
                
                person_identity, recognition_conf = "unknown", 0.0
                gender, gender_conf = "unknown", 0.0
            
            # Show preview
            if args.show_preview:
                draw_preview(frame, largest_face, person_identity, recognition_conf,
                           gender, gender_conf)
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break
            
            # Exit if not in watch mode
            if not args.watch:
                break
    
    except KeyboardInterrupt:
        print("\n[INFO] Stopped by user", flush=True)
    
    finally:
        cap.release()
        if args.show_preview:
            cv2.destroyAllWindows()
        output_writer.write_fallback()
        
        # Delete output file on exit
        if USER_JSON.exists():
            USER_JSON.unlink()
            print(f"[INFO] Deleted {USER_JSON}", flush=True)
    
    return 0


if __name__ == "__main__":
    args = parse_args()
    
    # Handle utility commands
    if args.list_cameras:
        list_cameras()
        sys.exit(0)
    
    # Run enrollment or detection mode
    if args.enroll_person:
        exit_code = run_enrollment(args)
    else:
        exit_code = run_detection(args)
    
    sys.exit(exit_code)

