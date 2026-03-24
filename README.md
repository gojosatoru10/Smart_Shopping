# Smart_Shopping

## Hand Recognition to TUIO Integration

This repository now includes a standalone Python bridge service that streams hand cursor updates and gesture-triggered bursts as TUIO `/tuio/2Dcur` bundles for the C# `TuioDemo` receiver.

### New bridge files
- `bridge/hand_tuio_bridge.py`
- `bridge/start_tuio_integration.ps1`

### Free-port startup contract
- The bridge uses a free UDP port when `--tuio-port auto` is used.
- The selected port is published to stdout (`TUIO_PORT=<port>`).
- The selected port is also written to `.runtime/tuio_port.json`.

### Default port
- Launcher and bridge default to UDP port `50001`.
- You can still opt into automatic free-port mode by passing `-TuioPort auto` in the PowerShell launcher or `--tuio-port auto` in the Python bridge.

### Quick start (Windows)
1. Install Python dependencies:
	- `pip install -r requirements.txt`
2. Start the integrated flow:
	- `run_all.bat`

### Launcher defaults
- `run_all.bat` runs with:
  - Python: `C:\Users\ka422\AppData\Local\Programs\Python\Python39\python.exe`
  - Camera index: `1` (Iriun-friendly default)
  - TUIO port: `50001`

### Manual run
1. Start bridge service:
	- `python .\bridge\hand_tuio_bridge.py --tuio-port 50001 --port-file .runtime/tuio_port.json --show-preview`
2. Read `port` from `.runtime/tuio_port.json`.
3. Start C# receiver with that port:
	- `TuioDemo.exe <port>`
