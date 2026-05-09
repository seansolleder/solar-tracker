using SolarTracker.Device.Hardware;
using SolarTracker.Device.Sensors;

namespace SolarTracker.Device.Ui
{
    // Single-screen dashboard layout, 320x240, 5x7 font scaled 2x = 10x14 chars.
    //
    //  ┌─────────── 320 ──────────┐
    //  │  SOLAR TRACKER           │  16px header bar
    //  │--------------------------│
    //  │  GPS                     │
    //  │   LAT  +47.12345         │
    //  │   LON  -122.98765        │
    //  │   FIX  OK   12:34:56     │
    //  │--------------------------│
    //  │  PANEL TILT              │
    //  │   PITCH  +12.3 DEG       │
    //  │   ROLL   -01.4 DEG       │
    //  │--------------------------│
    //  │  AZIMUTH +12.3 DEG  RECAL│  tap row to recalibrate
    //  └──────────────────────────┘
    //
    // Layout uses fixed positions instead of dirty-rect tracking — cheaper than
    // it sounds because each row is a thin strip and we only redraw on change.
    public sealed class Dashboard
    {
        // Touch zone for "tap to recalibrate" — entire azimuth row.
        public const int RecalZoneY = 175;
        public const int RecalZoneHeight = 60;

        private readonly Display _display;

        public Dashboard(Display display)
        {
            _display = display;
        }

        public void DrawStatic()
        {
            _display.FillScreen(Display.ColorBlack);
            _display.FillRect(0, 0, Display.Width, 18, Display.ColorBlue);
            _display.DrawText(6, 2, "SOLAR TRACKER", Display.ColorWhite, Display.ColorBlue, 2);

            _display.DrawText(6, 28,  "GPS",         Display.ColorAmber, Display.ColorBlack, 2);
            _display.DrawText(6, 110, "PANEL TILT",  Display.ColorAmber, Display.ColorBlack, 2);
            _display.DrawText(6, 180, "AZIMUTH",     Display.ColorAmber, Display.ColorBlack, 2);
            _display.DrawText(252, 180, "RECAL",     (ushort)0x4208 /* dim grey */, Display.ColorBlack, 1);
        }

        public void Update(GpsFix gps, TiltReading tilt, double? azimuthDeg, bool magnetOk)
        {
            // GPS block
            DrawValue( 26,  50, "LAT", FormatLatLon(gps.Latitude),   gps.HasFix);
            DrawValue( 26,  72, "LON", FormatLatLon(gps.Longitude),  gps.HasFix);
            DrawValue( 26,  94, "FIX", gps.HasFix
                ? "OK  " + Pad2(gps.UtcHour) + ":" + Pad2(gps.UtcMinute) + ":" + Pad2(gps.UtcSecond)
                : "NO SIGNAL", gps.HasFix);

            // Tilt block
            DrawValue( 26, 132, "PITCH", FormatDeg(tilt.PitchDeg), true);
            DrawValue( 26, 154, "ROLL",  FormatDeg(tilt.RollDeg),  true);

            // Azimuth — three states:
            //   not calibrated → "NOT CAL", red
            //   calibrated but magnet bad → "MAG ERR", red
            //   healthy → angle in degrees, green
            string azText;
            bool ok;
            if (!azimuthDeg.HasValue)   { azText = "NOT CAL";  ok = false; }
            else if (!magnetOk)         { azText = "MAG ERR";  ok = false; }
            else                        { azText = FormatDeg(azimuthDeg.Value); ok = true; }
            DrawValue( 26, 202, "", azText, ok);
        }

        public static bool IsRecalibrateTap(int y)
        {
            return y >= RecalZoneY && y < RecalZoneY + RecalZoneHeight;
        }

        private void DrawValue(int x, int y, string label, string value, bool ok)
        {
            ushort fg = ok ? Display.ColorGreen : Display.ColorRed;
            // Wipe the row before writing — avoids ghost characters when the
            // new value is shorter than the previous one.
            _display.FillRect(x, y, 230, 18, Display.ColorBlack);
            if (label.Length > 0)
            {
                _display.DrawText(x, y, label, Display.ColorWhite, Display.ColorBlack, 2);
                _display.DrawText(x + label.Length * 12 + 6, y, value, fg, Display.ColorBlack, 2);
            }
            else
            {
                _display.DrawText(x, y, value, fg, Display.ColorBlack, 2);
            }
        }

        private static string FormatLatLon(double v)
        {
            char sign = v < 0 ? '-' : '+';
            if (v < 0) v = -v;
            int whole = (int)v;
            int frac = (int)((v - whole) * 100000.0 + 0.5);
            return sign + whole.ToString() + "." + Pad5(frac);
        }

        private static string FormatDeg(double v)
        {
            char sign = v < 0 ? '-' : '+';
            if (v < 0) v = -v;
            int whole = (int)v;
            int frac = (int)((v - whole) * 10.0 + 0.5);
            return sign + whole.ToString() + "." + frac.ToString() + " DEG";
        }

        private static string Pad2(int v)
        {
            if (v < 10) return "0" + v.ToString();
            return v.ToString();
        }

        private static string Pad5(int v)
        {
            string s = v.ToString();
            while (s.Length < 5) s = "0" + s;
            return s;
        }
    }
}
