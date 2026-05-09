# CoreS3 nanoFramework Setup & Bring-up

End-to-end guide to get the M5Stack CoreS3 running .NET nanoFramework and verify the display + touch with the `SolarTracker.Bringup` test project.

You'll need:

- **M5Stack CoreS3** (ESP32-S3 variant)
- **USB-C cable** — must be a *data* cable, not a charge-only one
- **Windows 10/11 PC** with admin rights to install drivers

You don't need any of the sensors (GPS / IMU / AS5600) for this test — they get plugged in for later steps.

---

## 1. Install the toolchain

### 1a. .NET 6+ SDK

Required by the `nanoff` flashing tool. Install from the official .NET download page (search "download dotnet sdk"). Pick the latest LTS (.NET 8 is fine).

After install, open a fresh PowerShell and check:

```powershell
dotnet --version
```

Should print something like `8.0.404`. If it errors, the install didn't add `dotnet` to PATH — log out and back in, or restart.

### 1b. Visual Studio 2022 + nanoFramework extension

1. Install **Visual Studio 2022 Community** (free) from Microsoft if you don't have it. Make sure ".NET desktop development" workload is checked during install.
2. In Visual Studio: **Extensions → Manage Extensions → Online**, search for **".NET nanoFramework"**, install it.
3. Restart Visual Studio.

### 1c. nanoff CLI

This is the tool that flashes the nanoFramework runtime onto the ESP32. Open PowerShell:

```powershell
dotnet tool install -g nanoff
```

Verify:

```powershell
nanoff --help
```

If it's not found after install, your PATH doesn't include `%USERPROFILE%\.dotnet\tools` — add it, or restart the terminal.

---

## 2. Plug in the CoreS3

Connect via USB-C. Two LEDs on the back should light up.

Open **Device Manager → Ports (COM & LPT)**. You should see a new entry like:

- `USB-Enhanced-SERIAL CH9102 (COM5)` — the most common chip on recent CoreS3 batches
- `Silicon Labs CP210x USB to UART Bridge (COM5)` — older batches
- `USB Serial Device (COM5)` — if Windows already had a generic driver

**Note the COM port number** — you'll use it everywhere below. (Examples in this doc use `COM5`; substitute yours.)

If no port appears:

- Try a different USB-C cable (charge-only cables are extremely common).
- Install the CH9102 driver from WCH's website, or the CP210x driver from Silicon Labs, depending on which chip you see in Device Manager → Other devices (it'll show as an "Unknown device" if the driver is missing).

---

## 3. Flash the nanoFramework runtime

This is a one-time step per device. It replaces the factory firmware with the nanoFramework runtime; after this the device runs whatever .NET assembly you deploy from Visual Studio.

```powershell
nanoff --target ESP32_S3 --serialport COM5 --update --masserase
```

**Why each flag matters:**

| Flag | Reason |
|---|---|
| `ESP32_S3` | Plain ESP32_S3 image. We tested both `ESP32_S3` and `ESP32_S3_BLE` on the CoreS3 — both work and Visual Studio Debug sees both, despite earlier guidance saying you needed a `_UART` variant. The plain image gives the most flash room for user code. **Note**: `M5Core_S3` is *not* a real nanoff target — don't try that. |
| `--masserase` | Wipes flash before writing instead of doing the default read-then-write backup step. The backup step regularly fails partway through on first flash with a SLIP-frame timeout — `--masserase` skips it entirely. |

Confirm what targets are actually available on your installed `nanoff`:

```powershell
nanoff --listtargets | Select-String S3
```

You should see at least:

```
ESP32_S3_BLE_UART
ESP32_S3_BLE
ESP32_S3_ALL_UART
ESP32_S3
ESP32_S3_ALL
```

Expected output of the flash command (abridged):

```
Reading details from chip...OK
Connected to: ESP32-S3 (...)
Extracting ESP32_S3_ALL_UART-1.x.x.zip...OK
Updating to 1.x.x
Erasing flash...
Flashing successful!
```

Total time: ~1–2 minutes. The device will reboot automatically.

---

## 4. Open the solution

1. Clone or pull the latest:
   ```powershell
   cd C:\Users\<you>\solar-tracker
   git pull
   ```
2. Open `SolarTracker.sln` in Visual Studio 2022.
3. **Tools → Options → nanoFramework → Device Explorer** — pick the COM port. The CoreS3 should show up as `nanoFramework @ COM5` once detected.
4. **Solution Explorer**: right-click the solution → **Restore NuGet Packages**. First-time restore can take a couple of minutes.

---

## 5. Deploy the bring-up test

In Solution Explorer, right-click **SolarTracker.Bringup** → **Set as Startup Project**. Then **Debug → Start Debugging** (F5).

What you should see, in order:

| Stage | Display | Output (View → Output → "nanoFramework Debug") |
|---|---|---|
| Boot | Black | `BRINGUP: starting` |
| PMIC ready | Black | `BRINGUP: PMIC enabled` |
| Display init | Flashes red → green → blue → black | `BRINGUP: display initialised` |
| Idle | "BRINGUP TEST" header, "DISPLAY OK" green, "TOUCH SOMEWHERE" amber | `BRINGUP: touch ready` |
| Touch | White square at finger, "X=… Y=…" updates below | `BRINGUP: tap N at <x>,<y>` |

If you get all the way through to drawing squares where you tap, **everything's working**. You're ready to wire up the sensors.

---

## 6. Troubleshooting

### Screen stays completely black

Most likely cause: AXP2101 didn't enable the display rails. Check the Output window:

