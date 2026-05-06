"""
face_emotion_bridge.py
Captures facial expressions via MediaPipe FaceMesh + FER classifier,
writes .runtime/current_emotion.json for TuioDemo to read.

Usage:
    py -3.9 face_emotion_bridge.py [--camera-index 0] [--interval 0.5] [--show-preview] [--watch]

Requirements:
    py -3.9 -m pip install mediapipe==0.10.21 fer opencv-python
"""

import argparse
import json
import os
import sys
import time
import threading
from datetime import datetime
from pathlib import Path

try:
    import cv2
except ImportError:
    print("[ERROR] opencv-python not installed. Run: py -3.9 -m pip install opencv-python", file=sys.stderr)
    sys.exit(1)

try:
    import mediapipe as mp
except ImportError:
    print("[ERROR] mediapipe not installed. Run: py -3.9 -m pip install mediapipe==0.10.21", file=sys.stderr)
    sys.exit(1)

try:
    from fer import FER
except ImportError:
    print("[ERROR] fer not installed. Run: py -3.9 -m pip install fer", file=sys.stderr)
    sys.exit(1)

# ── Runtime output path ──────────────────────────────────────────────────────
RUNTIME_DIR = Path(".runtime")
EMOTION_JSON = RUNTIME_DIR / "current_emotion.json"

EMOTION_LABELS = ["angry", "disgust", "fear", "happy", "sad", "surprise", "neutral"]

# Maps FER emotion → adaptive UI hint consumed by TuioDemo
ADAPTIVE_HINT = {
    "angry":    "frustrated",
    "disgust":  "frustrated",
    "fear":     "confused",
    "happy":    "engaged",
    "sad":      "disengaged",
    "surprise": "interested",
    "neutral":  "neutral",
}


def write_emotion(emotion: str, confidence: float, scores: dict, face_detected: bool):
    RUNTIME_DIR.mkdir(exist_ok=True)
    payload = {
        "emotion":       emotion,
        "confidence":    round(confidence, 4),
        "adaptive_hint": ADAPTIVE_HINT.get(emotion, "neutral"),
        "face_detected": face_detected,
        "scores":        {k: round(v, 4) for k, v in scores.items()},
        "timestamp":     datetime.utcnow().isoformat() + "Z",
    }
    tmp = EMOTION_JSON.with_suffix(".tmp")
    tmp.write_text(json.dumps(payload, indent=2))
    tmp.replace(EMOTION_JSON)


def write_no_face():
    write_emotion("neutral", 0.0, {e: 0.0 for e in EMOTION_LABELS}, face_detected=False)


def run(camera_index: int, interval: float, show_preview: bool, watch: bool):
    print(f"[face_emotion_bridge] Starting on camera index {camera_index}", flush=True)

    cap = cv2.VideoCapture(camera_index)
    if not cap.isOpened():
        print(f"[ERROR] Cannot open camera index {camera_index}. Use --list-cameras to check.", file=sys.stderr)
        write_no_face()
        sys.exit(1)

    detector = FER(mtcnn=False)          # mtcnn=True is more accurate but slower

    mp_face = mp.solutions.face_detection
    face_detect = mp_face.FaceDetection(model_selection=0, min_detection_confidence=0.5)

    print("[face_emotion_bridge] Running. Press Ctrl+C to stop.", flush=True)

    last_write = 0.0
    smoothed: dict = {e: 0.0 for e in EMOTION_LABELS}
    alpha = 0.4  # EMA smoothing factor

    try:
        while True:
            ret, frame = cap.read()
            if not ret:
                time.sleep(0.1)
                continue

            now = time.monotonic()
            if now - last_write < interval:
                if show_preview:
                    _draw_preview(frame, smoothed)
                    if cv2.waitKey(1) & 0xFF == 27:
                        break
                continue

            last_write = now

            # ── FER emotion detection ────────────────────────────────────────
            rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = detector.detect_emotions(frame)

            if results:
                # Use the largest detected face
                best = max(results, key=lambda r: r["box"][2] * r["box"][3])
                raw_scores = best["emotions"]
                # EMA smooth
                for em in EMOTION_LABELS:
                    smoothed[em] = alpha * raw_scores.get(em, 0.0) + (1 - alpha) * smoothed[em]

                top_emotion = max(smoothed, key=smoothed.get)
                confidence  = smoothed[top_emotion]
                write_emotion(top_emotion, confidence, smoothed, face_detected=True)
                print(f"  [{top_emotion.upper():9s}] conf={confidence:.2f}", flush=True)
            else:
                write_no_face()
                print("  [NO FACE]", flush=True)

            if show_preview:
                _draw_preview(frame, smoothed, results)
                if cv2.waitKey(1) & 0xFF == 27:
                    break

            if not watch:
                break

    except KeyboardInterrupt:
        print("\n[face_emotion_bridge] Stopped by user.", flush=True)
    finally:
        cap.release()
        if show_preview:
            cv2.destroyAllWindows()
        write_no_face()


def _draw_preview(frame, smoothed: dict, results=None):
    if results:
        for face in results:
            x, y, w, h = face["box"]
            cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 255, 0), 2)
    y0 = 20
    for em, val in sorted(smoothed.items(), key=lambda kv: -kv[1]):
        bar = int(val * 150)
        cv2.rectangle(frame, (10, y0 - 10), (10 + bar, y0 + 2), (100, 200, 100), -1)
        cv2.putText(frame, f"{em}: {val:.2f}", (10, y0), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (255, 255, 255), 1)
        y0 += 18
    cv2.imshow("Face Emotion Bridge", frame)


def list_cameras(max_index=6):
    print("Available cameras:")
    for i in range(max_index):
        cap = cv2.VideoCapture(i)
        if cap.isOpened():
            print(f"  index {i}: OK")
            cap.release()
        else:
            print(f"  index {i}: not available")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Facial expression → .runtime/current_emotion.json")
    parser.add_argument("--camera-index", type=int, default=0,  help="Camera index (default 0)")
    parser.add_argument("--interval",     type=float, default=0.5, help="Write interval in seconds (default 0.5)")
    parser.add_argument("--show-preview", action="store_true",  help="Show OpenCV preview window")
    parser.add_argument("--watch",        action="store_true",  help="Run continuously (required while TuioDemo runs)")
    parser.add_argument("--list-cameras", action="store_true",  help="List available camera indices and exit")
    args = parser.parse_args()

    if args.list_cameras:
        list_cameras()
        sys.exit(0)

    run(args.camera_index, args.interval, args.show_preview, args.watch)