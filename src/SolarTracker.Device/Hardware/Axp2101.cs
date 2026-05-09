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
            // Voltage values are in 100mV steps from a 500mV base where applicable.
            // 0x12 = 18 → 0.5V + 18 * 0.1V = 2.3V… we want 3.3V which is encoding 0x1C (28).
            // Reference firmware uses 0x1C (28) for the 3.3V rails.
            const byte volts3v3 = 0x1C;

            // LCD logic and reset / backlight on BLDO1, BLDO2, DLDO1.
            WriteReg(RegBldo1Volt, volts3v3);
            WriteReg(RegBldo2Volt, volts3v3);
            WriteReg(RegDldo1Volt, volts3v3);

            // Touch needs ALDO2 high.
            WriteReg(RegAldo2Volt, volts3v3);

            // Enable bits in LDO_ONOFF registers.
            //   RegLdoOnOff0: bit0=ALDO1 bit1=ALDO2 bit2=ALDO3 bit3=ALDO4
            //                 bit4=BLDO1 bit5=BLDO2 bit6=DLDO1 bit7=DLDO2
            byte on0 = ReadReg(RegLdoOnOff0);
            on0 |= 0x02; // ALDO2 (touch)
            on0 |= 0x10; // BLDO1 (LCD logic)
            on0 |= 0x20; // BLDO2 (LCD reset)
            on0 |= 0x40; // DLDO1 (backlight)
            WriteReg(RegLdoOnOff0, on0);

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
