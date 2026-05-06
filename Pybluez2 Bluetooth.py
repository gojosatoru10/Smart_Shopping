import argparse
import asyncio
import json
import platform
import tempfile
import time
from pathlib import Path

try:
    import bluetooth  # pybluez2
except Exception:
    bluetooth = None

try:
    # winsdk
    from winsdk.windows.devices.bluetooth import BluetoothConnectionStatus, BluetoothDevice
except ModuleNotFoundError:
    BluetoothConnectionStatus = None
    BluetoothDevice = None


def normalize_mac(mac: str) -> str:
    return mac.strip().upper()


def load_allowed_devices(db_path: Path) -> dict:
    data = json.loads(db_path.read_text(encoding="utf-8"))
    devices = data.get("devices", data)
    allowed = {}
    for addr, info in devices.items():
        allowed[normalize_mac(addr)] = info
    return allowed


def is_bluetooth_on() -> bool:
    if bluetooth is None:
        return False
    try:
        bluetooth.discover_devices(duration=2, lookup_names=False)
        return True
    except OSError:
        return False
    except Exception:
        return False


def mac_to_uint64(mac: str) -> int:
    return int(normalize_mac(mac).replace(":", ""), 16)


async def _get_connected_macs_windows_async(macs: list) -> set:
    connected = set()
    if BluetoothDevice is None or BluetoothConnectionStatus is None:
        return connected

    for mac in macs:
        device = None
        try:
            device = await BluetoothDevice.from_bluetooth_address_async(mac_to_uint64(mac))
        except Exception:
            continue

        if device is None:
            continue

        try:
            if device.connection_status == BluetoothConnectionStatus.CONNECTED:
                connected.add(normalize_mac(mac))
        except Exception:
            pass
        finally:
            # Release the WinRT COM reference so the next poll gets a fresh
            # object with updated connection_status instead of a cached one.
            try:
                device.close()
            except Exception:
                pass

    return connected


def get_connected_macs_windows(macs: list) -> set:
    try:
        return asyncio.run(_get_connected_macs_windows_async(macs))
    except RuntimeError:
        loop = asyncio.new_event_loop()
        try:
            return loop.run_until_complete(_get_connected_macs_windows_async(macs))
        finally:
            loop.close()


def get_connected_macs(macs: list) -> set:
    if platform.system().lower() == "windows":
        return get_connected_macs_windows(macs)
    return set()


def load_cache(cache_file: Path) -> dict:
    if not cache_file.exists():
        return {"last_connected": {}, "prev_connected": []}
    try:
        data = json.loads(cache_file.read_text(encoding="utf-8"))
        if not isinstance(data, dict):
            return {"last_connected": {}, "prev_connected": []}
        if "last_connected" not in data or not isinstance(data["last_connected"], dict):
            data["last_connected"] = {}
        if "prev_connected" not in data or not isinstance(data["prev_connected"], list):
            data["prev_connected"] = []
        return data
    except Exception:
        return {"last_connected": {}, "prev_connected": []}


def write_json_atomic(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", delete=False, dir=str(path.parent), encoding="utf-8") as tmp:
        json.dump(payload, tmp, indent=2)
        tmp.flush()
        tmp_name = tmp.name
    Path(tmp_name).replace(path)


def save_cache(cache_file: Path, cache_data: dict) -> None:
    write_json_atomic(cache_file, cache_data)


def resolve_recent_user(allowed_devices: dict, cache_data: dict) -> dict:
    now = int(time.time())

    if bluetooth is None:
        return {
            "status": "error",
            "username": "",
            "mac": "",
            "connected_at": 0,
            "generated_at": now,
            "source": "pybluez2",
            "selection_reason": "pybluez2 module is not available",
        }

    if not is_bluetooth_on():
        return {
            "status": "searching",
            "username": "",
            "mac": "",
            "connected_at": 0,
            "generated_at": now,
            "source": "pybluez2",
            "selection_reason": "bluetooth adapter unavailable or disabled",
        }

    connected = get_connected_macs(list(allowed_devices.keys()))
    prev_connected = {normalize_mac(x) for x in cache_data.get("prev_connected", [])}
    last_connected = cache_data.get("last_connected", {})

    for mac in connected:
        if mac not in last_connected:
            last_connected[mac] = now
        elif mac not in prev_connected:
            last_connected[mac] = now

    cache_data["prev_connected"] = sorted(list(connected))
    cache_data["last_connected"] = last_connected

    if not connected:
        return {
            "status": "login_required",
            "username": "",
            "mac": "",
            "connected_at": 0,
            "generated_at": now,
            "source": "pybluez2",
            "selection_reason": "no allowed connected device",
        }

    selected = None
    best_ts = -1
    for mac in connected:
        ts = int(last_connected.get(mac, 0))
        if ts > best_ts:
            best_ts = ts
            selected = mac

    if selected is None:
        selected = sorted(list(connected))[0]
        best_ts = int(last_connected.get(selected, now))

    user_info = allowed_devices.get(selected, {})
    if isinstance(user_info, dict):
        username = str(user_info.get("name", "Unknown"))
    else:
        username = str(user_info)

    return {
        "status": "signed_in",
        "username": username,
        "mac": selected,
        "connected_at": int(best_ts),
        "generated_at": now,
        "source": "pybluez2",
        "selection_reason": "most_recent_connected",
    }


def one_shot(args: argparse.Namespace) -> int:
    allowed_devices = load_allowed_devices(args.db_path)
    cache_data = load_cache(args.cache_file)
    result = resolve_recent_user(allowed_devices, cache_data)
    save_cache(args.cache_file, cache_data)
    write_json_atomic(args.output_file, result)

    print(json.dumps(result, indent=2))
    return 0


def watch(args: argparse.Namespace) -> int:
    allowed_devices = load_allowed_devices(args.db_path)
    previous_identity = ""

    while True:
        cache_data = load_cache(args.cache_file)
        result = resolve_recent_user(allowed_devices, cache_data)
        save_cache(args.cache_file, cache_data)
        write_json_atomic(args.output_file, result)

        identity = result.get("status", "") + "|" + result.get("username", "") + "|" + result.get("mac", "")
        if identity != previous_identity:
            print(json.dumps(result, ensure_ascii=True))
            previous_identity = identity

        time.sleep(max(0.5, float(args.interval)))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Bluetooth recent device resolver")
    parser.add_argument("--db-path", type=Path, default=Path("devices_db.json"))
    parser.add_argument("--output-file", type=Path, default=Path(".runtime/current_user.json"))
    parser.add_argument("--cache-file", type=Path, default=Path(".runtime/bluetooth_seen_cache.json"))
    parser.add_argument("--watch", action="store_true", help="Continuously update output state file")
    parser.add_argument("--interval", type=float, default=1.5, help="Polling interval seconds in watch mode")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.watch:
        return watch(args)
    return one_shot(args)


if __name__ == "__main__":
    raise SystemExit(main())
