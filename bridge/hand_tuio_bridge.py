#!/usr/bin/env python3
"""Realtime hand tracking to TUIO bridge.
Sends /tuio/2Dcur for hand cursors and /tuio/2Dobj for synthesized navigation (SymbolID 1)
when swipe gestures are detected, matching TuioDemo.cs page-switching logic.
Preview: MediaPipe hand landmarks (skeleton) plus fingertip dot + sid label when --show-preview.
Requires: Python 3.9+, mediapipe with solutions API (e.g. mediapipe==0.10.21), opencv-python,
dollarpy, python-osc, torch (optional for .pth load).
"""

from __future__ import annotations

import os as _os, sys as _sys
_sys.path.insert(0, _os.path.dirname(_os.path.abspath(__file__)))

import argparse
import json
import math
import os
import pickle
import socket
import sys
import time
from collections import deque
from dataclasses import dataclass, field
from pathlib import Path
from typing import Deque, Dict, List, Optional, Sequence, Tuple

import cv2
try:
    from mediapipe.python.solutions import drawing_styles as mp_drawing_styles
    from mediapipe.python.solutions import drawing_utils as mp_drawing
    from mediapipe.python.solutions import hands as mp_hands
except ModuleNotFoundError:
    import mediapipe as mp  # type: ignore

    mp_drawing_styles = mp.solutions.drawing_styles
    mp_drawing = mp.solutions.drawing_utils
    mp_hands = mp.solutions.hands
from dollarpy import Point, Recognizer, Template
from pythonosc.osc_bundle_builder import IMMEDIATELY, OscBundleBuilder
from pythonosc.osc_message_builder import OscMessageBuilder
from pythonosc.udp_client import UDPClient

from clothing_detection import ClothingDetector

LEFT_SESSION_ID = 1
RIGHT_SESSION_ID = 101
PORT_SCAN_START = 3333
PORT_SCAN_END = 65535
THIS_DIR = Path(__file__).resolve().parent
REPO_ROOT = THIS_DIR.parent

NAV_OBJECT_SESSION_ID = 1000
NAV_OBJECT_SYMBOL_ID = 1
SWIPE_RIGHT_ANGLE_RAD = 0.785398
SWIPE_LEFT_ANGLE_RAD = 5.32325
NAV_EMIT_FRAMES = 5


@dataclass
class GestureEvent:
    label: str
    score: float
    ts: float


@dataclass
class HandState:
    side: str
    session_id: int
    position: Optional[Tuple[float, float]] = None
    last_position: Optional[Tuple[float, float]] = None
    speed: Tuple[float, float] = (0.0, 0.0)
    accel: float = 0.0
    stroke_id: int = 1
    trajectory: Deque[Point] = field(default_factory=lambda: deque(maxlen=45))
    burst_queue: Deque[Tuple[float, float]] = field(default_factory=deque)
    last_gesture_ts: float = 0.0
    last_gesture_label: str = ""


@dataclass
class NavigationState:
    active: bool = False
    direction: str = ""
    frames_remaining: int = 0
    last_trigger_ts: float = 0.0


