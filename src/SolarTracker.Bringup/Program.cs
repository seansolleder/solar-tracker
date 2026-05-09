using System.Device.Gpio;
using System.Device.I2c;
using System.Device.Spi;
using System.Diagnostics;
using System.Threading;

using nanoFramework.Hardware.Esp32;

using SolarTracker.Device.Hardware;

namespace SolarTracker.Bringup
{
    // Hardware bring-up test for the M5Stack CoreS3.
    //
    // What it proves, in order:
    //   1) nanoFramework runs (BRINGUP: starting prints below).
    //   2) Internal I²C works (AXP2101 PMIC responds, display rails come up).
    //   3) SPI + ILI9342C work (screen flashes red/green/blue, then text).
    //   4) FT6336U touch works (tapping draws a square at the touch point).
    public class Program
    {
        // Bump on every Bringup change so we can confirm the device is
        // running the latest pushed code.
        private const string Version = "bringup-v9-aw9523";

        // Pin map matches the M5Stack CoreS3 reference schematic.
        // nanoFramework's DeviceFunction only exposes SPI1_* and SPI2_*
        // on this chip, so SPI3 isn't available. Sticking with bus 2.
        private const int LcdMosi = 37;
        private const int LcdSck  = 36;
        private const int LcdCs   = 3;
        private const int LcdDc   = 35;
        private const int LcdSpiBus = 2;

        private const int InternalSda = 12;
        private const int InternalScl = 11;
        private const int InternalI2cBus = 1;

        public static void Main()
        {
            Debug.WriteLine("BRINGUP: ===== " + Version + " =====");
            Debug.WriteLine("BRINGUP: starting");

            // 1) PMIC — without this the display rails are off.
            Configuration.SetPinFunction(InternalSda, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(InternalScl, DeviceFunction.I2C1_CLOCK);

            I2cDevice pmicI2c = I2cDevice.Create(new I2cConnectionSettings(InternalI2cBus, Axp2101.DefaultAddress));
            Axp2101 pmic = new Axp2101(pmicI2c);
            pmic.EnableDisplayAndTouch();
            Debug.WriteLine("BRINGUP: PMIC enabled");

            // 1b) AW9523 I/O expander — releases LCD reset. The CoreS3 routes
            // LCD_RST through this chip rather than a direct GPIO, so without
            // configuring it, the panel stays in reset and ignores SPI even
            // though the rails are powered.
            I2cDevice aw9523I2c = I2cDevice.Create(new I2cConnectionSettings(InternalI2cBus, Aw9523.DefaultAddress));
            Aw9523 aw9523 = new Aw9523(aw9523I2c);
            aw9523.InitForCoreS3();
            Thread.Sleep(50); // let LCD come out of reset
            Debug.WriteLine("BRINGUP: AW9523 initialised, LCD released from reset");

            // 2) Display. Bus 2 / SPI2_* with MISO assigned to an unused
            // pin (driver requires a real GPIO).
            Configuration.SetPinFunction(LcdMosi, DeviceFunction.SPI2_MOSI);
            Configuration.SetPinFunction(LcdSck,  DeviceFunction.SPI2_CLOCK);
            Configuration.SetPinFunction(48,      DeviceFunction.SPI2_MISO);

            GpioController gpio = new GpioController();
            GpioPin dc = gpio.OpenPin(LcdDc, PinMode.Output);
            dc.Write(PinValue.High);

            SpiConnectionSettings spi = new SpiConnectionSettings(LcdSpiBus, LcdCs)
            {
                ClockFrequency = 10_000_000,
                Mode = SpiMode.Mode0
            };
            Display display = new Display(SpiDevice.Create(spi), dc);
            display.Init();
            Debug.WriteLine("BRINGUP: display initialised");

            // Colour-bar test — confirms RGB565 mapping and full-screen draw.
            display.FillScreen(Display.ColorRed);   Thread.Sleep(400);
            display.FillScreen(Display.ColorGreen); Thread.Sleep(400);
            display.FillScreen(Display.ColorBlue);  Thread.Sleep(400);
            display.FillScreen(Display.ColorBlack);

            display.DrawText( 20,  20, "BRINGUP TEST",     Display.ColorWhite, Display.ColorBlack, 3);
            display.DrawText( 20,  70, "DISPLAY OK",       Display.ColorGreen, Display.ColorBlack, 2);
            display.DrawText( 20, 100, "TOUCH SOMEWHERE",  Display.ColorAmber, Display.ColorBlack, 2);

            // 3) Touch.
            I2cDevice touchI2c = I2cDevice.Create(new I2cConnectionSettings(InternalI2cBus, Touch.DefaultAddress));
            Touch touch = new Touch(touchI2c);
            Debug.WriteLine("BRINGUP: touch ready");

            int taps = 0;
            int lastX = -1, lastY = -1;

            while (true)
            {
                if (touch.TryRead(out int tx, out int ty))
                {
                    if (tx != lastX || ty != lastY)
                    {
                        if (lastX >= 0) display.FillRect(lastX - 5, lastY - 5, 10, 10, Display.ColorBlack);
                        display.FillRect(tx - 5, ty - 5, 10, 10, Display.ColorWhite);

                        display.FillRect(20, 200, 280, 18, Display.ColorBlack);
                        display.DrawText(20, 200,
                            "X=" + tx.ToString() + " Y=" + ty.ToString(),
                            Display.ColorGreen, Display.ColorBlack, 2);

                        taps++;
                        Debug.WriteLine("BRINGUP[" + Version + "]: tap " + taps.ToString() + " at " + tx.ToString() + "," + ty.ToString());
                        lastX = tx; lastY = ty;
                    }
                }
                Thread.Sleep(50);
            }
        }
    }
}
