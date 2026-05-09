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

## Firmware

.NET nanoFramework targeting the ESP32-S3 image. Solution at `SolarTracker.sln`. Two projects:

- `src/SolarTracker.Bringup/` — minimal PMIC + display + touch test, no sensors needed. Use this first.
- `src/SolarTracker.Device/` — full firmware (dashboard + calibration + GPS + IMU + AS5600).

**Setup + bring-up walkthrough:** see [docs/setup.md](docs/setup.md).

| File | Role |
|---|---|
| `Program.cs` | Pin assignments, boot sequence, render loop |
| `Hardware/Axp2101.cs` | PMIC bring-up — must run before display/touch |
| `Hardware/Calibration.cs` | Flash-backed `azimuth_zero` storage |
| `Hardware/Display.cs` | ILI9342C SPI driver (fill, rect, text) |
| `Hardware/Font5x7.cs` | Bitmap font for the dashboard |
| `Hardware/Touch.cs` | FT6336U capacitive touch reader |
| `Sensors/As5600.cs` | AS5600 12-bit absolute magnetic encoder |
| `Sensors/Gps.cs` | ATGM336H NMEA-0183 parser (RMC sentence) |
| `Sensors/Imu.cs` | MPU6886 accel → pitch/roll |
| `Ui/CalibrationScreen.cs` | Install-time "point south, tap CONFIRM" flow |
| `Ui/Dashboard.cs` | Main screen — GPS / tilt / azimuth |

### Boot sequence

1. Configure internal I²C → talk to AXP2101 → enable BLDO1/BLDO2/DLDO1 (display) and ALDO2 (touch).
2. Configure SPI2 + DC pin → init ILI9342C → blank screen.
3. Open touch (FT6336U) on the internal I²C bus.
4. Configure Grove I²C → init MPU6886 + AS5600.
5. Load `azimuth_zero` from `I:\calibration.dat`. If missing, run the calibration screen and write it.
6. Open Port C UART → start GPS reader thread.
7. Draw the dashboard.
8. Loop: read sensors → update dashboard → poll touch (tap on AZIMUTH row re-enters calibration).

### Calibration

The AS5600 reports a 12-bit absolute angle (0–4095) of whatever orientation the magnet currently has. To turn that into a real-world azimuth, the firmware needs to know which raw reading corresponds to "panel pointing due south." That mapping is the install-time calibration:

1. Mount everything mechanically — encoder, magnet, motors.
2. Power on. If no calibration is stored, the calibration screen appears with the live raw value and magnet-health indicator.
3. Manually point the panel due south (phone compass for a rough alignment, solar-noon shadow for ±0.5°).
4. Tap **CONFIRM SOUTH**. The current raw value is written to flash as `azimuth_zero`.
5. The dashboard takes over and shows `(raw − azimuth_zero)` in degrees as the panel azimuth.

The calibration persists across power loss and firmware redeploys (kept in the user-data partition, separate from the code partition). Tap the AZIMUTH row on the dashboard to re-enter calibration if the magnet is ever re-glued or the encoder is re-mounted.

### Hardware notes

- **AXP2101 register values** — taken from the M5Stack reference firmware. If the screen stays dark on first boot, the ALDO/BLDO/DLDO map is the place to look.
- **AS5600 magnet** must be diametrically magnetized and centered on the rotation axis within ~0.25 mm. 1–2 mm air gap to the sensor.
- **Touch coordinate system** may need flipping depending on panel rotation — easy to invert in `Touch.TryRead()` once verified on hardware.

## Status

Bootstrapping. Firmware scaffolded — display/sensor bring-up pending hardware verification.
