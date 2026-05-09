#!/usr/bin/env python3
"""Run this first to find which import is killing hand_tuio_bridge.py"""
import sys
print(f"Python: {sys.version}")
print(f"Executable: {sys.executable}")
print()

imports = [
    ("cv2",           "import cv2; print('cv2', cv2.__version__)"),
    ("mediapipe",     "import mediapipe as mp; print('mediapipe', mp.__version__)"),
    ("dollarpy",      "from dollarpy import Point, Recognizer, Template; print('dollarpy OK')"),
    ("python-osc",    "from pythonosc.udp_client import UDPClient; print('python-osc OK')"),
    ("clothing_detection", "from clothing_detection import ClothingDetector; print('clothing_detection OK')"),
]

all_ok = True
for name, stmt in imports:
    try:
        exec(stmt)
    except Exception as e:
        print(f"  FAILED  {name}: {e}")
        all_ok = False

print()
if all_ok:
    print("All imports OK — the crash is elsewhere.")
else:
    print("Fix the FAILED imports above, then re-run hand_tuio_bridge.py")