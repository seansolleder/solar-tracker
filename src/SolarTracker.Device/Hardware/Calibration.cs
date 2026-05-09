using System.IO;

namespace SolarTracker.Device.Hardware
{
    // Persistent install-time calibration: which raw AS5600 reading corresponds
    // to "panel pointing due south." Stored on the ESP32-S3's internal flash —
    // non-volatile, no battery needed, survives reboots and motor power-loss.
    //
    // File format: 8 bytes.
    //   bytes 0..3 = magic header "SOLR"  (rejects garbage on first boot)
    //   bytes 4..7 = azimuth_zero, little-endian int32
    //
    // Internal flash is mounted at I:\ on nanoFramework ESP32-S3 builds. If we
    // ever need to wipe the calibration to force a re-install flow, deleting
    // the file is enough.
    public sealed class Calibration
    {
        private const string Path = "I:\\calibration.dat";
        private const byte Magic0 = (byte)'S';
        private const byte Magic1 = (byte)'O';
        private const byte Magic2 = (byte)'L';
        private const byte Magic3 = (byte)'R';

        public bool TryLoad(out int azimuthZero)
        {
            azimuthZero = 0;

            if (!File.Exists(Path)) return false;

            byte[] buf = File.ReadAllBytes(Path);
            if (buf.Length < 8) return false;
            if (buf[0] != Magic0 || buf[1] != Magic1 || buf[2] != Magic2 || buf[3] != Magic3) return false;

            azimuthZero =  buf[4]
                        | (buf[5] << 8)
                        | (buf[6] << 16)
                        | (buf[7] << 24);
            return true;
        }

        public void Save(int azimuthZero)
        {
            byte[] buf = new byte[8];
            buf[0] = Magic0;
            buf[1] = Magic1;
            buf[2] = Magic2;
            buf[3] = Magic3;
            buf[4] = (byte)( azimuthZero        & 0xFF);
            buf[5] = (byte)((azimuthZero >> 8)  & 0xFF);
            buf[6] = (byte)((azimuthZero >> 16) & 0xFF);
            buf[7] = (byte)((azimuthZero >> 24) & 0xFF);

            File.WriteAllBytes(Path, buf);
        }
    }
}