class GestureRecognizer:
    def __init__(self, model_path: Path, threshold: float, window_size: int) -> None:
        self.threshold = threshold
        self.window_size = window_size
        self.recognizer = self._load_recognizer(model_path)

    @staticmethod
    def _load_model_payload(model_path: Path):
        torch_err: Optional[Exception] = None
        try:
            import torch  # type: ignore

            return torch.load(model_path, map_location="cpu", weights_only=False)
        except Exception as exc:
            torch_err = exc

        try:
            with model_path.open("rb") as fh:
                header = fh.read(4)
            if header == b"PK\x03\x04" and torch_err is not None:
                raise RuntimeError(
                    "Model requires PyTorch to load. Install torch or run with --no-gesture."
                ) from torch_err
        except OSError:
            pass

        with model_path.open("rb") as fh:
            return pickle.load(fh)

    def _load_recognizer(self, model_path: Path) -> Recognizer:
        payload = self._load_model_payload(model_path)

        if isinstance(payload, Recognizer):
            return payload

        if isinstance(payload, dict):
            templates_raw = payload.get("templates") or payload.get("recognizer")
            if isinstance(payload.get("threshold"), (int, float)):
                self.threshold = float(payload["threshold"])
            if isinstance(payload.get("window_size"), int):
                self.window_size = int(payload["window_size"])

            templates = self._coerce_templates(templates_raw)
            if templates:
                return Recognizer(templates)

        raise ValueError(f"Unsupported model format in {model_path}")

    @staticmethod
    def _coerce_templates(raw) -> List[Template]:
        if raw is None:
            return []
        if isinstance(raw, list) and raw and isinstance(raw[0], Template):
            return raw

        templates: List[Template] = []
        if isinstance(raw, list):
            for item in raw:
                if isinstance(item, Template):
                    templates.append(item)
                    continue
                if isinstance(item, dict):
                    name = str(item.get("name") or item.get("label") or "unknown")
                    points_raw = item.get("points") or []
                    points = []
                    for p in points_raw:
                        if isinstance(p, Point):
                            points.append(p)
                        elif isinstance(p, (list, tuple)) and len(p) >= 2:
                            stroke = int(p[2]) if len(p) > 2 else 1
                            points.append(Point(float(p[0]), float(p[1]), stroke))
                    if points:
                        templates.append(Template(name, points))
        return templates

    def classify(self, points: Sequence[Point]) -> GestureEvent:
        if len(points) < self.window_size:
            return GestureEvent("unknown", 0.0, time.time())

        result = self.recognizer.recognize(list(points))
        label = "unknown"
        score = 0.0

        if isinstance(result, tuple) and len(result) >= 2:
            first = result[0]
            second = result[1]
            if isinstance(first, str):
                label = first
            elif hasattr(first, "name"):
                label = str(first.name)
            if isinstance(second, (float, int)):
                score = float(second)

        if score < self.threshold:
            label = "unknown"

        return GestureEvent(label, score, time.time())


class TuioCursorSender:
    def __init__(self, host: str, port: int) -> None:
        self.client = UDPClient(host, port)
        self.frame_seq = 0

    def send_frame(self, cursors: Dict[int, Tuple[float, float, float, float, float]]) -> None:
        self.frame_seq += 1
        builder = OscBundleBuilder(IMMEDIATELY)

        for session_id, data in cursors.items():
            x, y, xs, ys, accel = data
            msg = OscMessageBuilder(address="/tuio/2Dcur")
            msg.add_arg("set")
            msg.add_arg(int(session_id))
            msg.add_arg(float(x))
            msg.add_arg(float(y))
            msg.add_arg(float(xs))
            msg.add_arg(float(ys))
            msg.add_arg(float(accel))
            builder.add_content(msg.build())

        alive = OscMessageBuilder(address="/tuio/2Dcur")
        alive.add_arg("alive")
        for session_id in sorted(cursors.keys()):
            alive.add_arg(int(session_id))
        builder.add_content(alive.build())

        fseq = OscMessageBuilder(address="/tuio/2Dcur")
        fseq.add_arg("fseq")
        fseq.add_arg(int(self.frame_seq))
        builder.add_content(fseq.build())

        self.client.send(builder.build())


class TuioObjectSender:
    def __init__(self, host: str, port: int) -> None:
        self.client = UDPClient(host, port)
        self.frame_seq = 0

    def send_frame(
        self,
        objects: Dict[int, Tuple[int, float, float, float, float, float, float, float, float]],
    ) -> None:
        self.frame_seq += 1
        builder = OscBundleBuilder(IMMEDIATELY)

        for session_id, data in objects.items():
            class_id, x, y, angle, xs, ys, rs, ma, ra = data
            msg = OscMessageBuilder(address="/tuio/2Dobj")
            msg.add_arg("set")
            msg.add_arg(int(session_id))
            msg.add_arg(int(class_id))
            msg.add_arg(float(x))
            msg.add_arg(float(y))
            msg.add_arg(float(angle))
            msg.add_arg(float(xs))
            msg.add_arg(float(ys))
            msg.add_arg(float(rs))
            msg.add_arg(float(ma))
            msg.add_arg(float(ra))
            builder.add_content(msg.build())

        alive = OscMessageBuilder(address="/tuio/2Dobj")
        alive.add_arg("alive")
        for session_id in sorted(objects.keys()):
            alive.add_arg(int(session_id))
        builder.add_content(alive.build())

        fseq = OscMessageBuilder(address="/tuio/2Dobj")
        fseq.add_arg("fseq")
        fseq.add_arg(int(self.frame_seq))
        builder.add_content(fseq.build())

        self.client.send(builder.build())

    def send_empty(self) -> None:
        self.frame_seq += 1
        builder = OscBundleBuilder(IMMEDIATELY)

        alive = OscMessageBuilder(address="/tuio/2Dobj")
        alive.add_arg("alive")
        builder.add_content(alive.build())

        fseq = OscMessageBuilder(address="/tuio/2Dobj")
        fseq.add_arg("fseq")
        fseq.add_arg(int(self.frame_seq))
        builder.add_content(fseq.build())

        self.client.send(builder.build())


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def find_free_udp_port(host: str, start: int = PORT_SCAN_START, end: int = PORT_SCAN_END) -> int:
    for port in range(start, end + 1):
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            try:
                sock.bind((host, port))
                return port
            except OSError:
                continue
    raise RuntimeError(f"No free UDP port found in range {start}-{end}")


