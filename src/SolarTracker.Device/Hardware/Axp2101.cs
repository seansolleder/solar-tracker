using System.Device.I2c;
using System.Threading;

namespace SolarTracker.Device.Hardware
{
    // AXP2101 PMIC on the M5Stack CoreS3.
    // Until this chip is told to enable the right rails, the LCD backlight, the
    // LCD logic supply, and the touch panel are all powered down — so the screen
    // stays dark. Configure once at boot before talking to the display or touch.
    //
    // Rail map for the CoreS3 (from M5Stack's reference firmware):
    //   ALDO1 = 1.8V — camera DVDD
    //   ALDO2 = 3.3V — TP RST line / extra peripherals
    //   ALDO3 = 3.3V — speaker / audio
    //   ALDO4 = 3.3V — TF card
    //   BLDO1 = 3.3V — LCD logic supply
    //   BLDO2 = 3.3V — LCD reset / backlight
    //   DLDO1 = 3.3V — backlight rail
    //
    // We enable just what's needed for display + touch. Other rails left alone.
    public sealed class Axp2101
    {
        public const byte DefaultAddress = 0x34;

        private const byte RegLdoOnOff0   = 0x90;
        private const byte RegLdoOnOff1   = 0x91;
        private const byte RegAldo1Volt   = 0x92;
        private const byte RegAldo2Volt   = 0x93;
        private const byte RegAldo3Volt   = 0x94;
        private const byte RegAldo4Volt   = 0x95;
        private const byte RegBldo1Volt   = 0x96;
        private const byte RegBldo2Volt   = 0x97;
        private const byte RegDldo1Volt   = 0x99;

        private readonly I2cDevice _device;

        public Axp2101(I2cDevice device)
        {
            _device = device;
        }

        public void EnableDisplayAndTouch()
        {
            // 0x1C = 28 → 0.5V + 28 * 0.1V = 3.3V on this rail's voltage register.
            const byte volts3v3 = 0x1C;

            WriteReg(RegBldo1Volt, volts3v3); // LCD logic
            WriteReg(RegBldo2Volt, volts3v3); // LCD reset
            WriteReg(RegDldo1Volt, volts3v3); // backlight
            WriteReg(RegAldo2Volt, volts3v3); // touch

            // Enable rails. Value taken straight from M5GFX's CoreS3 init —
            // 0xBF = ALDO1..4 + BLDO1..2 + DLDO at the bit M5Stack picked.
            // Their bit-7-is-DLDO1 mapping doesn't match the public AXP2101
            // datasheet, but matches what the on-board chip actually does.
            WriteReg(RegLdoOnOff0, 0xBF);

            // Give rails a moment to come up before the display init runs.
            Thread.Sleep(50);
        }

        private void WriteReg(byte register, byte value)
        {
            _device.Write(new byte[] { register, value });
        }

        private byte ReadReg(byte register)
        {
            byte[] read = new byte[1];
            _device.WriteRead(new byte[] { register }, read);
            return read[0];
        }
    }
}
