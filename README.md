# Solar Tracker

Dual-axis solar tracker built on the **M5Stack Core3 (ESP32)**.

Instead of LDR-based sun-seeking, this tracker uses **GPS + an angle/IMU sensor** to compute and verify the sun's position geometrically — robust on cloudy days and free of phototropic drift.

## Hardware

| Role | Part |
|---|---|
| MCU / brain | M5Stack Core3 (ESP32-S3) |
| Position fix | GPS module |
| Tilt feedback | Angle / IMU sensor |
| Azimuth + elevation drives | 2x BTS7960 H-bridge motor drivers |
| Auxiliary actuator | M5Stack Servo2 module |
| Telemetry link | RF433 to remote weather station |
| Battery | 24 V |
| 5 V rail | Klnuoxj 24V→5V 5A IP68 buck converter (8–32 V in, reverse-polarity protected) |

## Power wiring

The buck converter bridges the 24 V motor battery to the 5 V logic side.

| Converter wire | Connect to |
|---|---|
| Red (Input +) | 24 V battery + |
| Black (Input -) | 24 V battery - |
| Yellow (Output +) | M5Stack 5V / BTS7960 VCC / Servo2 VCC |
| Black (Output -) | M5Stack GND / BTS7960 GND / Servo2 GND |

> **Common ground:** tie input - and output - to a single ground point so the MCU and motor drivers agree on 0 V. Otherwise PWM signals get noisy and the BTS7960 enable lines can latch incorrectly.

5 V can be delivered to the Core3 via:
- **A.** USB-C pigtail soldered to the converter's yellow/black wires (cleanest), or
- **B.** Direct to the 5V/GND pins on the M-Bus header.

## Why GPS + angle sensor (not LDRs)

- Works through clouds, fog, and partial shading.
- No false-tracking off bright reflections.
- Position is computed from time + lat/lon, then closed-loop verified against the angle sensor — drive errors are measurable, not just inferred.

## Status

Bootstrapping. Source coming.
