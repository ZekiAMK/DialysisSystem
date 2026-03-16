# Cyclami BLE speed sensor reader

This Python app connects to a **Cyclami bike speed/cadence sensor** over **Bluetooth Low Energy**, reads live CSC notifications, decodes the values, prints them as JSON, and can optionally POST each reading to your server.

## What this app expects

It expects the sensor to expose the standard Bluetooth **Cycling Speed and Cadence (CSC)** service:

- Service: `0x1816`
- Measurement characteristic: `0x2A5B`

That is the normal BLE profile for bike speed/cadence sensors. If your specific Cyclami unit uses a proprietary service instead, this script will not decode it until the UUIDs and packet format are changed.

## Files

- `cyclami_ble_reader.py` — the Python app
- `README_cyclami_ble_reader.md` — this manual

## 1) Install Python

Use Python 3.10 or newer.

Check your version:

```bash
python --version
```

or on some systems:

```bash
python3 --version
```

## 2) Install dependencies

```bash
pip install bleak requests
```

If your system uses `pip3`:

```bash
pip3 install bleak requests
```

## 3) Prepare the sensor

1. Put a battery in the Cyclami sensor.
2. Wake it up by spinning the wheel or crank a few times.
3. Make sure it is **not already connected** to another app, bike computer, or phone Bluetooth menu.
4. In **nRF Connect**, confirm you can see service `1816` and characteristic `2A5B`.

## 4) Basic usage

Run the script:

```bash
python cyclami_ble_reader.py
```

It will:

1. scan for BLE devices advertising the CSC service
2. prefer a device whose name contains `Cyclami`
3. connect
4. subscribe to notifications from `2A5B`
5. print JSON lines as data arrives

Example output:

```json
{"timestamp": "2026-03-09T10:15:22.123456+00:00", "device_name": "Cyclami", "address": "AA:BB:CC:DD:EE:FF", "speed_m_s": 6.944, "speed_kmh": 25.0, "distance_m": 105.25, "total_wheel_revolutions": 50, "wheel_rpm": 198.0, "cadence_rpm": null, "total_crank_revolutions": null, "wheel_event_time_s": 123.456, "crank_event_time_s": null, "raw_hex": "0132000000401f"}
```

## 5) If the device name is not “Cyclami”

Try a broader filter:

```bash
python cyclami_ble_reader.py --name ""
```

Or use the exact BLE address / device UUID from nRF Connect:

```bash
python cyclami_ble_reader.py --address AA:BB:CC:DD:EE:FF
```

On macOS, use the device UUID shown by your BLE tools rather than a MAC address.

## 6) Set wheel circumference

Speed and distance are computed from wheel revolutions, so the wheel circumference must be reasonably accurate.

Example for a 700x25c tire:

```bash
python cyclami_ble_reader.py --wheel-circumference-mm 2105
```

Another example:

```bash
python cyclami_ble_reader.py --wheel-circumference-mm 2136
```

## 7) POST the data to your backend

To send every decoded reading as JSON to your API:

```bash
python cyclami_ble_reader.py --post-url https://your-server.example.com/cycling-data
```

Each notification is POSTed as a JSON body.

Example payload:

```json
{
  "timestamp": "2026-03-09T10:15:22.123456+00:00",
  "device_name": "Cyclami",
  "address": "AA:BB:CC:DD:EE:FF",
  "speed_m_s": 6.944,
  "speed_kmh": 25.0,
  "distance_m": 105.25,
  "total_wheel_revolutions": 50,
  "wheel_rpm": 198.0,
  "cadence_rpm": null,
  "total_crank_revolutions": null,
  "wheel_event_time_s": 123.456,
  "crank_event_time_s": null,
  "raw_hex": "0132000000401f"
}
```

For a local test API:

```bash
python cyclami_ble_reader.py --post-url http://127.0.0.1:8000/data
```

## 8) Debug logging

To see more detail:

```bash
python cyclami_ble_reader.py --debug
```

## 9) Common problems

### No device found

Usually one of these:

- the sensor is sleeping
- the battery is weak or dead
- the sensor is already connected to another app
- the sensor does not advertise the standard CSC service

Fixes:

- spin the wheel again
- close other apps that may own the BLE connection
- use `--address`
- verify in nRF Connect that `1816` is present

### Connects but no data comes in

Usually the sensor only sends updates while the wheel is moving.

Spin the wheel continuously for a few seconds.

### Wrong speed value

Your wheel circumference is incorrect.

Set `--wheel-circumference-mm` to the correct value for your tire.

### HTTPS POST errors

That usually means your server certificate is invalid or self-signed.

For testing only, you can disable TLS verification:

```bash
python cyclami_ble_reader.py --post-url https://your-test-server.local/data --insecure
```

## 10) Minimal server example to receive POSTs

Here is a tiny Flask server that accepts the sensor data:

```python
from flask import Flask, request, jsonify

app = Flask(__name__)

@app.post("/data")
def data():
    payload = request.get_json(force=True, silent=True) or {}
    print(payload)
    return jsonify({"ok": True})

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=8000, debug=True)
```

Install Flask:

```bash
pip install flask
```

Run server:

```bash
python server.py
```

Then in another terminal:

```bash
python cyclami_ble_reader.py --post-url http://127.0.0.1:8000/data
```

## 11) How the decoding works

The CSC Measurement packet contains:

- flags
- cumulative wheel revolutions + last wheel event time
- optionally cumulative crank revolutions + last crank event time

The script calculates:

- **speed** from change in wheel revolutions over change in wheel event time
- **distance** from cumulative wheel revolutions × wheel circumference
- **cadence** from change in crank revolutions over change in crank event time

## 12) Before using it in production

First verify your Cyclami sensor in **nRF Connect**:

- service `1816` exists
- characteristic `2A5B` sends notifications when the wheel moves

If that is true, this script should work with little or no modification.

If your nRF Connect screenshot shows different UUIDs, the script can be adapted to those UUIDs and packet bytes.
