#!/usr/bin/env python3
import sys, os
from pathlib import Path

print("STEP 1: basic imports OK", flush=True)

# Add bridge dir to path
bridge_dir = str(Path(r"E:\HCI\Project\Smart_Shopping\bridge").resolve())
if bridge_dir not in sys.path:
    sys.path.insert(0, bridge_dir)
print(f"STEP 2: sys.path patched, bridge_dir={bridge_dir}", flush=True)

os.environ.setdefault("KMP_DUPLICATE_LIB_OK", "TRUE")
os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_THREADING_LAYER", "GNU")
print("STEP 3: env vars set", flush=True)

import cv2
print(f"STEP 4: cv2 OK ({cv2.__version__})", flush=True)

try:
    from mediapipe.python.solutions import drawing_styles as mp_drawing_styles
    from mediapipe.python.solutions import drawing_utils as mp_drawing
    from mediapipe.python.solutions import hands as mp_hands
    print("STEP 5: mediapipe solutions OK", flush=True)
except Exception as e:
    print(f"STEP 5 FAILED mediapipe solutions: {e}", flush=True)
    try:
        import mediapipe as mp
        mp_drawing_styles = mp.solutions.drawing_styles
        mp_drawing = mp.solutions.drawing_utils
        mp_hands = mp.solutions.hands
        print("STEP 5b: mediapipe fallback OK", flush=True)
    except Exception as e2:
        print(f"STEP 5b FAILED: {e2}", flush=True)
        sys.exit(1)

try:
    from dollarpy import Point, Recognizer, Template
    print("STEP 6: dollarpy OK", flush=True)
except Exception as e:
    print(f"STEP 6 FAILED dollarpy: {e}", flush=True)
    sys.exit(1)

try:
    from pythonosc.osc_bundle_builder import IMMEDIATELY, OscBundleBuilder
    from pythonosc.osc_message_builder import OscMessageBuilder
    from pythonosc.udp_client import UDPClient
    print("STEP 7: pythonosc OK", flush=True)
except Exception as e:
    print(f"STEP 7 FAILED pythonosc: {e}", flush=True)
    sys.exit(1)

try:
    from clothing_detection import ClothingDetector
    print("STEP 8: clothing_detection OK", flush=True)
except Exception as e:
    print(f"STEP 8 FAILED clothing_detection: {e}", flush=True)
    # Not fatal - just warn
    print("  (clothing detection will be disabled)", flush=True)

print("ALL IMPORTS DONE - crash is inside main() logic", flush=True)