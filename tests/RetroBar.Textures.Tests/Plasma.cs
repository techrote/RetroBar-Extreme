using System;
using System.Linq;
using RetroBar.Utilities;

internal static partial class Program
{
    // Golden pixels calculated independently from PlasmaTerm/plasma.py.
    private static void Plasma()
    {
        Check(PlasmaRenderer.RecommendedSize(0, 100) == (0, 0), "empty surface");
        Check(PlasmaRenderer.RecommendedSize(1280, 1024) == (320, 256), "quarter-size taskbar");
        var large = PlasmaRenderer.RecommendedSize(3840, 2160);
        Check(large.Width * large.Height <= PlasmaRenderer.MaxPixels, "frame pixel budget");
        Check(large.Width <= 1024 && large.Height <= 1024, "frame side cap");
        Check(Math.Abs((double)large.Width / large.Height - 3840.0 / 2160.0) < 0.02, "frame aspect");
        Throws<ArgumentOutOfRangeException>(() => PlasmaRenderer.RecommendedSize(double.PositiveInfinity, 100));
        Throws<ArgumentOutOfRangeException>(() => PlasmaRenderer.RecommendedSize(1280, 1024, 0));

        var first = new byte[4 * 3 * 4];
        var same = new byte[first.Length];
        PlasmaRenderer.Render(first, 4, 3, 0);
        PlasmaRenderer.Render(same, 4, 3, 0);
        Check(first.SequenceEqual(same), "deterministic frames");
        void CheckPixel(byte[] pixels, int x, int y, byte r, byte g, byte b)
        {
            int offset = (y * 4 + x) * 4;
            Check(pixels[offset] == b && pixels[offset + 1] == g
                && pixels[offset + 2] == r && pixels[offset + 3] == 255,
                "PlasmaTerm reference pixel " + x + "," + y);
        }
        CheckPixel(first, 0, 0, 0, 185, 255);
        CheckPixel(first, 1, 0, 25, 0, 255);
        CheckPixel(first, 3, 1, 255, 0, 15);
        CheckPixel(first, 2, 2, 235, 0, 255);
        // Same logical field at twice the raster resolution: every matching
        // sample must retain its colour and position.
        var fine = new byte[8 * 6 * 4];
        PlasmaRenderer.Render(fine, 8, 6, 0, 0.5, 0.5);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 4; x++)
                for (int c = 0; c < 4; c++)
                    Check(first[(y * 4 + x) * 4 + c] == fine[((y * 2) * 8 + x * 2) * 4 + c],
                        "resolution-only changes preserve plasma position");
        Throws<ArgumentOutOfRangeException>(() => PlasmaRenderer.Render(first, 4, 3, 0, double.NaN, 1));
        var moved = new byte[first.Length];
        PlasmaRenderer.Render(moved, 4, 3, 1.25);
        Check(!first.SequenceEqual(moved), "time advances field");
        CheckPixel(moved, 0, 0, 0, 149, 255);
        CheckPixel(moved, 2, 1, 0, 255, 231);

        Throws<ArgumentNullException>(() => PlasmaRenderer.Render(null, 4, 3, 0));
        Throws<ArgumentException>(() => PlasmaRenderer.Render(new byte[3], 4, 3, 0));
        Throws<ArgumentOutOfRangeException>(() => PlasmaRenderer.Render(first, 4, 3, double.NaN));
        Throws<ArgumentOutOfRangeException>(() => PlasmaRenderer.Render(first, 1024, 1024, 0));
    }
}
