using System.Threading;

using SolarTracker.Device.Hardware;
using SolarTracker.Device.Sensors;

namespace SolarTracker.Device.Ui
{
    // Install-time calibration UI: shows the live AS5600 raw reading and
    // magnet status, then waits for the user to manually point the panel
    // due south and tap CONFIRM. Returns the captured raw value to be
    // saved as azimuth_zero.
    //
    //  ┌─────────── 320 ──────────┐
    //  │ CALIBRATION              │  red header
    //  │ POINT PANEL DUE SOUTH    │
    //  │                          │
    //  │  RAW    1847             │
    //  │  ANGLE  162.3 DEG        │
    //  │  MAGNET OK               │  red if not detected/weak/strong
    //  │                          │
    //  │ ┌──────────────────────┐ │
    //  │ │     CONFIRM SOUTH    │ │  green when magnet OK, gray otherwise
    //  │ └──────────────────────┘ │
    //  └──────────────────────────┘
    //
    // Touch zone for the confirm button: y >= 180 spans full width.
    public sealed class CalibrationScreen
    {
        private const int ConfirmZoneY = 180;
        private const int ConfirmZoneHeight = 56;

        private readonly Display _display;
        private readonly As5600 _encoder;
        private readonly Touch _touch;

        public CalibrationScreen(Display display, As5600 encoder, Touch touch)
        {
            _display = display;
            _encoder = encoder;
            _touch = touch;
        }

        // Blocks until the user taps CONFIRM with a healthy magnet reading.
        // Returns the raw value captured at that moment.
        public int Run()
        {
            DrawStatic();

            int lastRaw = -1;
            bool lastHealthy = false;
            bool wasTouching = false;

            while (true)
            {
                int raw = _encoder.ReadRaw();
                As5600Status status = _encoder.ReadStatus();

                if (raw != lastRaw || status.IsHealthy != lastHealthy)
                {
                    DrawLive(raw, status);
                    DrawConfirmButton(status.IsHealthy);
                    lastRaw = raw;
                    lastHealthy = status.IsHealthy;
                }

                bool touching = _touch.TryRead(out int tx, out int ty);
                if (touching && !wasTouching && status.IsHealthy && InConfirmZone(ty))
                {
                    return raw;
                }
                wasTouching = touching;

                Thread.Sleep(50);
            }
        }

        private void DrawStatic()
        {
            _display.FillScreen(Display.ColorBlack);
            _display.FillRect(0, 0, Display.Width, 18, Display.ColorRed);
            _display.DrawText(6, 2, "CALIBRATION", Display.ColorWhite, Display.ColorRed, 2);

            _display.DrawText(6, 28, "POINT PANEL DUE SOUTH", Display.ColorAmber, Display.ColorBlack, 2);

            _display.DrawText(6, 70,  "RAW",    Display.ColorWhite, Display.ColorBlack, 2);
            _display.DrawText(6, 100, "ANGLE",  Display.ColorWhite, Display.ColorBlack, 2);
            _display.DrawText(6, 130, "MAGNET", Display.ColorWhite, Display.ColorBlack, 2);
        }

        private void DrawLive(int raw, As5600Status status)
        {
            // Wipe the value columns then redraw — avoids ghosts when number
            // shrinks (e.g. 1847 → 999).
            _display.FillRect(110, 70, 200, 14, Display.ColorBlack);
            _display.DrawText(110, 70, raw.ToString(), Display.ColorGreen, Display.ColorBlack, 2);

            double deg = raw * As5600.DegreesPerCount;
            _display.FillRect(110, 100, 200, 14, Display.ColorBlack);
            _display.DrawText(110, 100, FormatDeg(deg), Display.ColorGreen, Display.ColorBlack, 2);

            _display.FillRect(110, 130, 200, 14, Display.ColorBlack);
            string label;
            ushort color;
            if (!status.MagnetDetected) { label = "MISSING";  color = Display.ColorRed; }
            else if (status.MagnetTooWeak)   { label = "TOO WEAK";   color = Display.ColorRed; }
            else if (status.MagnetTooStrong) { label = "TOO STRONG"; color = Display.ColorRed; }
            else                             { label = "OK";         color = Display.ColorGreen; }
            _display.DrawText(110, 130, label, color, Display.ColorBlack, 2);
        }

        private void DrawConfirmButton(bool enabled)
        {
            ushort fill = enabled ? Display.ColorGreen : (ushort)0x4208; // dim grey when disabled
            _display.FillRect(20, ConfirmZoneY, Display.Width - 40, ConfirmZoneHeight, fill);
            _display.DrawText(60, ConfirmZoneY + 18, "CONFIRM SOUTH", Display.ColorBlack, fill, 2);
        }

        private static bool InConfirmZone(int ty)
        {
            return ty >= ConfirmZoneY && ty < ConfirmZoneY + ConfirmZoneHeight;
        }

        private static string FormatDeg(double v)
        {
            int whole = (int)v;
            int frac = (int)((v - whole) * 10.0 + 0.5);
            return whole.ToString() + "." + frac.ToString() + " DEG";
        }
    }
}
