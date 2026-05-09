using System.Device.I2c;

namespace SolarTracker.Device.Sensors
{
    // MPU6886 6-axis IMU on the M5Stack Grove I/O Hub.
    //
    // We only need the accelerometer for absolute tilt — gravity always
    // points down at 1g, so the accel vector tells us the panel's orientation
    // when at rest (which is all the time, for a slow-tracking solar panel).
    // Gyro stays unused for now; we'd add it for vibration/movement detection
    // or a complementary filter later.
    public sealed class Imu
    {
        public const byte DefaultAddress = 0x68;

        private const byte RegPwrMgmt1   = 0x6B;
        private const byte RegAccelXoutH = 0x3B;
        private const byte RegWhoAmI     = 0x75;

        // Default ±2g range gives 16384 LSB per g.
        private const double AccelLsbPerG = 16384.0;
        private const double RadToDeg = 57.29577951308232;

        private readonly I2cDevice _device;

        public Imu(I2cDevice device)
        {
            _device = device;
        }

        public bool Init()
        {
            byte[] who = new byte[1];
            _device.WriteRead(new byte[] { RegWhoAmI }, who);
            // 0x19 = MPU6886, 0x71 = MPU9250 fallback some clones return.
            if (who[0] != 0x19 && who[0] != 0x71) return false;

            // Wake from sleep, select PLL clock.
            _device.Write(new byte[] { RegPwrMgmt1, 0x01 });
            return true;
        }

        public TiltReading Read()
        {
            byte[] raw = new byte[6];
            _device.WriteRead(new byte[] { RegAccelXoutH }, raw);

            short rx = (short)((raw[0] << 8) | raw[1]);
            short ry = (short)((raw[2] << 8) | raw[3]);
            short rz = (short)((raw[4] << 8) | raw[5]);

            double ax = rx / AccelLsbPerG;
            double ay = ry / AccelLsbPerG;
            double az = rz / AccelLsbPerG;

            double pitch = System.Math.Atan2(-ax, System.Math.Sqrt(ay * ay + az * az)) * RadToDeg;
            double roll  = System.Math.Atan2(ay, az) * RadToDeg;

            return new TiltReading { PitchDeg = pitch, RollDeg = roll };
        }
    }
}