def write_port_file(path: Path, host: str, port: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "host": host,
        "port": port,
        "protocol": "tuio-osc-udp",
        "timestamp": int(time.time()),
        "pid": os.getpid(),
    }
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def normalize_label(label: str) -> str:
    return label.strip().lower().replace("-", "_").replace(" ", "_")


def extract_swipe_direction(label: str) -> Optional[str]:
    name = normalize_label(label)
    if "swipe_left" in name:
        return "left"
    if "swipe_right" in name:
        return "right"
    return None


def make_burst_offsets(label: str) -> List[Tuple[float, float]]:
    name = normalize_label(label)

    if "swipe_left" in name:
        return [(-0.03, 0.0), (-0.06, 0.0), (-0.09, 0.0), (-0.12, 0.0)]
    if "swipe_right" in name:
        return [(0.03, 0.0), (0.06, 0.0), (0.09, 0.0), (0.12, 0.0)]
    if "select" in name:
        return [(0.0, 0.01), (0.0, 0.03), (0.0, 0.01), (0.0, 0.0)]
    if "clockwise" in name:
        return [
            (0.02, 0.0),
            (0.04, 0.02),
            (0.02, 0.04),
            (0.0, 0.02),
            (0.02, 0.0),
        ]
    if "anticlockwise" in name or "counterclockwise" in name:
        return [
            (0.0, 0.02),
            (0.02, 0.04),
            (0.04, 0.02),
            (0.02, 0.0),
            (0.0, 0.02),
        ]

    return []


def extract_index_tip(hand_landmarks) -> Tuple[float, float]:
    index_tip = hand_landmarks.landmark[8]
    return clamp01(float(index_tip.x)), clamp01(float(index_tip.y))


def update_kinematics(state: HandState, dt: float) -> None:
    if state.position is None:
        return

    if state.last_position is None or dt <= 0:
        state.speed = (0.0, 0.0)
        state.accel = 0.0
        state.last_position = state.position
        return

    dx = state.position[0] - state.last_position[0]
    dy = state.position[1] - state.last_position[1]

    prev_speed = state.speed
    vx = dx / dt
    vy = dy / dt
    ax = (vx - prev_speed[0]) / dt
    ay = (vy - prev_speed[1]) / dt

    state.speed = (vx, vy)
    state.accel = math.sqrt(ax * ax + ay * ay)
    state.last_position = state.position


def maybe_trigger_burst(
    state: HandState,
    event: Optional[GestureEvent],
    cooldown_sec: float,
) -> None:
    if event is None:
        return

    now = time.time()

    if event.label == "unknown":
        return

    if now - state.last_gesture_ts < cooldown_sec and event.label == state.last_gesture_label:
        return

    offsets = make_burst_offsets(event.label)
    if not offsets:
        return

    state.burst_queue.clear()
    for offset in offsets:
        state.burst_queue.append(offset)

    state.last_gesture_ts = now
    state.last_gesture_label = event.label


def maybe_trigger_navigation(
    nav: NavigationState,
    event: Optional[GestureEvent],
    cooldown_sec: float,
) -> None:
    if event is None:
        return

    now = time.time()
    if now - nav.last_trigger_ts < cooldown_sec:
        return

    direction = extract_swipe_direction(event.label)
    if direction is None:
        return

    nav.active = True
    nav.direction = direction
    nav.frames_remaining = NAV_EMIT_FRAMES
    nav.last_trigger_ts = now
    print(f"NAV: {event.label} -> page {'next' if direction == 'right' else 'prev'}")


