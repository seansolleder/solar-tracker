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
        private const int LcdSck  = 36;
        private const int LcdCs   = 3;
        private const int LcdDc   = 35;
        private const int LcdSpiBus = 2;         // SPI2 / FSPI

        // Internal I2C (PMIC + touch) — separate from the Grove/Hub bus
        private const int InternalSda = 12;
        private const int InternalScl = 11;
        private const int InternalI2cBus = 1;

        // External Grove bus (Port A → I/O Hub → MPU6886 + AS5600 + future I2C devices)
        private const int GroveSda = 2;
        private const int GroveScl = 1;
        private const int GroveI2cBus = 0;

        // Port C UART (Extension module → ATGM336H GPS)
        private const int GpsTx = 17;
        private const int GpsRx = 18;
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

            // 3) Touch lives on the same internal I²C bus as the PMIC.
            I2cDevice touchI2c = I2cDevice.Create(new I2cConnectionSettings(InternalI2cBus, Touch.DefaultAddress));
            Touch touch = new Touch(touchI2c);

            // 4) Sensors on the Grove I²C bus — MPU6886 (tilt) + AS5600 (azimuth).
            Configuration.SetPinFunction(GroveSda, DeviceFunction.I2C2_DATA);
            Configuration.SetPinFunction(GroveScl, DeviceFunction.I2C2_CLOCK);

            I2cDevice imuI2c = I2cDevice.Create(new I2cConnectionSettings(GroveI2cBus, Imu.DefaultAddress));
            Imu imu = new Imu(imuI2c);
            bool imuOk = imu.Init();

            I2cDevice encI2c = I2cDevice.Create(new I2cConnectionSettings(GroveI2cBus, As5600.DefaultAddress));
            As5600 encoder = new As5600(encI2c);

            // 5) Load calibration from flash. If there isn't one, run the
            //    install-time calibration screen first — without a valid
            //    azimuth_zero, the dashboard's azimuth is meaningless.
            Calibration calStorage = new Calibration();
            int azimuthZero;
            bool calibrated = calStorage.TryLoad(out azimuthZero);

            CalibrationScreen calScreen = new CalibrationScreen(display, encoder, touch);
            if (!calibrated)
            {
                azimuthZero = calScreen.Run();
                calStorage.Save(azimuthZero);
                calibrated = true;
            }

            // 6) GPS reader.
            Configuration.SetPinFunction(GpsTx, DeviceFunction.COM2_TX);
            Configuration.SetPinFunction(GpsRx, DeviceFunction.COM2_RX);
            Gps gps = new Gps(GpsPort);
            gps.Start();

            // 7) Show the dashboard.
            Dashboard dashboard = new Dashboard(display);
            dashboard.DrawStatic();

            // 8) Render loop.
            //    - Read sensors, push to dashboard.
            //    - Poll touch; a tap on the AZIMUTH row re-enters calibration mode.
            TiltReading tilt = new TiltReading();
            bool wasTouching = false;

            while (true)
            {
                if (imuOk) tilt = imu.Read();
                GpsFix fix = gps.Snapshot();

                int rawAngle = encoder.ReadRaw();
                As5600Status encStatus = encoder.ReadStatus();
                double? azDeg = calibrated ? (double?)As5600.ToDegrees(rawAngle, azimuthZero) : null;

                dashboard.Update(fix, tilt, azDeg, encStatus.IsHealthy);

                bool touching = touch.TryRead(out int tx, out int ty);
                if (touching && !wasTouching && Dashboard.IsRecalibrateTap(ty))
                {
                    azimuthZero = calScreen.Run();
                    calStorage.Save(azimuthZero);
                    dashboard.DrawStatic();
                }
                wasTouching = touching;

                Thread.Sleep(200);
            }
        }
    }
}
