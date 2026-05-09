using System.Diagnostics;
using System.Threading;

namespace SolarTracker.Bringup
{
    // TEMPORARY minimal bring-up — proves the toolchain end-to-end without
    // any native-binding packages. If this deploys and prints, we re-add the
    // hardware references (Esp32, Gpio, Spi, I2c) one at a time to find which
    // one the runtime can't accept.
    public class Program
    {
        public static void Main()
        {
            int n = 0;
            while (true)
            {
                Debug.WriteLine("BRINGUP: hello " + n.ToString());
                n++;
                Thread.Sleep(1000);
            }
        }
    }
}
