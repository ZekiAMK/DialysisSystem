#!/usr/bin/env python3
"""
Cyclami BLE speed sensor reader using the standard Bluetooth Cycling Speed and Cadence (CSC) service.

Features:
- scans for BLE devices advertising CSC service (0x1816)
- connects with bleak
- subscribes to CSC Measurement notifications (0x2A5B)
- decodes wheel/crank data
- calculates speed, distance, cadence
- prints JSON to stdout
- optionally POSTs each decoded sample to an HTTP endpoint

Tested against the Bluetooth SIG CSC Measurement format and Bleak 2.x API style.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import logging
import math
import signal
import sys
from dataclasses import dataclass, asdict
from datetime import datetime, timezone
from typing import Any, Optional

import requests
from bleak import BleakClient, BleakScanner
from bleak.backends.characteristic import BleakGATTCharacteristic

CSC_SERVICE_UUID = "00001816-0000-1000-8000-00805f9b34fb"
CSC_MEASUREMENT_UUID = "00002a5b-0000-1000-8000-00805f9b34fb"
CSC_FEATURE_UUID = "00002a5c-0000-1000-8000-00805f9b34fb"
SENSOR_LOCATION_UUID = "00002a5d-0000-1000-8000-00805f9b34fb"

WHEEL_EVENT_TIME_RESOLUTION = 1 / 1024.0  # seconds
CRANK_EVENT_TIME_RESOLUTION = 1 / 1024.0  # seconds
MAX_16BIT = 65536
MAX_32BIT = 4294967296


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def decode_sensor_location(raw: bytes) -> str:
    # Bluetooth SIG assigned values for Sensor Location characteristic.
    locations = {
        0: "Other",
        1: "Top of shoe",
        2: "In shoe",
        3: "Hip",
        4: "Front wheel",
        5: "Left crank",
        6: "Right crank",
        7: "Left pedal",
        8: "Right pedal",
        9: "Front hub",
        10: "Rear dropout",
        11: "Chainstay",
        12: "Rear wheel",
        13: "Rear hub",
        14: "Chest",
        15: "Spider",
        16: "Chain ring",
    }
    if not raw:
        return "Unknown"
    return locations.get(raw[0], f"Unknown({raw[0]})")


@dataclass
class Reading:
    timestamp: str
    device_name: Optional[str]
    address: str
    speed_m_s: Optional[float]
    speed_kmh: Optional[float]
    distance_m: Optional[float]
    total_wheel_revolutions: Optional[int]
    wheel_rpm: Optional[float]
    cadence_rpm: Optional[float]
    total_crank_revolutions: Optional[int]
    wheel_event_time_s: Optional[float]
    crank_event_time_s: Optional[float]
    raw_hex: str


class CyclamiCSCReader:
    def __init__(
        self,
        wheel_circumference_m: float,
        post_url: Optional[str] = None,
        post_timeout_s: float = 5.0,
        verify_tls: bool = True,
    ) -> None:
        self.wheel_circumference_m = wheel_circumference_m
        self.post_url = post_url
        self.post_timeout_s = post_timeout_s
        self.verify_tls = verify_tls

        self.device_name: Optional[str] = None
        self.address: str = ""

        self._last_wheel_revs: Optional[int] = None
        self._last_wheel_event_time: Optional[int] = None
        self._last_crank_revs: Optional[int] = None
        self._last_crank_event_time: Optional[int] = None
        self._base_total_distance_m = 0.0
        self._latest_distance_m: Optional[float] = None

    async def pick_device(self, name_filter: Optional[str], address: Optional[str], scan_time: float):
        if address:
            logging.info("Using explicit BLE address/UUID: %s", address)
            return await BleakScanner.find_device_by_address(address, timeout=scan_time)

        logging.info("Scanning for BLE CSC devices for %.1f seconds...", scan_time)
        devices = await BleakScanner.discover(timeout=scan_time, service_uuids=[CSC_SERVICE_UUID])

        if name_filter:
            lowered = name_filter.lower()
            filtered = [d for d in devices if (d.name or "").lower().find(lowered) != -1]
        else:
            filtered = devices

        if not filtered:
            return None

        # Pick the strongest signal among matches.
        filtered.sort(key=lambda d: getattr(d, "rssi", -999), reverse=True)
        return filtered[0]

    async def run(self, name_filter: Optional[str], address: Optional[str], scan_time: float) -> None:
        device = await self.pick_device(name_filter=name_filter, address=address, scan_time=scan_time)
        if device is None:
            raise RuntimeError(
                "No CSC BLE device found. Wake the sensor by spinning the wheel, ensure it is not connected elsewhere, "
                "or pass --address from nRF Connect."
            )

        self.device_name = device.name
        self.address = device.address
        logging.info("Connecting to %s (%s)", device.name, device.address)

        disconnected_event = asyncio.Event()

        def _on_disconnect(_: BleakClient) -> None:
            logging.warning("Device disconnected")
            disconnected_event.set()

        async with BleakClient(device, disconnected_callback=_on_disconnect) as client:
            logging.info("Connected: %s", client.is_connected)
            await self._read_optional_info(client)
            await client.start_notify(CSC_MEASUREMENT_UUID, self._notification_handler)
            logging.info("Subscribed to CSC Measurement notifications")
            logging.info("Spin the wheel to generate data... Press Ctrl+C to stop.")
            await disconnected_event.wait()

    async def _read_optional_info(self, client: BleakClient) -> None:
        try:
            feature_raw = await client.read_gatt_char(CSC_FEATURE_UUID)
            feature_value = int.from_bytes(feature_raw[:2], "little") if len(feature_raw) >= 2 else 0
            logging.info("CSC Feature raw=0x%04x", feature_value)
        except Exception as exc:
            logging.info("Could not read CSC Feature: %s", exc)

        try:
            loc_raw = await client.read_gatt_char(SENSOR_LOCATION_UUID)
            logging.info("Sensor location: %s", decode_sensor_location(loc_raw))
        except Exception as exc:
            logging.info("Could not read Sensor Location: %s", exc)

    def _notification_handler(self, _: BleakGATTCharacteristic, data: bytearray) -> None:
        try:
            reading = self._parse_csc_measurement(bytes(data))
            line = json.dumps(asdict(reading), ensure_ascii=False)
            print(line, flush=True)
            if self.post_url:
                self._post_json(asdict(reading))
        except Exception as exc:
            logging.exception("Failed to parse notification: %s", exc)

    def _parse_csc_measurement(self, data: bytes) -> Reading:
        if len(data) < 1:
            raise ValueError("Empty CSC measurement")

        flags = data[0]
        wheel_present = bool(flags & 0x01)
        crank_present = bool(flags & 0x02)

        offset = 1
        speed_m_s: Optional[float] = None
        speed_kmh: Optional[float] = None
        distance_m = self._latest_distance_m
        total_wheel_revolutions: Optional[int] = None
        wheel_event_time_s: Optional[float] = None
        wheel_rpm: Optional[float] = None
        cadence_rpm: Optional[float] = None
        total_crank_revolutions: Optional[int] = None
        crank_event_time_s: Optional[float] = None

        if wheel_present:
            if len(data) < offset + 6:
                raise ValueError("CSC wheel data truncated")
            cumulative_wheel_revs = int.from_bytes(data[offset : offset + 4], "little")
            last_wheel_event_time = int.from_bytes(data[offset + 4 : offset + 6], "little")
            offset += 6

            total_wheel_revolutions = cumulative_wheel_revs
            wheel_event_time_s = last_wheel_event_time * WHEEL_EVENT_TIME_RESOLUTION
            distance_m = cumulative_wheel_revs * self.wheel_circumference_m
            self._latest_distance_m = distance_m

            if self._last_wheel_revs is not None and self._last_wheel_event_time is not None:
                rev_diff = (cumulative_wheel_revs - self._last_wheel_revs) % MAX_32BIT
                time_diff_ticks = (last_wheel_event_time - self._last_wheel_event_time) % MAX_16BIT
                time_diff_s = time_diff_ticks * WHEEL_EVENT_TIME_RESOLUTION
                if rev_diff > 0 and time_diff_s > 0:
                    speed_m_s = (rev_diff * self.wheel_circumference_m) / time_diff_s
                    speed_kmh = speed_m_s * 3.6
                    wheel_rpm = (rev_diff / time_diff_s) * 60.0
                elif rev_diff == 0 and time_diff_s > 0:
                    speed_m_s = 0.0
                    speed_kmh = 0.0
                    wheel_rpm = 0.0

            self._last_wheel_revs = cumulative_wheel_revs
            self._last_wheel_event_time = last_wheel_event_time

        if crank_present:
            if len(data) < offset + 4:
                raise ValueError("CSC crank data truncated")
            cumulative_crank_revs = int.from_bytes(data[offset : offset + 2], "little")
            last_crank_event_time = int.from_bytes(data[offset + 2 : offset + 4], "little")
            offset += 4

            total_crank_revolutions = cumulative_crank_revs
            crank_event_time_s = last_crank_event_time * CRANK_EVENT_TIME_RESOLUTION

            if self._last_crank_revs is not None and self._last_crank_event_time is not None:
                crank_diff = (cumulative_crank_revs - self._last_crank_revs) % MAX_16BIT
                crank_time_diff_ticks = (last_crank_event_time - self._last_crank_event_time) % MAX_16BIT
                crank_time_diff_s = crank_time_diff_ticks * CRANK_EVENT_TIME_RESOLUTION
                if crank_diff > 0 and crank_time_diff_s > 0:
                    cadence_rpm = (crank_diff / crank_time_diff_s) * 60.0
                elif crank_diff == 0 and crank_time_diff_s > 0:
                    cadence_rpm = 0.0

            self._last_crank_revs = cumulative_crank_revs
            self._last_crank_event_time = last_crank_event_time

        return Reading(
            timestamp=utc_now_iso(),
            device_name=self.device_name,
            address=self.address,
            speed_m_s=round(speed_m_s, 3) if speed_m_s is not None else None,
            speed_kmh=round(speed_kmh, 3) if speed_kmh is not None else None,
            distance_m=round(distance_m, 3) if distance_m is not None else None,
            total_wheel_revolutions=total_wheel_revolutions,
            wheel_rpm=round(wheel_rpm, 3) if wheel_rpm is not None else None,
            cadence_rpm=round(cadence_rpm, 3) if cadence_rpm is not None else None,
            total_crank_revolutions=total_crank_revolutions,
            wheel_event_time_s=round(wheel_event_time_s, 3) if wheel_event_time_s is not None else None,
            crank_event_time_s=round(crank_event_time_s, 3) if crank_event_time_s is not None else None,
            raw_hex=data.hex(),
        )

    def _post_json(self, payload: dict[str, Any]) -> None:
        try:
            response = requests.post(
                self.post_url,
                json=payload,
                timeout=self.post_timeout_s,
                verify=self.verify_tls,
                headers={"Content-Type": "application/json"},
            )
            response.raise_for_status()
        except Exception as exc:
            logging.error("POST failed: %s", exc)


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Read a Cyclami speed sensor over BLE and optionally POST data.")
    parser.add_argument("--name", default="Cyclami", help="Device name contains filter. Default: Cyclami")
    parser.add_argument("--address", help="BLE address (Windows/Linux) or device UUID (macOS).")
    parser.add_argument(
        "--wheel-circumference-mm",
        type=float,
        default=2105.0,
        help="Wheel circumference in mm. Default: 2105 (typical 700x25c road tire).",
    )
    parser.add_argument("--scan-time", type=float, default=8.0, help="Scan timeout in seconds. Default: 8")
    parser.add_argument("--post-url", help="Optional HTTP/HTTPS endpoint to receive JSON POSTs.")
    parser.add_argument("--post-timeout", type=float, default=5.0, help="POST timeout in seconds. Default: 5")
    parser.add_argument("--insecure", action="store_true", help="Disable TLS certificate verification for HTTPS POSTs.")
    parser.add_argument("--debug", action="store_true", help="Enable debug logging.")
    return parser


async def async_main() -> int:
    args = build_arg_parser().parse_args()
    logging.basicConfig(
        level=logging.DEBUG if args.debug else logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    wheel_circumference_m = args.wheel_circumference_mm / 1000.0
    reader = CyclamiCSCReader(
        wheel_circumference_m=wheel_circumference_m,
        post_url=args.post_url,
        post_timeout_s=args.post_timeout,
        verify_tls=not args.insecure,
    )

    stop_event = asyncio.Event()

    def _stop(*_: Any) -> None:
        stop_event.set()

    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            asyncio.get_running_loop().add_signal_handler(sig, _stop)
        except NotImplementedError:
            # Windows may not support all signal handlers in event loop.
            signal.signal(sig, lambda *_args: _stop())

    task = asyncio.create_task(reader.run(args.name, args.address, args.scan_time))
    stop_task = asyncio.create_task(stop_event.wait())

    done, pending = await asyncio.wait({task, stop_task}, return_when=asyncio.FIRST_COMPLETED)

    if stop_task in done:
        logging.info("Stop requested")
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass
        return 0

    try:
        await task
        return 0
    except Exception as exc:
        logging.error("Fatal error: %s", exc)
        return 1
    finally:
        for p in pending:
            p.cancel()


def main() -> int:
    try:
        return asyncio.run(async_main())
    except KeyboardInterrupt:
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
