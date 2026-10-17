using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-011): original procedural patterns, not downloaded image assets.
    // All patterns are periodic on a 64x64 lattice. Texture size is in DIPs, independent of
    // the painted rectangle and native taskbar size. The legacy LayoutTransform still works.
    public static class LutTextures
    {
        public const int MaxDimension = 512, MaxFileBytes = 4 * 1024 * 1024;
        private const int TileSize = 64, CacheLimit = 16, PatternLimit = 48;
        public static IReadOnlyList<string> BuiltInNames { get; } = Array.AsReadOnly(new[]
            { "brushed", "carbon", "noise", "scanlines", "checker", "diamond", "clouds", "cells", "marble", // UX-013: last three are coherent noise
              "weave", "bricks", "dots", "waves" });
        public static string TexturesFolder => "Textures".InLocalAppData();

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, BitmapSource> Images = new Dictionary<string, BitmapSource>();
        private static readonly Queue<string> ImageOrder = new Queue<string>();
        private static readonly Dictionary<string, byte[]> Patterns = new Dictionary<string, byte[]>();

        public static string[] CustomNames(string folder = null)
        {
            folder ??= TexturesFolder;
            if (!Directory.Exists(folder)) return Array.Empty<string>();
            RejectLinks(folder);
            return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).Where(TextureSpec.IsValidName)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        public static ImageBrush Create(TextureSpec spec, IEnumerable<Color?> palette = null,
            double rotate = 0, string texturesFolder = null)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            double ScaleFor(int w, int h) => ResamplesHere(spec, w, h) ? 1 : spec.Scale;
            Color[] lut = spec.ResolveLut(palette);
            if (!double.IsFinite(rotate)) throw new ArgumentOutOfRangeException(nameof(rotate));
            int width = TileSize, height = TileSize;
            byte[] pixels;
            string sourceKey;
            if (BuiltInNames.Contains(spec.Name))
            {
                sourceKey = TextureGenerators.Key(spec);
                lock (CacheLock)
                {
                    if (!Patterns.TryGetValue(sourceKey, out pixels))
                    {
                        if (Patterns.Count >= PatternLimit) Patterns.Clear(); // sliders make many one-off tiles
                        Patterns[sourceKey] = pixels = TextureGenerators.Pattern(spec);
                    }
                }
            }
            else
            {
                byte[] png = ReadPng(spec.Name, texturesFolder ?? TexturesFolder, out width, out height);
                sourceKey = Convert.ToHexString(SHA256.HashData(png));
                // Do not key custom assets by timestamp/length: in-place replacements can
                // preserve both. Read a bounded snapshot on every rebuild, never every frame.
                pixels = null;
                string candidateKey = ImageKey(sourceKey, lut, spec);
                lock (CacheLock)
                {
                    if (Images.TryGetValue(candidateKey, out BitmapSource cached))
                        return Brush(cached, ScaleFor(width, height), rotate);
                }
                using var stream = new MemoryStream(png, false);
                var decoder = new PngBitmapDecoder(stream,
                    BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count != 1 || decoder.Frames[0].PixelWidth != width || decoder.Frames[0].PixelHeight != height)
                    throw new FormatException("texture must be a single PNG tile with matching dimensions");
                var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
                pixels = new byte[width * height * 4];
                converted.CopyPixels(pixels, width * 4, 0);
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i + 3] != 0 && (pixels[i] != pixels[i + 1] || pixels[i] != pixels[i + 2]))
                        throw new FormatException("custom textures must be greyscale (equal red, green and blue channels)");
                }
            }

            string key = ImageKey(sourceKey, lut, spec);
            lock (CacheLock)
            {
                if (Images.TryGetValue(key, out BitmapSource cached)) return Brush(cached, ScaleFor(width, height), rotate);
            }

            // Grey level and coverage as floats, so scaling and sharpening do not round in between.
            int count = width * height;
            var grey = new float[count];
            var coverage = new float[count];
            for (int i = 0; i < count; i++)
            {
                grey[i] = pixels[i * 4];
                coverage[i] = pixels[i * 4 + 3];
            }

            bool resample = ResamplesHere(spec, width, height);
            double brushScale = resample ? 1 : spec.Scale;
            if (resample)
            {
                int newWidth = (int)Math.Round(width * spec.Scale), newHeight = (int)Math.Round(height * spec.Scale);
                grey = TextureResampler.Scale(grey, width, height, newWidth, newHeight, spec.Resample);
                coverage = TextureResampler.Scale(coverage, width, height, newWidth, newHeight, spec.Resample);
                width = newWidth; height = newHeight;
            }
            if (spec.Sharpen > 0)
            {
                grey = TextureResampler.Sharpen(grey, width, height, spec.Sharpen, spec.SharpenRadius);
            }

            byte[] result = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                double level = Math.Clamp(((grey[i] / 255.0) - 0.5) * spec.Contrast + 0.5, 0, 1);
                if (spec.WhiteLevel < 1) level = Math.Min(1, level / spec.WhiteLevel);
                Color colour = Sample(lut, level);
                double r = colour.R, g = colour.G, b = colour.B;
                if (spec.Saturation != 1)
                {
                    double luma = 0.299 * r + 0.587 * g + 0.114 * b;
                    r = luma + (r - luma) * spec.Saturation;
                    g = luma + (g - luma) * spec.Saturation;
                    b = luma + (b - luma) * spec.Saturation;
                }
                if (spec.Brightness != 0)
                {
                    double shift = spec.Brightness * 255;
                    r += shift; g += shift; b += shift;
                }
                int o = i * 4;
                result[o] = (byte)Math.Round(Math.Clamp(b, 0, 255));
                result[o + 1] = (byte)Math.Round(Math.Clamp(g, 0, 255));
                result[o + 2] = (byte)Math.Round(Math.Clamp(r, 0, 255));
                byte coverageByte = (byte)Math.Round(Math.Clamp(coverage[i], 0, 255));
                result[o + 3] = (byte)((colour.A * coverageByte + 127) / 255);
                if (result[o + 3] == 0) result[o] = result[o + 1] = result[o + 2] = 0;
            }
            // Discard input PNG DPI: one source pixel is one DIP at texture scale 1.
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, result, width * 4);
            image.Freeze();
            lock (CacheLock)
            {
                if (!Images.ContainsKey(key))
                {
                    while (Images.Count >= CacheLimit) Images.Remove(ImageOrder.Dequeue());
                    Images[key] = image;
                    ImageOrder.Enqueue(key);
                }
                else image = Images[key];
            }
            return Brush(image, brushScale, rotate);
        }

        // Scaling is done here (and the brush shows the tile 1:1) unless the method is "smooth",
        // the scale is 1, or the scaled tile would be unreasonably large; then WPF scales it.
        private static bool ResamplesHere(TextureSpec spec, int width, int height) =>
            spec.Resample != TextureResampler.Smooth && spec.Scale != 1 && TextureResampler.CanResample(width, height, spec.Scale);

        private static string ImageKey(string source, Color[] lut, TextureSpec spec)
        {
            var c = CultureInfo.InvariantCulture;
            string tone = spec.Brightness == 0 && spec.Saturation == 1 && spec.WhiteLevel == 1 && spec.Sharpen == 0 && spec.Resample == TextureResampler.Smooth ? "" :
                ":" + spec.Brightness.ToString("R", c) + "," + spec.Saturation.ToString("R", c) + "," + spec.WhiteLevel.ToString("R", c) + "," +
                spec.Sharpen.ToString("R", c) + "," + spec.SharpenRadius.ToString("R", c) + "," + spec.Resample + "," + spec.Scale.ToString("R", c);
            return source + ":" + spec.Contrast.ToString("R", c) + ":" + string.Join(",", lut.Select(BrushSpec.FormatColor)) + tone;
        }

        private static ImageBrush Brush(BitmapSource image, double scale, double rotate)
        {
            var brush = new ImageBrush(image)
            {
                TileMode = TileMode.Tile,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, image.PixelWidth, image.PixelHeight),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, image.PixelWidth * scale, image.PixelHeight * scale),
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
            };
            if (rotate != 0) brush.Transform = new RotateTransform(rotate);
            brush.Freeze();
            return brush;
        }

        // Interpolate straight sRGB and alpha independently. Source coverage is multiplied
        // afterwards; do not interpolate premultiplied bytes as though they were grey levels.
        public static Color Sample(IReadOnlyList<Color> lut, double level)
        {
            if (lut == null || lut.Count < 2 || lut.Count > StyleDocument.PALETTE_SIZE)
                throw new ArgumentException("LUT must contain 2 to 12 colours", nameof(lut));
            if (!double.IsFinite(level)) throw new ArgumentOutOfRangeException(nameof(level));
            double position = Math.Clamp(level, 0, 1) * (lut.Count - 1);
            int left = Math.Min((int)position, lut.Count - 2);
            double t = position - left;
            Color a = lut[left], b = lut[left + 1];
            byte mix(byte x, byte y) => (byte)Math.Round(x + (y - x) * t, MidpointRounding.AwayFromZero);
            return Color.FromArgb(mix(a.A, b.A), mix(a.R, b.R), mix(a.G, b.G), mix(a.B, b.B));
        }

        private static byte[] ReadPng(string name, string folder, out int width, out int height)
        {
            if (!TextureSpec.IsValidName(name) || BuiltInNames.Contains(name))
                throw new FormatException("not a custom PNG filename");
            string path = Path.Combine(Path.GetFullPath(folder), name);
            RejectLinks(path);
            byte[] bytes;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (file.Length < 33 || file.Length > MaxFileBytes)
                    throw new FormatException("PNG texture must be at most 4 MiB and contain a valid header");
                bytes = new byte[(int)file.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int n = file.Read(bytes, read, bytes.Length - read);
                    if (n == 0) throw new IOException("texture changed while reading; save it again");
                    read += n;
                }
                if (file.ReadByte() != -1) throw new IOException("texture changed while reading; save it again");
            }
            byte[] header = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 };
            if (!bytes.Take(header.Length).SequenceEqual(header)) throw new FormatException("not a PNG texture");
            uint number(int offset) => ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) |
                ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
            uint w = number(16), h = number(20);
            if (w < 1 || h < 1 || w > MaxDimension || h > MaxDimension)
                throw new FormatException("PNG texture dimensions must be from 1 to 512 pixels on each side");
            width = (int)w; height = (int)h;
            return bytes;
        }

        // No filesystem redirects outside the configured texture directory, including an
        // ancestor junction. User assets are names, never arbitrary paths or remote URIs.
        private static void RejectLinks(string path)
        {
            for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("linked files and folders are not supported for textures");
        }
    }
}