- If `BRINGUP: starting` prints but `BRINGUP: PMIC enabled` does *not* → the I²C write to the PMIC threw. Probably wrong I²C bus pin numbers or wrong PMIC address. Verify pin 11/12 in `Program.cs` against the CoreS3 schematic for your hardware revision.
- If `BRINGUP: PMIC enabled` prints but the screen never flashes red → the rail-enable bits in `Hardware/Axp2101.cs` are wrong for your unit. The values come from M5Stack's reference firmware; some board revisions move the LCD rail between LDOs. Compare against M5Stack's published `M5CoreS3.cpp`.

### Screen flashes colors but text is mirrored / wrong colors

`MADCTL` (register `0x36`) in `Display.Init()` controls orientation and RGB/BGR ordering. Try changing the `Data(0x08);` line to `0x68`, `0xC8`, or `0x28` to find the right combination for your panel batch.

### Touch coordinates are mirrored

In `Hardware/Touch.cs`, after the `x` and `y` reads, add:

```csharp
x = Display.Width  - 1 - x;   // if X is mirrored
y = Display.Height - 1 - y;   // if Y is mirrored
```

The CoreS3 panel orientation and the FT6336U's reported coordinates don't always agree out of the box — this is the spot to adjust.

### "Cannot find or open a device" on F5

- Re-check that **Device Explorer** shows the device (Tools → Options → nanoFramework → Device Explorer → refresh).
- Close any other tool that has the COM port open (PuTTY, the previous `nanoff` window, Arduino IDE, etc.). Only one app can hold the port.
- Unplug + replug the USB cable. Sometimes the previous deploy leaves the device in a wedged state.

### `nanoff` fails at "Failed to connect to ESP32 bootloader"

The CoreS3's auto-reset over USB isn't reliable. Use the dedicated download-mode button — it's the small recessed button on the **front-bottom-right of the CoreS3**, just to the right of the SD card slot (label: *"PRESS: REBOOT, HOLD 3S: ENTER DOWNLOAD MODE (GREEN LED ON)"*).

1. With USB plugged in and the device on, **press and hold that button for 3 full seconds**.
2. Watch for the **green LED** to turn on — that confirms the chip is now in ROM bootloader mode.
3. Release the button.
4. Now run the `nanoff` command from step 3 above.

**Don't confuse this with the side power button** — that one only powers the device on/off and won't put it into download mode.

### `nanoff` fails partway through with "No complete SLIP frame received within 30000ms"

This is the backup-config step timing out — it happens regularly on first flash. Two fixes:

- Make sure you have `--masserase` in the command (skips backup entirely).
- If still failing, lower the baud: add `--baud 115200`.

If it's *still* failing, the USB cable / port is dropping bytes:

- Try a different USB-C cable (use one you've confirmed works for data on something else).
- Plug into a port directly on the motherboard, not through a hub or front-panel port.

### Deploy says "no debugger attached" or hangs

Run `nanoff --target ESP32_S3 --serialport COM5 --devicedetails` — confirms the runtime is alive. If the runtime version doesn't match what your project was built against, re-run the `--update` from step 3.

### Deploy succeeds but `Link failure: needs assembly 'X.Y.Z' (1.1.29.0)` in Debug output

The runtime image has *specific* assembly versions of `System.Device.I2c`, `System.Device.Spi`, and `System.Device.Gpio` baked in. NuGet's *latest* package versions tend to be one patch ahead of what the runtime expects, and the runtime refuses to link them. The fix: use the `-1 patch` versions, which are what we pin in `packages.config`:

| Package | Pinned (works) | Latest (won't link) |
|---|---|---|
| `nanoFramework.System.Device.Gpio` | `1.1.56` | 1.1.57 |
| `nanoFramework.System.Device.I2c` | `1.1.28` | 1.1.29 |
| `nanoFramework.System.Device.Spi` | `1.3.81` | 1.3.82 |

If a future runtime update changes which versions are bundled, the link error itself will tell you what it wants — the message format is `needs assembly 'X' (V.V.V.V)`, where V.V.V.V is the version your project references but the runtime doesn't have. Inspect `~/.nanoFramework/fw_cache/<target>/<target>-<version>.zip → native_assemblies.csv` for the runtime's bundled list, but the link error is usually all you need.

### Screen stays completely black even though `BRINGUP: display initialised` prints

The CoreS3 has an **AW9523 I/O expander chip** at I²C address `0x58` that controls the LCD reset line. Without writing its output register, the LCD stays in reset and ignores everything we send over SPI. The bring-up project handles this in `Hardware/Aw9523.cs`; if you build a new project from scratch, remember to copy that driver in and call `aw9523.InitForCoreS3()` *before* `display.Init()`.

### `SpiDevice.Create()` throws `ArgumentException` with empty message

nanoFramework's ESP32 SPI driver requires every signal to be assigned to a real GPIO — even on a write-only bus where MISO is unused. `Configuration.SetPinFunction(-1, DeviceFunction.SPI2_MISO)` doesn't satisfy it; nor does omitting the call. Pick an unused GPIO (we use `48` on the CoreS3) and assign it as `SPI2_MISO`. The CoreS3's LCD has no MISO line, so the assignment doesn't conflict with anything.

---

## 7. After the test passes

Once the bring-up project works end-to-end:

1. Wire the **MPU6886** into Port A of the I/O Hub.
2. Wire the **GPS module** into Port C of the Extension module.
3. Wire the **AS5600 + magnet** mechanically (later, when the parts arrive).
4. In Visual Studio: right-click **SolarTracker.Device** → **Set as Startup Project** → F5.

The full firmware will boot into the calibration screen on first run (because no `azimuth_zero` is in flash yet). Tap CONFIRM with the panel pointing south, and from then on every boot goes straight to the dashboard.
