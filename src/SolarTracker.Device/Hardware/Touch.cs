using System.Device.I2c;

namespace SolarTracker.Device.Hardware
{
    // FT6336U capacitive touch controller on the M5Stack CoreS3.
    // Lives on the internal I²C bus alongside the AXP2101 PMIC.
    //
    // The chip can report up to two simultaneous touches; we only care about
    // the first one — single-tap UI is all the calibration flow needs.
    public sealed class Touch
    {
        public const byte DefaultAddress = 0x38;

        private readonly I2cDevice _device;

        public Touch(I2cDevice device)
        {
            _device = device;
        }

        // Returns true when a finger is currently on the screen, with x/y in
        // display pixel coordinates (0..319 / 0..239). Returns false when
        // there's no touch — tx/ty are then 0.
        public bool TryRead(out int x, out int y)
        {
            x = 0; y = 0;

            byte[] buf = new byte[7];
            _device.WriteRead(new byte[] { 0x00 }, buf);

            int touches = buf[2] & 0x0F;
            if (touches == 0) return false;

            // Touch 1: bytes 3..6
            //   byte 3: high nibble = event flag, low nibble = X high
            //   byte 4: X low
            //   byte 5: high nibble = touch id, low nibble = Y high
            //   byte 6: Y low
            x = ((buf[3] & 0x0F) << 8) | buf[4];
            y = ((buf[5] & 0x0F) << 8) | buf[6];
            return true;
        }
    }
}
