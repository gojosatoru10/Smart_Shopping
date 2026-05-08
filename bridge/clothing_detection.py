from __future__ import annotations

import json
import time
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, Optional, Tuple


CATEGORY_INDEX = {
    "tshirt": 0,
    "hoodie": 1,
    "jacket": 2,
    "pants": 3,
    "shorts": 4,
}


def map_label_to_category(label: str) -> Optional[Tuple[str, int]]:
    name = (label or "").strip().lower()
    if not name:
        return None

    # Model classes expected: sweat, tshirt, jean, shorts, coat
    if "sweat" in name or "hoodie" in name or "hooded" in name:
        return "hoodie", CATEGORY_INDEX["hoodie"]
    if "tshirt" in name or "t-shirt" in name or "tee" in name:
        return "tshirt", CATEGORY_INDEX["tshirt"]
    if "jean" in name:
        return "pants", CATEGORY_INDEX["pants"]
    if "coat" in name:
        return "jacket", CATEGORY_INDEX["jacket"]

    if any(k in name for k in ("jacket", "blazer", "outerwear")):
        return "jacket", CATEGORY_INDEX["jacket"]
    if any(k in name for k in ("short", "bermuda")):
        return "shorts", CATEGORY_INDEX["shorts"]
    if any(k in name for k in ("pant", "trouser", "jean", "leggings", "jogger")):
        return "pants", CATEGORY_INDEX["pants"]
    if any(k in name for k in ("shirt", "top", "blouse")):
        return "tshirt", CATEGORY_INDEX["tshirt"]
    return None


def write_json_atomic(path: Path, payload: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    tmp.replace(path)


class ClothingDetector:
    def __init__(
        self,
        model_path: Path,
        output_path: Path,
        min_confidence: float = 0.35,
        interval_sec: float = 0.5,
    ) -> None:
        self.model_path = Path(model_path)
        self.output_path = Path(output_path)
        self.min_confidence = float(min_confidence)
        self.interval_sec = max(0.05, float(interval_sec))
        self._last_infer_ts = 0.0
        self._model = None
        self.last_payload: Dict[str, Any] = {}

        self._load_model()

    def _load_model(self) -> None:
        if not self.model_path.exists():
            self._write_status("model_unavailable", error=f"Model file not found: {self.model_path}")
            return

        try:
            from ultralytics import YOLO  # type: ignore
        except Exception as exc:
            self._write_status("model_unavailable", error=f"ultralytics import failed: {exc}")
            return

        try:
            self._model = YOLO(str(self.model_path))
            self._write_status("ready")
        except Exception as exc:
            self._model = None
            self._write_status("model_unavailable", error=f"Model load failed: {exc}")

    def _write_status(self, status: str, **extra: Any) -> None:
        payload: Dict[str, Any] = {
            "status": status,
            "timestamp": datetime.utcnow().isoformat() + "Z",
        }
        payload.update(extra)
        self.last_payload = payload
        write_json_atomic(self.output_path, payload)

    def _best_detection(self, frame) -> Optional[Tuple[str, str, int, float]]:
        if self._model is None:
            return None

        try:
            results = self._model(frame, verbose=False)
        except Exception as exc:
            self._write_status("error", error=f"inference_failed: {exc}")
            return None

        if not results:
            return None

        res = results[0]
        boxes = getattr(res, "boxes", None)
        if boxes is None or len(boxes) == 0:
            return None

        names = getattr(res, "names", {}) or {}
        best = None
        best_conf = 0.0

        for box in boxes:
            try:
                conf = float(box.conf[0].item())
                cls_id = int(box.cls[0].item())
            except Exception:
                continue

            label = str(names.get(cls_id, cls_id))
            mapped = map_label_to_category(label)
            if mapped is None:
                continue
            if conf > best_conf:
                best_conf = conf
                best = (label, mapped[0], mapped[1], conf)

        return best

    def process_frame(self, frame, now_ts: Optional[float] = None) -> Optional[Dict[str, Any]]:
        now = float(now_ts if now_ts is not None else time.time())
        if now - self._last_infer_ts < self.interval_sec:
            return None
        self._last_infer_ts = now

        if self._model is None:
            return self.last_payload

        best = self._best_detection(frame)
        if best is None:
            self._write_status("no_detection")
            return self.last_payload

        raw_label, category, category_index, confidence = best
        if confidence < self.min_confidence:
            self._write_status("no_detection", confidence=round(confidence, 4), label=raw_label)
            return self.last_payload

        payload = {
            "status": "detected",
            "label": raw_label,
            "category": category,
            "category_index": category_index,
            "confidence": round(confidence, 4),
            "timestamp": datetime.utcnow().isoformat() + "Z",
        }
        self.last_payload = payload
        write_json_atomic(self.output_path, payload)
        return payload
