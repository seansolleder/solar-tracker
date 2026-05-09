using System.Device.Gpio;
using System.Device.I2c;
using System.Device.Spi;
using System.Diagnostics;
using System.Threading;

using nanoFramework.Hardware.Esp32;

namespace SolarTracker.Bringup
{
    // Diagnostic build — references one type from each suspect assembly so
    // they all actually get deployed (tree-shaker won't drop them). No
    // hardware calls yet; we just want to know whether *loading* the modules
    // breaks deploy or whether it's our runtime calls that crash the device.
    public class Program
    {
        public static void Main()
        {
            // One concrete reference per assembly to defeat tree-shaking.
            PinMode gpioRef = PinMode.Output;
            SpiMode spiRef = SpiMode.Mode0;
            DeviceFunction espRef = DeviceFunction.SPI2_MOSI;
            // I2c has no enum that's tiny enough — declare a typed null local.
            I2cConnectionSettings i2cRef = null;

            int n = 0;
            while (true)
            {
                Debug.WriteLine(
                    "BRINGUP: hello " + n.ToString() +
                    "  gpio=" + ((int)gpioRef).ToString() +
                    "  spi="  + ((int)spiRef).ToString() +
                    "  esp="  + ((int)espRef).ToString() +
                    "  i2c="  + (i2cRef == null ? "null" : "set"));
                n++;
                Thread.Sleep(1000);
            }
        }
    }
}
