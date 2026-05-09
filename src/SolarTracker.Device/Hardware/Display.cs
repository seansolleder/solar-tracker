using System.Device.Gpio;
using System.Device.Spi;
using System.Threading;

namespace SolarTracker.Device.Hardware
{
    // ILI9342C 320x240 SPI display on the M5Stack CoreS3.
    // Pin map (CoreS3 reference schematic):
    //   MOSI = GPIO37, SCK = GPIO36, CS = GPIO3, DC = GPIO35, RST = -1 (handled by AXP2101)
    //
    // This is intentionally a thin driver: full-screen colour fill, rectangle
    // fill, and 5x7 bitmap-font text. That's all the dashboard needs.
    public sealed class Display
    {
        public const int Width  = 320;
        public const int Height = 240;

        public const ushort ColorBlack = 0x0000;
        public const ushort ColorWhite = 0xFFFF;
        public const ushort ColorRed   = 0xF800;
        public const ushort ColorGreen = 0x07E0;
        public const ushort ColorBlue  = 0x001F;
        public const ushort ColorAmber = 0xFD20;

        private readonly SpiDevice _spi;
        private readonly GpioPin _dc;
        private readonly byte[] _scratch = new byte[2];

        public Display(SpiDevice spi, GpioPin dc)
        {
            _spi = spi;
            _dc = dc;
        }

        public void Init()
        {
            // ILI9342C init sequence — same family as ILI9341 with a couple of
            // CoreS3-specific values (MADCTL inverts so colours are RGB and the
            // panel's native orientation matches a landscape dashboard).
            Cmd(0x01); Thread.Sleep(120);     // Software reset
            Cmd(0x11); Thread.Sleep(120);     // Sleep out

            Cmd(0x3A); Data(0x55);            // 16-bit colour (RGB565)
            Cmd(0x36); Data(0x08);            // MADCTL: BGR=0, MY=0, MX=0, MV=0 — landscape, RGB
            Cmd(0xB0); Data(0x00);            // Interface mode

            // Inversion + frame rate — values straight from M5Stack reference
            Cmd(0x21);                        // Display inversion ON (CoreS3 panels need this)
            Cmd(0xB1); Data(0x00); Data(0x18);

            // Display on
            Cmd(0x29); Thread.Sleep(20);

            FillScreen(ColorBlack);
        }

        public void FillScreen(ushort color)
        {
            FillRect(0, 0, Width, Height, color);
        }

        public void FillRect(int x, int y, int w, int h, ushort color)
        {
            SetWindow(x, y, x + w - 1, y + h - 1);
            Cmd(0x2C);
            _dc.Write(PinValue.High);

            byte hi = (byte)(color >> 8);
            byte lo = (byte)(color & 0xFF);

            // Stream 32 pixels per SPI write to keep buffer use small.
            byte[] line = new byte[64];
            for (int i = 0; i < line.Length; i += 2) { line[i] = hi; line[i + 1] = lo; }

            int pixels = w * h;
            while (pixels > 0)
            {
                int chunk = pixels >= 32 ? 32 : pixels;
                _spi.Write(new System.SpanByte(line, 0, chunk * 2));
                pixels -= chunk;
            }
        }

        public void DrawText(int x, int y, string text, ushort color, ushort background, int scale = 2)
        {
            for (int i = 0; i < text.Length; i++)
            {
                DrawChar(x + i * 6 * scale, y, text[i], color, background, scale);
            }
        }

        private void DrawChar(int x, int y, char c, ushort color, ushort background, int scale)
        {
            int idx = (c >= 32 && c <= 95) ? (c - 32) : 0;
            byte[] glyph = Font5x7.Glyph(idx);

            for (int col = 0; col < 5; col++)
            {
                byte bits = glyph[col];
                for (int row = 0; row < 7; row++)
                {
                    bool on = (bits & (1 << row)) != 0;
                    FillRect(x + col * scale, y + row * scale, scale, scale, on ? color : background);
                }
            }
            // 1-pixel column gap (uses background colour)
            FillRect(x + 5 * scale, y, scale, 7 * scale, background);
        }

        private void SetWindow(int x0, int y0, int x1, int y1)
        {
            Cmd(0x2A); // Column address
            Data((byte)(x0 >> 8)); Data((byte)x0);
            Data((byte)(x1 >> 8)); Data((byte)x1);
            Cmd(0x2B); // Page address
            Data((byte)(y0 >> 8)); Data((byte)y0);
            Data((byte)(y1 >> 8)); Data((byte)y1);
        }

        private void Cmd(byte c)
        {
            _dc.Write(PinValue.Low);
            _scratch[0] = c;
            _spi.Write(new System.SpanByte(_scratch, 0, 1));
        }

        private void Data(byte d)
        {
            _dc.Write(PinValue.High);
            _scratch[0] = d;
            _spi.Write(new System.SpanByte(_scratch, 0, 1));
        }
    }
}
