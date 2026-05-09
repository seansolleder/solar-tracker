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
        // Bump this on every commit so we can confirm the device is running
        // the latest pushed code (vs a stale cached build).
        private const string Version = "ref-test-v3-downgraded";

        public static void Main()
        {
            Debug.WriteLine("BRINGUP: ===== " + Version + " =====");

            // Force every suspect assembly to be linked by allocating an
            // instance from each one. `new` calls survive the C# compiler's
            // optimisation pass; enum constants and typed-null locals don't.
            I2cConnectionSettings i2cs = new I2cConnectionSettings(1, 0x68);
            SpiConnectionSettings spis = new SpiConnectionSettings(2, 5);
            GpioController gpio = new GpioController();
            int pinFn = (int)DeviceFunction.SPI2_MOSI;

            Debug.WriteLine("BRINGUP: i2cs.BusId=" + i2cs.BusId.ToString());
            Debug.WriteLine("BRINGUP: spis.BusId=" + spis.BusId.ToString());
            Debug.WriteLine("BRINGUP: gpio.PinCount=" + gpio.PinCount.ToString());
            Debug.WriteLine("BRINGUP: SPI2_MOSI=" + pinFn.ToString());

            int n = 0;
            while (true)
            {
                Debug.WriteLine("BRINGUP[" + Version + "]: hello " + n.ToString());
                n++;
                Thread.Sleep(1000);
            }
        }
    }
}