def cursor_output(state: HandState) -> Optional[Tuple[float, float, float, float, float]]:
    if state.position is None:
        return None

    x, y = state.position

    if state.burst_queue:
        ox, oy = state.burst_queue.popleft()
        x = clamp01(x + ox)
        y = clamp01(y + oy)

    sx, sy = state.speed
    return x, y, sx, sy, state.accel


def _open_capture(index: int, use_dshow: bool):
    if sys.platform == "win32" and use_dshow:
        return cv2.VideoCapture(index, cv2.CAP_DSHOW)
    return cv2.VideoCapture(index)


def _frame_looks_live(frame) -> bool:
    """Reject fully black / uninitialized frames (common before IRuin starts streaming)."""
    if frame is None or frame.size == 0:
        return False
    return float(frame.mean()) > 2.0


def list_available_cameras(use_dshow: bool) -> None:
    print("Probing camera indices 0-9 (may take a few seconds)...")
    for i in range(10):
        cap = _open_capture(i, use_dshow)
        if not cap.isOpened():
            print(f"  [{i}] not available")
            continue
        ok = False
        w = h = 0
        for _ in range(5):
            ok, frame = cap.read()
            if ok and frame is not None:
                h, w = frame.shape[:2]
                mean = float(frame.mean())
                live = _frame_looks_live(frame)
                print(f"  [{i}] OK  {w}x{h}  mean={mean:.1f}  {'live' if live else 'dark/static'}")
                break
            time.sleep(0.05)
        else:
            print(f"  [{i}] opened but no frames")
        cap.release()


def wait_for_live_camera(index: int, use_dshow: bool, timeout_sec: float, poll_sec: float) -> Optional[cv2.VideoCapture]:
    deadline = time.time() + timeout_sec
    while time.time() < deadline:
        cap = _open_capture(index, use_dshow)
        if cap.isOpened():
            for _ in range(15):
                ok, frame = cap.read()
                if ok and frame is not None and _frame_looks_live(frame):
                    print(f"Camera index {index}: streaming OK ({frame.shape[1]}x{frame.shape[0]})", flush=True)
                    return cap
                time.sleep(0.03)
        if cap.isOpened():
            cap.release()
        print(
            f"Waiting for camera index {index} (start IRuin on phone, same Wi‑Fi/USB)...",
            flush=True,
        )
        time.sleep(poll_sec)
    return None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Hand recognizer to TUIO bridge")
    parser.add_argument("--camera-index", type=int, default=0)
    parser.add_argument("--model-path", type=Path, default=Path("models/gesture_recognizer_dollarpy.pth"))
    parser.add_argument("--threshold", type=float, default=0.05)
    parser.add_argument("--window-size", type=int, default=45)
    parser.add_argument("--send-fps", type=float, default=30.0)
    parser.add_argument("--gesture-cooldown", type=float, default=0.6)
    parser.add_argument("--tuio-host", type=str, default="127.0.0.1")
    parser.add_argument("--tuio-port", type=str, default="50001", help="UDP target port or 'auto'")
    parser.add_argument(
        "--port-file",
        type=Path,
        default=Path(".runtime/tuio_port.json"),
        help="Path to write selected TUIO target details",
    )
    parser.add_argument("--show-preview", action="store_true")
    parser.add_argument("--no-gesture", action="store_true", help="Disable gesture burst mode")
    parser.add_argument(
        "--list-cameras",
        action="store_true",
        help="Print which camera indices open on this PC, then exit (use to pick IRuin index)",
    )
    parser.add_argument(
        "--use-dshow",
        action="store_true",
        help="Windows: use DirectShow backend (often more stable for virtual webcams)",
    )
    parser.add_argument(
        "--wait-for-camera",
        action="store_true",
        help="Do not use another camera: wait until this index delivers live video (e.g. IRuin app streaming)",
    )
    parser.add_argument(
        "--wait-timeout-sec",
        type=float,
        default=300.0,
        help="With --wait-for-camera, give up after this many seconds",
    )
    parser.add_argument(
        "--wait-poll-sec",
        type=float,
        default=2.0,
        help="Seconds between retries while waiting for camera",
    )
    parser.add_argument(
        "--disable-clothing-detection",
        action="store_true",
        help="Disable YOLO clothing detection output for Outfit Builder auto-category select",
    )
    parser.add_argument(
        "--clothing-model-path",
        type=Path,
        default=REPO_ROOT / "YOLO26m Model" / "weights" / "best.pt",
        help="Path to YOLO weights (.pt) for clothing detection",
    )
    parser.add_argument(
        "--clothing-output-file",
        type=Path,
        default=REPO_ROOT / ".runtime" / "current_clothing.json",
        help="Path to write latest clothing detection JSON",
    )
    parser.add_argument(
        "--clothing-interval",
        type=float,
        default=0.5,
        help="Seconds between YOLO inference runs",
    )
    parser.add_argument(
        "--clothing-min-conf",
        type=float,
        default=0.35,
        help="Minimum confidence required to emit a detected clothing category",
    )
    return parser.parse_args()


