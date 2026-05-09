using System.Device.Gpio;
using System.Device.I2c;
using System.Device.Spi;
using System.Threading;

using nanoFramework.Hardware.Esp32;

using SolarTracker.Device.Hardware;
using SolarTracker.Device.Sensors;
using SolarTracker.Device.Ui;

namespace SolarTracker.Device
{
    public class Program
    {
        // CoreS3 pin assignments — ESP32-S3 GPIOs from the M5Stack reference schematic.
        // Internal display SPI bus
        private const int LcdMosi = 37;
        private const int LcdMiso = -1;          // not used (write-only)
        private const int LcdSck  = 36;
        private const int LcdCs   = 3;
        private const int LcdDc   = 35;
        private const int LcdSpiBus = 2;         // SPI2 / FSPI

        // Internal I2C (PMIC, touch, onboard sensors) — separate from the Grove/Hub bus
        private const int InternalSda = 12;
        private const int InternalScl = 11;
        private const int InternalI2cBus = 1;

        // External Grove bus (Port A → I/O Hub → MPU6886 + AS5600 + future I2C devices)
        private const int GroveSda = 2;
        private const int GroveScl = 1;
        private const int GroveI2cBus = 0;

        // Port C UART (Extension module → ATGM336H GPS)
        private const int GpsTx = 17;            // CoreS3 → GPS RX (unused by GPS itself)
        private const int GpsRx = 18;            // CoreS3 ← GPS TX
        private const string GpsPort = "COM2";

        public static void Main()
        {
            // 1) Wake the PMIC and turn on the display + touch rails.
            Configuration.SetPinFunction(InternalSda, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(InternalScl, DeviceFunction.I2C1_CLOCK);

            I2cDevice pmicI2c = I2cDevice.Create(new I2cConnectionSettings(InternalI2cBus, Axp2101.DefaultAddress));
            Axp2101 pmic = new Axp2101(pmicI2c);
            pmic.EnableDisplayAndTouch();

            // 2) Bring up the display.
            Configuration.SetPinFunction(LcdMosi, DeviceFunction.SPI2_MOSI);
            Configuration.SetPinFunction(LcdSck,  DeviceFunction.SPI2_CLOCK);

            GpioController gpio = new GpioController();
            GpioPin dc = gpio.OpenPin(LcdDc, PinMode.Output);
            dc.Write(PinValue.High);

            SpiConnectionSettings spiSettings = new SpiConnectionSettings(LcdSpiBus, LcdCs)
            {
                ClockFrequency = 40_000_000,
                Mode = SpiMode.Mode0,
                DataBitLength = 8
            };
            SpiDevice spi = SpiDevice.Create(spiSettings);
            Display display = new Display(spi, dc);
            display.Init();

            // 3) Bring up the dashboard so something is on screen even before sensors respond.
            Dashboard dashboard = new Dashboard(display);
            dashboard.DrawStatic();

            // 4) Sensors on the Grove I2C bus.
            Configuration.SetPinFunction(GroveSda, DeviceFunction.I2C2_DATA);
            Configuration.SetPinFunction(GroveScl, DeviceFunction.I2C2_CLOCK);

            I2cDevice imuI2c = I2cDevice.Create(new I2cConnectionSettings(GroveI2cBus, Imu.DefaultAddress));
            Imu imu = new Imu(imuI2c);
            bool imuOk = imu.Init();

            // 5) GPS on Port C UART.
            Configuration.SetPinFunction(GpsTx, DeviceFunction.COM2_TX);
            Configuration.SetPinFunction(GpsRx, DeviceFunction.COM2_RX);
            Gps gps = new Gps(GpsPort);
            gps.Start();

            // 6) Main render loop. Keep this slow — the panel won't move
            //    fast enough to need anything more frequent.
            TiltReading tilt = new TiltReading();
            while (true)
            {
                if (imuOk) tilt = imu.Read();
                GpsFix fix = gps.Snapshot();

                dashboard.Update(fix, tilt, null /* azimuth — encoder pending */);

                Thread.Sleep(500);
            }
        }
    }
}
