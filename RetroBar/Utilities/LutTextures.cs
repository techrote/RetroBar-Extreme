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
        private const int TileSize = 64, CacheLimit = 16;
        public static IReadOnlyList<string> BuiltInNames { get; } = Array.AsReadOnly(new[]
            { "brushed", "carbon", "noise", "scanlines", "checker", "diamond", "clouds", "cells", "marble" }); // UX-013: last three are coherent noise
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
            Color[] lut = spec.ResolveLut(palette);
            if (!double.IsFinite(rotate)) throw new ArgumentOutOfRangeException(nameof(rotate));
            int width = TileSize, height = TileSize;
            byte[] pixels;
            string sourceKey;
            if (BuiltInNames.Contains(spec.Name))
            {
                sourceKey = spec.Name;
                lock (CacheLock)
                {
                    if (!Patterns.TryGetValue(spec.Name, out pixels))
                        Patterns[spec.Name] = pixels = Pattern(spec.Name);
                }
            }
            else
            {
                byte[] png = ReadPng(spec.Name, texturesFolder ?? TexturesFolder, out width, out height);
                sourceKey = Convert.ToHexString(SHA256.HashData(png));
                // Do not key custom assets by timestamp/length: in-place replacements can
                // preserve both. Read a bounded snapshot on every rebuild, never every frame.
                pixels = null;
                string candidateKey = ImageKey(sourceKey, lut, spec.Contrast);
                lock (CacheLock)
                {
                    if (Images.TryGetValue(candidateKey, out BitmapSource cached))
                        return Brush(cached, spec.Scale, rotate);
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

            string key = ImageKey(sourceKey, lut, spec.Contrast);
            lock (CacheLock)
            {
                if (Images.TryGetValue(key, out BitmapSource cached)) return Brush(cached, spec.Scale, rotate);
            }
            byte[] result = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                double level = Math.Clamp((pixels[i] / 255.0 - 0.5) * spec.Contrast + 0.5, 0, 1);
                Color colour = Sample(lut, level);
                result[i] = colour.B; result[i + 1] = colour.G; result[i + 2] = colour.R;
                result[i + 3] = (byte)((colour.A * pixels[i + 3] + 127) / 255);
                if (result[i + 3] == 0) result[i] = result[i + 1] = result[i + 2] = 0;
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
            return Brush(image, spec.Scale, rotate);
        }

        private static string ImageKey(string source, Color[] lut, double contrast) => source + ":" +
            contrast.ToString("R", CultureInfo.InvariantCulture) + ":" + string.Join(",", lut.Select(BrushSpec.FormatColor));

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

        private static byte[] Pattern(string name)
        {
            var pixels = new byte[TileSize * TileSize * 4];
            double wave(double n, double period) => Math.Cos(2 * Math.PI * n / period);
            double noise(int x, int y)
            {
                uint n = unchecked((uint)((x & 63) + (y & 63) * 64) + 0x9e3779b9u);
                n = unchecked((n ^ (n >> 16)) * 0x7feb352du);
                n = unchecked((n ^ (n >> 15)) * 0x846ca68bu);
                return ((n ^ (n >> 16)) & 65535) / 65535.0 - 0.5;
            }
            for (int y = 0; y < TileSize; y++)
                for (int x = 0; x < TileSize; x++)
                {
                    double level;
                    switch (name)
                    {
                        case "brushed":
                            level = 0.50 + 0.16 * wave(y, 64) + 0.23 * noise(0, y) +
                                0.04 * wave(x + y, 64) + 0.035 * noise(x, y);
                            break;
                        case "carbon":
                            bool across = ((x / 8 + y / 8) & 1) == 0;
                            level = 0.25 + 0.34 * (1 + wave(across ? y : x, 8)) / 2 + 0.07 * wave(across ? x : y, 4);
                            break;
                        case "noise": level = 0.5 + 0.8 * noise(x, y); break;
                        case "scanlines": level = y % 4 < 2 ? 0.2 : 0.8; break;
                        case "checker": level = ((x / 8 + y / 8) & 1) == 0 ? 0.25 : 0.75; break;
                        case "diamond":
                            double u = (x % 16) - 7.5, v = (y % 16) - 7.5;
                            if (((x / 16 + y / 16) & 1) != 0) u = -u;
                            double along = (u + v) / Math.Sqrt(2), cross = (u - v) / Math.Sqrt(2);
                            level = 0.3 + 0.6 * Math.Max(0, 1 - along * along / 36 - cross * cross / 3);
                            break;
                        // UXUI-Extreme (UX-013): coherent noise, periodic on the tile so it repeats seamlessly.
                        case "clouds": level = 0.5 + 1.3 * CoherentNoise.Fractal(x, y, TileSize, 4, 4); break;
                        case "cells": level = CoherentNoise.Cells(x, y, TileSize, 6); break;
                        case "marble":
                            double turbulence = CoherentNoise.Turbulence(x, y, TileSize, 4, 4);
                            level = 0.5 + 0.5 * Math.Sin(2 * Math.PI * (2.0 * x / TileSize + 1.0 * y / TileSize + 1.6 * turbulence));
                            break;
                        default: throw new ArgumentException("unknown procedural texture", nameof(name));
                    }
                    int i = (y * TileSize + x) * 4;
                    pixels[i] = pixels[i + 1] = pixels[i + 2] = (byte)Math.Round(Math.Clamp(level, 0, 1) * 255);
                    pixels[i + 3] = 255;
                }
            return pixels;
        }
    }
}
