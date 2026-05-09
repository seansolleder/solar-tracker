using System.Device.I2c;

namespace SolarTracker.Device.Hardware
{
    // AW9523B 16-bit I/O expander on the M5Stack CoreS3, address 0x58 on the
    // internal I²C bus. Several CoreS3 hardware functions are wired through
    // this chip rather than direct ESP32-S3 GPIOs, including the LCD reset
    // line, audio amplifier reset, and bus enables. Without configuring it,
    // the LCD stays in reset and ignores everything we send over SPI — so
    // the screen looks dark even though the AXP2101 has powered the rails.
    //
    // Init sequence and register values taken straight from M5GFX's CoreS3
    // bring-up (M5GFX.cpp). Magic numbers; left as-is to mirror the upstream.
    public sealed class Aw9523
    {
        public const byte DefaultAddress = 0x58;

        private readonly I2cDevice _device;

        public Aw9523(I2cDevice device)
        {
            _device = device;
        }

        public void InitForCoreS3()
        {
            WriteReg(0x02, 0x07); // P0 output state
            WriteReg(0x03, 0x03); // P1 output state — releases LCD reset
            WriteReg(0x04, 0x18); // P0 direction (1 = input on bits 3,4)
            WriteReg(0x05, 0x0C); // P1 direction (1 = input on bits 2,3)
            WriteReg(0x11, 0x10); // GCR — push-pull on P0
            WriteReg(0x12, 0xFF); // P0 LED-mode disable (use as plain GPIO)
            WriteReg(0x13, 0xFF); // P1 LED-mode disable
        }

        private void WriteReg(byte register, byte value)
        {
            _device.Write(new byte[] { register, value });
        }
    }
}
