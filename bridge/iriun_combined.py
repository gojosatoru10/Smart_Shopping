#!/usr/bin/env python3
"""
iriun_combined.py
Single-camera bridge that runs THREE computer vision systems simultaneously:
  1. Hand tracking (TUIO) - gesture recognition and cursor control
  2. Face emotion detection - emotional state analysis
  3. Face recognition - person identification with gender

Solves the "two processes can't share one camera" limitation on Windows.

Outputs:
  - TUIO /tuio/2Dcur cursors and /tuio/2Dobj swipe bursts -> UDP port (default 3333)
  - .runtime/current_emotion.json (every face_interval seconds)
  - .runtime/face_detection.json (every recognition_interval seconds)
  - .runtime/tuio_port.json (selected port)

Run example:
  python bridge/iriun_combined.py --camera-index 1 --tuio-port 3333 --show-preview
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import time
from datetime import datetime
from pathlib import Path
from typing import Dict, Optional, Tuple

import cv2
import numpy as np

THIS_DIR = Path(__file__).resolve().parent
REPO_ROOT = THIS_DIR.parent
sys.path.insert(0, str(THIS_DIR))

import hand_tuio_bridge as hb  # type: ignore  # reuse classes/helpers


try:
    import mediapipe as mp
except ImportError:
    print("[ERROR] mediapipe not installed. Run: python -m pip install mediapipe==0.10.21", file=sys.stderr)
    sys.exit(1)

try:
    from fer import FER
except ImportError:
    print("[ERROR] fer not installed. Run: python -m pip install fer==22.4.0 'tensorflow<2.16'", file=sys.stderr)
    sys.exit(1)

try:
    import face_recognition
except ImportError:
    print("[ERROR] face_recognition not installed. Run: python -m pip install face-recognition", file=sys.stderr)
    sys.exit(1)


RUNTIME_DIR = REPO_ROOT / ".runtime"
EMOTION_JSON = RUNTIME_DIR / "current_emotion.json"
USER_JSON = RUNTIME_DIR / "face_detection.json"
LOGIN_MODE_JSON = RUNTIME_DIR / "login_mode.json"
DEFAULT_PORT_FILE = RUNTIME_DIR / "tuio_port.json"
DEFAULT_DATABASE = REPO_ROOT / "models" / "known_faces.json"

EMOTION_LABELS = ["angry", "disgust", "fear", "happy", "sad", "surprise", "neutral"]
ADAPTIVE_HINT = {
    "angry": "frustrated",
    "disgust": "frustrated",
    "fear": "confused",
    "happy": "engaged",
    "sad": "disengaged",
    "surprise": "interested",
    "neutral": "neutral",
}

# Face recognition thresholds
RECOGNITION_DISTANCE_THRESHOLD = 0.4
RECOGNITION_CONFIDENCE_THRESHOLD = 0.6


def write_emotion(emotion: str, confidence: float, scores: dict, face_detected: bool) -> None:
    RUNTIME_DIR.mkdir(exist_ok=True)
    payload = {
        "emotion": emotion,
        "confidence": round(float(confidence), 4),
        "adaptive_hint": ADAPTIVE_HINT.get(emotion, "neutral"),
        "face_detected": bool(face_detected),
        "scores": {k: round(float(v), 4) for k, v in scores.items()},
        "timestamp": datetime.utcnow().isoformat() + "Z",
    }
    tmp = EMOTION_JSON.with_suffix(".tmp")
    tmp.write_text(json.dumps(payload, indent=2))
    tmp.replace(EMOTION_JSON)


def write_no_face() -> None:
    write_emotion("neutral", 0.0, {e: 0.0 for e in EMOTION_LABELS}, face_detected=False)


def write_face_recognition(person_identity: str, recognition_conf: float, gender: str, gender_conf: float, face_detected: bool, login_success: bool = False) -> None:
    """Write face recognition results to JSON."""
    RUNTIME_DIR.mkdir(exist_ok=True)
    payload = {
        "person_identity": person_identity,
        "recognition_confidence": round(float(recognition_conf), 4),
        "gender": gender,
        "gender_confidence": round(float(gender_conf), 4),
        "face_detected": bool(face_detected),
        "login_success": bool(login_success),
        "timestamp": datetime.utcnow().isoformat() + "Z",
    }
    tmp = USER_JSON.with_suffix(".tmp")
    tmp.write_text(json.dumps(payload, indent=2))
    tmp.replace(USER_JSON)


def write_no_face_recognition() -> None:
    """Write no-face-detected state for face recognition."""
    write_face_recognition("unknown", 0.0, "unknown", 0.0, face_detected=False, login_success=False)


def is_login_mode_active() -> bool:
    """Check if login mode is active by reading login_mode.json."""
    try:
        if not LOGIN_MODE_JSON.exists():
            return False
        
        # Read file with utf-8-sig to handle BOM from C# WriteAllText
        content = LOGIN_MODE_JSON.read_text(encoding='utf-8-sig').strip()
        if not content:
            return False
        
        data = json.loads(content)
        active = data.get("active", False)
        
        return active
    except json.JSONDecodeError as e:
        print(f"[login_mode] JSON decode error: {e}", flush=True)
        return False
    except Exception as e:
        print(f"[login_mode] Unexpected error: {e}", flush=True)
        return False


class PersonDatabase:
    """Manages the JSON database of known faces."""
    def __init__(self, database_path: Path):
        self.database_path = database_path
        self.data = {"version": "1.0", "people": []}
        self.load()
    
    def load(self) -> None:
        if not self.database_path.exists():
            return
        try:
            with open(self.database_path, 'r') as f:
                self.data = json.load(f)
        except Exception as e:
            print(f"[WARNING] Failed to load face database: {e}", file=sys.stderr)
    
    def get_all_encodings(self) -> list:
        """Get all (name, encoding) pairs for matching."""
        result = []
        for person in self.data.get("people", []):
            name = person["name"]
            for encoding_list in person.get("encodings", []):
                encoding = np.array(encoding_list)
                result.append((name, encoding))
        return result
    
    def get_person_gender(self, person_name: str) -> Optional[str]:
        """Get the gender of a person from the database."""
        for person in self.data.get("people", []):
            if person["name"] == person_name:
                return person.get("gender", "unknown")
        return None


class FaceRecognizer:
    """Identifies specific individuals by matching face encodings."""
    def __init__(self, database: PersonDatabase):
        self.database = database
    
    def recognize_face(self, frame: np.ndarray, face_location: Tuple[int, int, int, int]) -> Tuple[str, float, str, float]:
        """
        Recognize face and return (person_name, recognition_conf, gender, gender_conf).
        Returns ("unknown", 0.0, "unknown", 0.0) if no match.
        """
        try:
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            encodings = face_recognition.face_encodings(rgb_frame, [face_location])
            
            if not encodings:
                return ("unknown", 0.0, "unknown", 0.0)
            
            face_encoding = encodings[0]
            known_encodings = self.database.get_all_encodings()
            
            if not known_encodings:
                return ("unknown", 0.0, "unknown", 0.0)
            
            known_names = [name for name, _ in known_encodings]
            known_face_encodings = [encoding for _, encoding in known_encodings]
            
            distances = face_recognition.face_distance(known_face_encodings, face_encoding)
            min_distance_idx = np.argmin(distances)
            min_distance = distances[min_distance_idx]
            
            if min_distance < RECOGNITION_DISTANCE_THRESHOLD:
                confidence = max(0.0, 1.0 - min_distance)
                person_name = known_names[min_distance_idx]
                gender = self.database.get_person_gender(person_name) or "unknown"
                gender_conf = 1.0 if gender in ["male", "female"] else 0.0
                return (person_name, confidence, gender, gender_conf)
            else:
                return ("unknown", 0.0, "unknown", 0.0)
        
        except Exception as e:
            print(f"[WARNING] Face recognition failed: {e}", file=sys.stderr)
            return ("unknown", 0.0, "unknown", 0.0)


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Iriun combined hand+face bridge")
    p.add_argument("--camera-index", type=int, default=1)
    p.add_argument("--tuio-host", type=str, default="127.0.0.1")
    p.add_argument("--tuio-port", type=str, default="3333", help="UDP port or 'auto'")
    p.add_argument("--port-file", type=Path, default=DEFAULT_PORT_FILE)
    p.add_argument("--send-fps", type=float, default=30.0)
    p.add_argument("--gesture-cooldown", type=float, default=0.6)
    p.add_argument("--threshold", type=float, default=0.05)
    p.add_argument("--window-size", type=int, default=45)
    p.add_argument("--model-path", type=Path, default=REPO_ROOT / "models" / "gesture_recognizer_dollarpy.pth")
    p.add_argument("--no-gesture", action="store_true")
    p.add_argument("--show-preview", action="store_true")
    p.add_argument("--use-dshow", action="store_true")
    p.add_argument("--wait-for-camera", action="store_true")
    p.add_argument("--wait-timeout-sec", type=float, default=300.0)
    p.add_argument("--wait-poll-sec", type=float, default=2.0)
    p.add_argument("--face-interval", type=float, default=0.7,
                   help="Seconds between face emotion classifications (saves CPU)")
    p.add_argument("--recognition-interval", type=float, default=1.0,
                   help="Seconds between face recognition checks (saves CPU)")
    p.add_argument("--database", type=Path, default=DEFAULT_DATABASE,
                   help=f"Path to known faces database (default: {DEFAULT_DATABASE})")
    p.add_argument("--list-cameras", action="store_true")
    return p.parse_args()


def _open_camera_with_fallback(args) -> tuple:
    """
    Try to open args.camera_index. If it times out or delivers only black frames,
    fall back through all available indices (0-4) and use the first live one.
    Returns (VideoCapture, actual_index_used).
    """
    FALLBACK_INDICES = list(dict.fromkeys(
        [args.camera_index] + [i for i in range(5) if i != args.camera_index]
    ))

    for idx in FALLBACK_INDICES:
        is_preferred = (idx == args.camera_index)
        timeout = args.wait_timeout_sec if (is_preferred and args.wait_for_camera) else 5.0

        print(f"[camera] Trying index {idx} (timeout={timeout:.0f}s)...", flush=True)
        cap = hb.wait_for_live_camera(idx, args.use_dshow, timeout, args.wait_poll_sec)

        if cap is not None:
            if idx != args.camera_index:
                print(
                    f"[camera] WARNING: requested index {args.camera_index} not live. "
                    f"Falling back to index {idx} (laptop camera).",
                    flush=True,
                )
            else:
                print(f"[camera] Index {idx} is live.", flush=True)
            return cap, idx

        print(f"[camera] Index {idx} not live, trying next...", flush=True)

    print("ERROR: no live camera found on any index.", file=sys.stderr)
    raise SystemExit(1)


def main() -> int:
    args = parse_args()

    if args.list_cameras:
        hb.list_available_cameras(args.use_dshow)
        return 0

    try:
        target_port = hb.resolve_target_port(args.tuio_host, args.tuio_port)
    except Exception as exc:
        print(f"ERROR failed to resolve target port: {exc}", file=sys.stderr)
        return 1


    gestures_enabled = not args.no_gesture
    recognizer: Optional[hb.GestureRecognizer] = None
    if gestures_enabled:
        try:
            recognizer = hb.GestureRecognizer(args.model_path, args.threshold, args.window_size)
        except Exception as exc:
            print("WARNING failed to load gesture model. Running cursor-only.", file=sys.stderr)
            print(f"DETAILS: {exc}", file=sys.stderr)
            gestures_enabled = False

    cursor_sender = hb.TuioCursorSender(args.tuio_host, target_port)
    object_sender = hb.TuioObjectSender(args.tuio_host, target_port)
    nav_state = hb.NavigationState()

    hands = hb.mp_hands.Hands(
        model_complexity=1,
        min_detection_confidence=0.5,
        min_tracking_confidence=0.5,
        max_num_hands=2,
    )

    state_by_side: Dict[str, hb.HandState] = {
        "left": hb.HandState(side="left", session_id=hb.LEFT_SESSION_ID, stroke_id=1),
        "right": hb.HandState(side="right", session_id=hb.RIGHT_SESSION_ID, stroke_id=101),
    }

    print("Initializing FER (first run downloads model)...", flush=True)
    fer_detector = FER(mtcnn=False)
    smoothed: Dict[str, float] = {e: 0.0 for e in EMOTION_LABELS}
    ema_alpha = 0.4
    last_face_ts = 0.0
    
    print("Initializing face recognition...", flush=True)
    database = PersonDatabase(args.database)
    face_recognizer = FaceRecognizer(database)
    last_recognition_ts = 0.0
    login_mode_was_active = False  # Track login mode state changes

    cap, args.camera_index = _open_camera_with_fallback(args)

    hb.write_port_file(args.port_file, args.tuio_host, target_port)
    print(f"TUIO_HOST={args.tuio_host}")
    print(f"TUIO_PORT={target_port}")
    print(f"TUIO_PORT_FILE={args.port_file}")
    print(f"EMOTION_JSON={EMOTION_JSON}")
    print(f"FACE_RECOGNITION_JSON={USER_JSON}")
    print(f"FACE_DATABASE={args.database}")
    print("Ready: hand TUIO + face emotion + face recognition all running on the same camera.", flush=True)

    # Initialize face recognition state file
    write_no_face_recognition()
    print("[face_recognition] Initialized face_detection.json", flush=True)

    frame_period = 1.0 / max(1e-6, args.send_fps)
    last_sent = 0.0
    last_frame_ts = time.time()

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                print("[camera] Failed to read frame, retrying...", flush=True)
                time.sleep(0.1)
                continue

            # Iriun mirrors the image; flip horizontally so left/right hands are correct.
            # The laptop built-in camera (index 0) does not need flipping.
            if args.camera_index != 0:
                frame = cv2.flip(frame, 1)

            now = time.time()
            dt = max(1e-6, now - last_frame_ts)
            last_frame_ts = now

            for st in state_by_side.values():
                st.position = None

            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = hands.process(rgb)

            if results.multi_hand_landmarks and results.multi_handedness:
                for landmarks, handedness in zip(results.multi_hand_landmarks, results.multi_handedness):
                    side_name = handedness.classification[0].label.strip().lower()
                    if side_name not in state_by_side:
                        continue
                    st = state_by_side[side_name]
                    x, y = hb.extract_index_tip(landmarks)
                    st.position = (x, y)
                    st.trajectory.append(hb.Point(x, y, st.stroke_id))
                    hb.update_kinematics(st, dt)
                    if gestures_enabled and recognizer is not None:
                        ge = recognizer.classify(list(st.trajectory))
                        hb.maybe_trigger_burst(st, ge, args.gesture_cooldown)
                        hb.maybe_trigger_navigation(nav_state, ge, args.gesture_cooldown)

            # Face emotion detection
            if now - last_face_ts >= args.face_interval:
                last_face_ts = now
                try:
                    detections = fer_detector.detect_emotions(frame)
                except Exception as exc:
                    detections = []
                    print(f"[face] detect error: {exc}", file=sys.stderr)
                if detections:
                    best = max(detections, key=lambda r: r["box"][2] * r["box"][3])
                    raw_scores = best["emotions"]
                    for em in EMOTION_LABELS:
                        smoothed[em] = ema_alpha * raw_scores.get(em, 0.0) + (1 - ema_alpha) * smoothed[em]
                    top_em = max(smoothed, key=smoothed.get)
                    write_emotion(top_em, smoothed[top_em], smoothed, face_detected=True)
                else:
                    write_no_face()
            
            # Face recognition (runs less frequently to save CPU)
            if now - last_recognition_ts >= args.recognition_interval:
                last_recognition_ts = now
                
                # Check if login mode is active
                login_mode_active = is_login_mode_active()
                
                # Detect login mode state change
                if login_mode_active != login_mode_was_active:
                    if login_mode_active:
                        print("[face_recognition] ✓ Login mode ACTIVATED - starting face recognition", flush=True)
                    else:
                        print("[face_recognition] ✗ Login mode DEACTIVATED - stopping face recognition (camera stays active)", flush=True)
                        write_no_face_recognition()
                    login_mode_was_active = login_mode_active
                
                # Only run face recognition if login mode is active
                if login_mode_active:
                    try:
                        # Detect faces using face_recognition library
                        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                        face_locations = face_recognition.face_locations(rgb_frame, model="hog")
                        
                        if face_locations:
                            # Use the largest face
                            largest_face = max(face_locations, key=lambda loc: (loc[2] - loc[0]) * (loc[1] - loc[3]))
                            person_name, recog_conf, gender, gender_conf = face_recognizer.recognize_face(frame, largest_face)
                            
                            # If a known person is recognized with good confidence, mark as login success
                            login_success = (person_name != "unknown" and recog_conf >= RECOGNITION_CONFIDENCE_THRESHOLD)
                            write_face_recognition(person_name, recog_conf, gender, gender_conf, face_detected=True, login_success=login_success)
                            
                            if login_success:
                                print(f"[face_recognition] ✓✓✓ LOGIN SUCCESS: {person_name} (confidence: {recog_conf:.2f}) ✓✓✓", flush=True)
                            else:
                                print(f"[face_recognition] Face detected: {person_name} (conf: {recog_conf:.2f})", flush=True)
                        else:
                            write_no_face_recognition()
                    except Exception as exc:
                        print(f"[recognition] error: {exc}", file=sys.stderr)
                        write_no_face_recognition()

            if args.show_preview:
                if results.multi_hand_landmarks:
                    for hl in results.multi_hand_landmarks:
                        hb.mp_drawing.draw_landmarks(
                            frame, hl, hb.mp_hands.HAND_CONNECTIONS,
                            hb.mp_drawing_styles.get_default_hand_landmarks_style(),
                            hb.mp_drawing_styles.get_default_hand_connections_style(),
                        )
                for st in state_by_side.values():
                    if st.position is None:
                        continue
                    px = int(st.position[0] * frame.shape[1])
                    py = int(st.position[1] * frame.shape[0])
                    cv2.circle(frame, (px, py), 8, (0, 255, 0), -1)
                top_em = max(smoothed, key=smoothed.get) if any(smoothed.values()) else "neutral"
                cv2.putText(frame, f"emotion: {top_em} ({smoothed[top_em]:.2f})",
                            (10, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), 2)
                
                # Draw face recognition info if available
                try:
                    if USER_JSON.exists():
                        with open(USER_JSON, 'r') as f:
                            recog_data = json.load(f)
                        if recog_data.get("face_detected"):
                            person = recog_data.get("person_identity", "unknown")
                            gender = recog_data.get("gender", "unknown")
                            cv2.putText(frame, f"person: {person} ({gender})",
                                        (10, 50), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 0, 255), 2)
                except:
                    pass

            if now - last_sent < frame_period:
                if args.show_preview:
                    cv2.imshow("iriun_combined (q to quit)", frame)
                    if cv2.waitKey(1) & 0xFF == ord("q"):
                        break
                continue

            payload: Dict[int, Tuple[float, float, float, float, float]] = {}
            for st in state_by_side.values():
                data = hb.cursor_output(st)
                if data is not None:
                    payload[st.session_id] = data
            cursor_sender.send_frame(payload)

            if nav_state.active and nav_state.frames_remaining > 0:
                angle = hb.SWIPE_RIGHT_ANGLE_RAD if nav_state.direction == "right" else hb.SWIPE_LEFT_ANGLE_RAD
                obj_payload = {
                    hb.NAV_OBJECT_SESSION_ID: (hb.NAV_OBJECT_SYMBOL_ID, 0.5, 0.5, angle, 0.0, 0.0, 0.0, 0.0, 0.0)
                }
                object_sender.send_frame(obj_payload)
                nav_state.frames_remaining -= 1
            elif nav_state.active and nav_state.frames_remaining <= 0:
                object_sender.send_empty()
                nav_state.active = False
                nav_state.direction = ""

            last_sent = now

            if args.show_preview:
                cv2.imshow("iriun_combined (q to quit)", frame)
                if cv2.waitKey(1) & 0xFF == ord("q"):
                    break

    except KeyboardInterrupt:
        pass
    finally:
        cap.release()
        hands.close()
        if args.show_preview:
            cv2.destroyAllWindows()
        write_no_face()
        write_no_face_recognition()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
