using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroBar.Utilities;

internal static partial class Program
{
    private static BitmapSource Render(Brush brush, double width, double height, double dpi = 1, double stretch = 1)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(stretch, stretch));
            dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            dc.Pop();
        }
        var target = new RenderTargetBitmap((int)Math.Ceiling(width * dpi * stretch), (int)Math.Ceiling(height * dpi * stretch),
            dpi * 96, dpi * 96, PixelFormats.Pbgra32);
        target.Render(visual); target.Freeze();
        return target;
    }

    private static void Periodic(BitmapSource image, int period, string label)
    {
        byte[] bytes = Bytes(image);
        int width = image.PixelWidth, height = image.PixelHeight;
        // Exclude only the outer drawing boundary. Internal tile boundaries are included.
        for (int y = 1; y + period < height - 1; y++)
            for (int x = 1; x + period < width - 1; x++)
            {
                int i = (y * width + x) * 4;
                Check(bytes[i + 3] == 255, label + " transparent seam at " + x + "," + y);
                for (int channel = 0; channel < 4; channel++)
                {
                    Check(Math.Abs(bytes[i + channel] - bytes[i + period * 4 + channel]) <= 1, label + " horizontal repeat at " + x + "," + y);
                    Check(Math.Abs(bytes[i + channel] - bytes[i + period * width * 4 + channel]) <= 1, label + " vertical repeat at " + x + "," + y);
                }
            }
    }

    private static void Tiling()
    {
        foreach (string name in LutTextures.BuiltInNames)
            foreach (double scale in new[] { 0.25, 1.0, 1.5 })
                foreach (double dpi in new[] { 1.0, 1.25, 1.5, 2.0 })
                    foreach (double rotate in new[] { 0.0, -90.0 })
                    {
                        var spec = new TextureSpec { Name = name, Scale = scale };
                        var brush = LutTextures.Create(spec, rotate: rotate);
                        Check(brush.ViewportUnits == BrushMappingMode.Absolute && brush.Viewport.Width == 64 * scale, "absolute native texture density");
                        double side = 64 * scale * 2 + 8;
                        var image = Render(brush, side, side, dpi);
                        string label = $"{name}, texture={scale}, DPI={dpi}, rotation={rotate}";
                        Periodic(image, (int)Math.Round(64 * scale * dpi), label);
                    }

        // Native taskbar resizing enlarges the filled area, not the texture. Rendering a
        // bigger native rectangle must leave its overlapping pixels unchanged.
        var native = LutTextures.Create(new TextureSpec { Name = "brushed" });
        var small = Render(native, 128, 48);
        var big = Render(native, 256, 96);
        byte[] a = Bytes(small), b = Bytes(big);
        for (int y = 1; y < 47; y++)
            for (int x = 1; x < 127; x++)
                for (int c = 0; c < 4; c++)
                    Check(a[(y * 128 + x) * 4 + c] == b[(y * 256 + x) * 4 + c], "native area resize does not stretch texture");
        foreach (double stretch in new[] { 1.25, 1.5, 2.0 })
            Periodic(Render(native, 140, 140, 1, stretch), (int)(64 * stretch), "legacy Stretch " + stretch);

        // Evidence uses the requested Ratrod LUT and a labelled synthetic vivid fixture.
        // It does not pretend to contain the owner's uncommitted CyberGarish theme.
        var ratrod = new TextureSpec { Name = "brushed", Lut = new[] { "#2b2f33", "#5a6066", "#8a9096" }.Select(BrushSpec.ParseColor).ToList() };
        var vivid = new TextureSpec { Name = "scanlines", Lut = new List<Color> { Colors.Indigo, Colors.Magenta, Colors.Cyan } };
        foreach (var preset in new[] { ("ratrod-brushed", ratrod), ("synthetic-vivid-scanlines", vivid) })
            foreach (double dpi in new[] { 1.0, 1.25, 1.5, 2.0 })
            {
                string suffix = ((int)(dpi * 100)).ToString(CultureInfo.InvariantCulture);
                SavePng(Render(LutTextures.Create(preset.Item2), 512, 64, dpi), Path.Combine(evidence, preset.Item1 + "-" + suffix + ".png"));
                SavePng(Render(LutTextures.Create(preset.Item2, rotate: -90), 64, 256, dpi), Path.Combine(evidence, preset.Item1 + "-vertical-" + suffix + ".png"));
            }
    }
}
