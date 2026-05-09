using System.Device.I2c;

namespace SolarTracker.Device.Sensors
{
    public sealed class As5600Status
    {
        public bool MagnetDetected;     // MD bit — magnet present
        public bool MagnetTooWeak;      // ML bit — magnet too far away
        public bool MagnetTooStrong;    // MH bit — magnet too close

        public bool IsHealthy
        {
            get { return MagnetDetected && !MagnetTooWeak && !MagnetTooStrong; }
        }
    }

    // AS5600 12-bit absolute magnetic encoder over I²C.
    // Address is fixed at 0x36 — only one of these per bus without a mux.
    //
    // We use the filtered ANGLE register (0x0E/0x0F), not RAW_ANGLE — the
    // chip's built-in averaging trades a tiny bit of latency for less jitter,
    // which is the right call for a slow-tracking solar panel.
    public sealed class As5600
    {
        public const byte DefaultAddress = 0x36;

        // Resolution constants — 12-bit value covers a full revolution.
        public const int MaxRaw = 4096;
        public const double DegreesPerCount = 360.0 / 4096.0;

        private const byte RegStatus = 0x0B;
        private const byte RegAngleH = 0x0E;

        private const byte BitMagnetTooStrong = 0x08; // MH
        private const byte BitMagnetTooWeak   = 0x10; // ML
        private const byte BitMagnetDetected  = 0x20; // MD

        private readonly I2cDevice _device;

        public As5600(I2cDevice device)
        {
            _device = device;
        }

        // Returns the current angle as a 12-bit count, 0–4095. Repeat reads
        // every loop — the chip has no concept of "stale" data, every read
        // reflects the magnet's orientation at that instant.
        public int ReadRaw()
        {
            byte[] raw = new byte[2];
            _device.WriteRead(new byte[] { RegAngleH }, raw);
            return ((raw[0] & 0x0F) << 8) | raw[1];
        }

        public As5600Status ReadStatus()
        {
            byte[] s = new byte[1];
            _device.WriteRead(new byte[] { RegStatus }, s);
            return new As5600Status
            {
                MagnetDetected  = (s[0] & BitMagnetDetected)  != 0,
                MagnetTooWeak   = (s[0] & BitMagnetTooWeak)   != 0,
                MagnetTooStrong = (s[0] & BitMagnetTooStrong) != 0
            };
        }

        // Convert a raw count + saved zero offset into a signed degrees value
        // in the range (-180, 180]. South is 0; east is +90; west is -90.
        public static double ToDegrees(int raw, int azimuthZero)
        {
            int delta = raw - azimuthZero;
            if (delta < 0) delta += MaxRaw;
            double deg = delta * DegreesPerCount;
            if (deg > 180.0) deg -= 360.0;
            return deg;
        }
    }
}