def resolve_target_port(host: str, port_arg: str) -> int:
    if port_arg.lower() == "auto":
        return find_free_udp_port(host)
    return int(port_arg)


def main() -> int:
    args = parse_args()

    if args.list_cameras:
        list_available_cameras(args.use_dshow)
        return 0

    try:
        target_port = resolve_target_port(args.tuio_host, args.tuio_port)
    except Exception as exc:
        print(f"ERROR failed to resolve target port: {exc}", file=sys.stderr)
        return 1

    recognizer: Optional[GestureRecognizer] = None
    gestures_enabled = not args.no_gesture
    if gestures_enabled:
        try:
            recognizer = GestureRecognizer(args.model_path, args.threshold, args.window_size)
        except Exception as exc:
            print(
                "WARNING failed to load recognizer model. Running cursor stream only (no gesture bursts).",
                file=sys.stderr,
            )
            print(f"DETAILS: {exc}", file=sys.stderr)
            gestures_enabled = False

    cursor_sender = TuioCursorSender(args.tuio_host, target_port)
    object_sender = TuioObjectSender(args.tuio_host, target_port)
    nav_state = NavigationState()

    hands = mp_hands.Hands(
        model_complexity=1,
        min_detection_confidence=0.5,
        min_tracking_confidence=0.5,
        max_num_hands=2,
    )

    state_by_side: Dict[str, HandState] = {
        "left": HandState(side="left", session_id=LEFT_SESSION_ID, stroke_id=1),
        "right": HandState(side="right", session_id=RIGHT_SESSION_ID, stroke_id=101),
    }

    if args.wait_for_camera:
        cap = wait_for_live_camera(
            args.camera_index,
            args.use_dshow,
            args.wait_timeout_sec,
            args.wait_poll_sec,
        )
        if cap is None:
            print("ERROR: timeout waiting for live camera stream.", file=sys.stderr)
            return 1
    else:
        cap = _open_capture(args.camera_index, args.use_dshow)
        if not cap.isOpened():
            print(f"ERROR failed to open camera index {args.camera_index}", file=sys.stderr)
            return 1
        ok, test = cap.read()
        if not ok or test is None:
            print("ERROR: camera opened but no frames. Try --list-cameras or --wait-for-camera.", file=sys.stderr)
            cap.release()
            return 1

    write_port_file(args.port_file, args.tuio_host, target_port)
    print(f"TUIO_HOST={args.tuio_host}")
    print(f"TUIO_PORT={target_port}")
    print(f"TUIO_PORT_FILE={args.port_file}")

    clothing_detector: Optional[ClothingDetector] = None
    if not args.disable_clothing_detection:
        clothing_detector = ClothingDetector(
            model_path=args.clothing_model_path,
            output_path=args.clothing_output_file,
            min_confidence=args.clothing_min_conf,
            interval_sec=args.clothing_interval,
        )
        print(f"CLOTHING_JSON={args.clothing_output_file}")

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
                    x, y = extract_index_tip(landmarks)
                    st.position = (x, y)
                    update_kinematics(st, dt)
                    st.trajectory.append(Point(x, y, st.stroke_id))

                    if gestures_enabled:
                        gesture_event = recognizer.classify(list(st.trajectory)) if recognizer else None
                        maybe_trigger_burst(st, gesture_event, args.gesture_cooldown)
                        if not clothing_nav_lock_active:
                            maybe_trigger_navigation(nav_state, gesture_event, args.gesture_cooldown)

            if args.show_preview:
                if results.multi_hand_landmarks:
                    for hand_landmarks in results.multi_hand_landmarks:
                        mp_drawing.draw_landmarks(
                            frame,
                            hand_landmarks,
                            mp_hands.HAND_CONNECTIONS,
                            mp_drawing_styles.get_default_hand_landmarks_style(),
                            mp_drawing_styles.get_default_hand_connections_style(),
                        )
                for st in state_by_side.values():
                    if st.position is None:
                        continue
                    px = int(st.position[0] * frame.shape[1])
                    py = int(st.position[1] * frame.shape[0])
                    cv2.circle(frame, (px, py), 8, (0, 255, 0), -1)
                    cv2.putText(
                        frame,
                        f"{st.side} sid={st.session_id}",
                        (px + 10, py + 10),
                        cv2.FONT_HERSHEY_SIMPLEX,
                        0.5,
                        (0, 255, 0),
                        1,
                        cv2.LINE_AA,
                    )
                if clothing_detector is not None:
                    latest = clothing_detector.last_payload
                    if latest.get("status") == "detected":
                        # Draw bounding box
                        box = latest.get("box")
                        if box is not None:
                            try:
                                bx, by, bw, bh = int(box[0]), int(box[1]), int(box[2]), int(box[3])
                                cv2.rectangle(frame, (bx, by), (bx + bw, by + bh), (0, 255, 255), 2)
                                label_txt = f"{latest.get('category')} {float(latest.get('confidence', 0.0)):.2f}"
                                (tw, th), _ = cv2.getTextSize(label_txt, cv2.FONT_HERSHEY_SIMPLEX, 0.6, 2)
                                cv2.rectangle(frame, (bx, by - th - 8), (bx + tw + 6, by), (0, 255, 255), -1)
                                cv2.putText(
                                    frame,
                                    label_txt,
                                    (bx + 3, by - 5),
                                    cv2.FONT_HERSHEY_SIMPLEX,
                                    0.6,
                                    (0, 0, 0),
                                    2,
                                    cv2.LINE_AA,
                                )
                            except Exception:
                                pass
                        # Status text top-left
                        txt = f"clothing: {latest.get('category')} ({float(latest.get('confidence', 0.0)):.2f})"
                    else:
                        txt = f"clothing: {latest.get('status', 'n/a')}"
                    cv2.putText(
                        frame,
                        txt,
                        (10, 25),
                        cv2.FONT_HERSHEY_SIMPLEX,
                        0.6,
                        (0, 255, 255),
                        2,
                        cv2.LINE_AA,
                    )

            if now - last_sent < frame_period:
                if args.show_preview:
                    cv2.imshow("hand_tuio_bridge", frame)
                    if cv2.waitKey(1) & 0xFF == ord("q"):
                        break
                continue

            payload: Dict[int, Tuple[float, float, float, float, float]] = {}
            for st in state_by_side.values():
                data = cursor_output(st)
                if data is not None:
                    payload[st.session_id] = data

            cursor_sender.send_frame(payload)

            if nav_state.active and nav_state.frames_remaining > 0:
                angle = (
                    SWIPE_RIGHT_ANGLE_RAD
                    if nav_state.direction == "right"
                    else SWIPE_LEFT_ANGLE_RAD
                )
                obj_payload: Dict[int, Tuple[int, float, float, float, float, float, float, float, float]] = {
                    NAV_OBJECT_SESSION_ID: (
                        NAV_OBJECT_SYMBOL_ID,
                        0.5,
                        0.5,
                        angle,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                    )
                }
                object_sender.send_frame(obj_payload)
                nav_state.frames_remaining -= 1
            elif nav_state.active and nav_state.frames_remaining <= 0:
                object_sender.send_empty()
                nav_state.active = False
                nav_state.direction = ""

            last_sent = now

            if args.show_preview:
                cv2.imshow("hand_tuio_bridge", frame)
                if cv2.waitKey(1) & 0xFF == ord("q"):
                    break
    except KeyboardInterrupt:
        pass
    finally:
        cap.release()
        hands.close()
        if args.show_preview:
            cv2.destroyAllWindows()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())