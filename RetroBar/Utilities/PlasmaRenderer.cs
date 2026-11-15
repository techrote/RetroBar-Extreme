using System;

namespace RetroBar.Utilities
{
    /// <summary>
    /// Deterministic native renderer of PlasmaTerm's four-wave field.
    /// No terminal, child process, filesystem or WPF dependency.
    /// </summary>
    public static class PlasmaRenderer
    {
        public const int MaxPixels = 262144;
        private const int MaxSide = 1024;
        private const double Speed = 0.6;
        private static readonly uint[] Palette = BuildPalette();

        // Physical pixel dimensions in, sampled frame dimensions out.
        public static (int Width, int Height) RecommendedSize(double pixelWidth, double pixelHeight, int divisor = 4)
        {
            if (!double.IsFinite(pixelWidth) || !double.IsFinite(pixelHeight)
                || pixelWidth < 0 || pixelHeight < 0 || divisor < 1 || divisor > 8)
                throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            if (pixelWidth == 0 || pixelHeight == 0) return (0, 0);
            double width = Math.Min(MaxSide * 16.0, pixelWidth / divisor);
            double height = Math.Min(MaxSide * 16.0, pixelHeight / divisor);
            double factor = Math.Min(1.0, Math.Min(MaxSide / width, MaxSide / height));
            factor = Math.Min(factor, Math.Sqrt(MaxPixels / (width * height)));
            return (Math.Max(1, (int)Math.Floor(width * factor)),
                    Math.Max(1, (int)Math.Floor(height * factor)));
        }

        // Reuses caller's BGRA32 buffer. Same time, size and palette => same bytes.
        public static void Render(byte[] pixels, int width, int height, double seconds)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (width < 1 || height < 1 || (long)width * height > MaxPixels)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (pixels.Length < (long)width * height * 4)
                throw new ArgumentException("BGRA buffer is smaller than the frame", nameof(pixels));
            if (!double.IsFinite(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            double t = seconds * 2.0 * Speed;
            double cx = width / 2.0, cy = height / 2.0;
            int hueOffset = (int)(seconds * 0.6 * Palette.Length / 360.0) % Palette.Length;
            for (int y = 0; y < height; y++)
            {
                double waveY = Math.Sin(y * 0.45 - t * 1.2);
                for (int x = 0; x < width; x++)
                {
                    double value = (Math.Sin(x * 0.35 + t) + waveY
                        + Math.Sin((x + y) * 0.25 + t * 0.6)
                        + Math.Sin(Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) * 0.6 - t)) / 4.0;
                    int index = Math.Min(255, (int)(Math.Clamp((value + 1.0) / 2.0, 0, 1) * 256));
                    uint argb = Palette[(index + hueOffset) % 256];
                    int offset = (y * width + x) * 4;
                    pixels[offset] = (byte)argb;
                    pixels[offset + 1] = (byte)(argb >> 8);
                    pixels[offset + 2] = (byte)(argb >> 16);
                    pixels[offset + 3] = 255;
                }
            }
        }

        private static uint[] BuildPalette()
        {
            // PlasmaTerm's full-circle procedural 256-colour HSV spectrum.
            var colors = new uint[256];
            for (int i = 0; i < colors.Length; i++)
            {
                double hue = i * 6.0 / colors.Length;
                int sector = (int)hue;
                double f = hue - sector;
                double r = 0, g = 0, b = 0;
                switch (sector)
                {
                    case 0: r = 1; g = f; break;
                    case 1: r = f; g = 1; break;
                    case 2: g = 1; b = f; break;
                    case 3: g = f; b = 1; break;
                    case 4: r = f; b = 1; break;
                    default: r = 1; b = f; break;
                }
                colors[i] = 0xff000000u | ((uint)(byte)(r * 255) << 16)
                    | ((uint)(byte)(g * 255) << 8) | (byte)(b * 255);
            }
            return colors;
        }
    }
}
