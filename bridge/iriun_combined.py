#!/usr/bin/env python3
"""
iriun_combined.py
Single-camera bridge that runs hand tracking (TUIO) AND face emotion (JSON)
on one Iriun camera frame stream. Solves the "two processes can't share one
camera" limitation on Windows.

Outputs:
  - TUIO /tuio/2Dcur cursors and /tuio/2Dobj swipe bursts -> UDP port (default 3333)
  - .runtime/current_emotion.json (every face_interval seconds)
  - .runtime/tuio_port.json (selected port)

Run example:
  python .\bridge\iriun_combined.py --camera-index 1 --tuio-port 3333 --show-preview
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

THIS_DIR = Path(__file__).resolve().parent
REPO_ROOT = THIS_DIR.parent
sys.path.insert(0, str(THIS_DIR))

import hand_tuio_bridge as hb  # type: ignore  # reuse classes/helpers
from clothing_detection import ClothingDetector


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


RUNTIME_DIR = REPO_ROOT / ".runtime"
EMOTION_JSON = RUNTIME_DIR / "current_emotion.json"
DEFAULT_PORT_FILE = RUNTIME_DIR / "tuio_port.json"

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
    p.add_argument(
        "--disable-clothing-detection",
        action="store_true",
        help="Disable YOLO clothing detection output for Outfit Builder auto-category select",
    )
    p.add_argument(
        "--clothing-model-path",
        type=Path,
        default=REPO_ROOT / "YOLO26m Model" / "weights" / "best.pt",
        help="Path to YOLO weights (.pt) for clothing detection",
    )
    p.add_argument(
        "--clothing-output-file",
        type=Path,
        default=RUNTIME_DIR / "current_clothing.json",
        help="Path to write latest clothing detection JSON",
    )
    p.add_argument("--clothing-interval", type=float, default=0.5)
    p.add_argument("--clothing-min-conf", type=float, default=0.35)
    p.add_argument("--list-cameras", action="store_true")
    return p.parse_args()


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

    if args.wait_for_camera:
        cap = hb.wait_for_live_camera(args.camera_index, args.use_dshow, args.wait_timeout_sec, args.wait_poll_sec)
        if cap is None:
            print("ERROR: timeout waiting for camera.", file=sys.stderr)
            return 1
    else:
        cap = hb._open_capture(args.camera_index, args.use_dshow)
        if not cap.isOpened():
            print(f"ERROR cannot open camera {args.camera_index}", file=sys.stderr)
            return 1
        ok, _ = cap.read()
        if not ok:
            print("ERROR camera opened but no frames. Try --wait-for-camera.", file=sys.stderr)
            cap.release()
            return 1

    hb.write_port_file(args.port_file, args.tuio_host, target_port)
    print(f"TUIO_HOST={args.tuio_host}")
    print(f"TUIO_PORT={target_port}")
    print(f"TUIO_PORT_FILE={args.port_file}")
    print(f"FACE_JSON={EMOTION_JSON}")

    clothing_detector: Optional[ClothingDetector] = None
    if not args.disable_clothing_detection:
        clothing_detector = ClothingDetector(
            model_path=args.clothing_model_path,
            output_path=args.clothing_output_file,
            min_confidence=args.clothing_min_conf,
            interval_sec=args.clothing_interval,
        )
        print(f"CLOTHING_JSON={args.clothing_output_file}")

    print("Ready: hand TUIO + face emotion both running on the same camera.", flush=True)

    frame_period = 1.0 / max(1e-6, args.send_fps)
    last_sent = 0.0
    last_frame_ts = time.time()
    clothing_nav_lock_until = 0.0
    clothing_lock_was_active = False
    clothing_detection_latched = False

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                continue

            if args.camera_index != 1:
                frame = cv2.flip(frame, 1)

            now = time.time()
            dt = max(1e-6, now - last_frame_ts)
            last_frame_ts = now

            if clothing_detector is not None:
                clothing_detector.process_frame(frame, now)
                latest = clothing_detector.last_payload
                detected_now = latest.get("status") == "detected"
                if detected_now and not clothing_detection_latched:
                    clothing_nav_lock_until = now + max(args.gesture_cooldown, 0.8)
                    clothing_detection_latched = True
                elif not detected_now:
                    clothing_detection_latched = False
            clothing_nav_lock_active = now < clothing_nav_lock_until
            if clothing_nav_lock_active and not clothing_lock_was_active:
                object_sender.send_empty()
                nav_state.active = False
                nav_state.direction = ""
                nav_state.frames_remaining = 0
            clothing_lock_was_active = clothing_nav_lock_active

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
                    hb.update_kinematics(st, dt)
                    st.trajectory.append(hb.Point(x, y, st.stroke_id))
                    if gestures_enabled and recognizer is not None:
                        ge = recognizer.classify(list(st.trajectory))
                        hb.maybe_trigger_burst(st, ge, args.gesture_cooldown)
                        if not clothing_nav_lock_active:
                            hb.maybe_trigger_navigation(nav_state, ge, args.gesture_cooldown)

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
                if clothing_detector is not None:
                    latest = clothing_detector.last_payload
                    if latest.get("status") == "detected":
                        txt = f"clothing: {latest.get('category')} ({float(latest.get('confidence', 0.0)):.2f})"
                    else:
                        txt = f"clothing: {latest.get('status', 'n/a')}"
                    cv2.putText(frame, txt, (10, 50), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (255, 180, 0), 2)

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

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
