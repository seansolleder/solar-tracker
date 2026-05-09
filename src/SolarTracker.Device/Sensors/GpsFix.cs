namespace SolarTracker.Device.Sensors
{
    public sealed class GpsFix
    {
        public bool HasFix;        // RMC status A=valid
        public double Latitude;    // signed decimal degrees, +N -S
        public double Longitude;   // signed decimal degrees, +E -W
        public int UtcHour;
        public int UtcMinute;
        public int UtcSecond;
    }
}
