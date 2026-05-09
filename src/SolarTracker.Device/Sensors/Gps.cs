using System.IO.Ports;
using System.Threading;

namespace SolarTracker.Device.Sensors
{
    // ATGM336H GPS, 9600 baud NMEA-0183.
    // We only parse the RMC sentence — it carries everything the dashboard
    // needs (UTC time, lat, lon, fix status). GGA/GLL would be redundant.
    //
    // Background thread reads bytes from UART, accumulates a line at a time,
    // and writes the latest fix into _latest. Read it with TryGetFix().
    public sealed class Gps
    {
        private readonly SerialPort _port;
        private readonly object _lock = new object();
        private GpsFix _latest = new GpsFix();
        private Thread _reader;

        public Gps(string portName)
        {
            _port = new SerialPort(portName, 9600)
            {
                ReadTimeout = 1000,
                NewLine = "\r\n"
            };
        }

        public void Start()
        {
            _port.Open();
            _reader = new Thread(ReadLoop) { IsBackground = true };
            _reader.Start();
        }

        public GpsFix Snapshot()
        {
            lock (_lock)
            {
                GpsFix copy = new GpsFix
                {
                    HasFix    = _latest.HasFix,
                    Latitude  = _latest.Latitude,
                    Longitude = _latest.Longitude,
                    UtcHour   = _latest.UtcHour,
                    UtcMinute = _latest.UtcMinute,
                    UtcSecond = _latest.UtcSecond
                };
                return copy;
            }
        }

        private void ReadLoop()
        {
            // Hand-rolled line accumulator: SerialPort.ReadLine throws on
            // timeout in nanoFramework, which makes the loop noisy. Reading
            // bytes and splitting on \n is more predictable.
            byte[] buf = new byte[128];
            string line = "";

            while (true)
            {
                int n;
                try { n = _port.Read(buf, 0, buf.Length); }
                catch { Thread.Sleep(50); continue; }

                for (int i = 0; i < n; i++)
                {
                    char c = (char)buf[i];
                    if (c == '\n')
                    {
                        ProcessLine(line);
                        line = "";
                    }
                    else if (c != '\r')
                    {
                        line += c;
                        if (line.Length > 120) line = ""; // garbage protection
                    }
                }
            }
        }

        private void ProcessLine(string line)
        {
            // We accept any talker prefix — GP, GN, BD all valid for this module.
            if (line.Length < 7) return;
            if (line[0] != '$') return;
            if (line[3] != 'R' || line[4] != 'M' || line[5] != 'C') return;

            string[] f = line.Split(',');
            // $xxRMC,time,status,lat,N/S,lon,E/W,speed,course,date,...
            if (f.Length < 7) return;

            bool valid = f[2] == "A";
            int hh = 0, mm = 0, ss = 0;
            if (f[1].Length >= 6)
            {
                hh = ParseInt(f[1], 0, 2);
                mm = ParseInt(f[1], 2, 2);
                ss = ParseInt(f[1], 4, 2);
            }

            double lat = NmeaToDegrees(f[3]);
            if (f[4] == "S") lat = -lat;
            double lon = NmeaToDegrees(f[5]);
            if (f[6] == "W") lon = -lon;

            lock (_lock)
            {
                _latest.HasFix = valid;
                _latest.Latitude = lat;
                _latest.Longitude = lon;
                _latest.UtcHour = hh;
                _latest.UtcMinute = mm;
                _latest.UtcSecond = ss;
            }
        }

        // NMEA encodes lat/lon as ddmm.mmmm (or dddmm.mmmm for longitude).
        // Convert to signed decimal degrees.
        private static double NmeaToDegrees(string s)
        {
            if (s == null || s.Length < 4) return 0.0;
            int dot = s.IndexOf('.');
            if (dot < 3) return 0.0;
            int degDigits = dot - 2;
            int deg = ParseInt(s, 0, degDigits);
            double minutes = ParseDouble(s, degDigits, s.Length - degDigits);
            return deg + minutes / 60.0;
        }

        private static int ParseInt(string s, int start, int len)
        {
            int v = 0;
            for (int i = 0; i < len; i++)
            {
                char c = s[start + i];
                if (c < '0' || c > '9') break;
                v = v * 10 + (c - '0');
            }
            return v;
        }

        private static double ParseDouble(string s, int start, int len)
        {
            double whole = 0;
            double frac = 0;
            double scale = 1;
            bool seenDot = false;
            for (int i = 0; i < len; i++)
            {
                char c = s[start + i];
                if (c == '.') { seenDot = true; continue; }
                if (c < '0' || c > '9') break;
                if (!seenDot) whole = whole * 10 + (c - '0');
                else { frac = frac * 10 + (c - '0'); scale *= 10; }
            }
            return whole + frac / scale;
        }
    }
}
