using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroBar.Utilities;

internal static partial class Program
{
    private static void Pixels()
    {
        Color[] lut = { Colors.Red, Colors.Green, Colors.Blue };
        Check(LutTextures.Sample(lut, 0) == Colors.Red && LutTextures.Sample(lut, 0.5) == Colors.Green && LutTextures.Sample(lut, 1) == Colors.Blue, "exact endpoints and internal stop");
        Check(LutTextures.Sample(new[] { Color.FromArgb(0, 0, 0, 0), Color.FromArgb(200, 100, 50, 20) }, 0.5) == Color.FromArgb(100, 50, 25, 10), "straight RGB/alpha interpolation");
        Throws<ArgumentException>(() => LutTextures.Sample(new[] { Colors.Red }, 0));
        Throws<ArgumentOutOfRangeException>(() => LutTextures.Sample(lut, double.NaN));
        var hashes = new HashSet<string>();
        foreach (string name in LutTextures.BuiltInNames)
        {
            var spec = new TextureSpec { Name = name };
            var first = LutTextures.Create(spec);
            var image = (BitmapSource)first.ImageSource;
            byte[] pixels = Bytes(image);
            Check(image.PixelWidth == 64 && image.PixelHeight == 64 && image.IsFrozen && first.IsFrozen, name + " immutable bounded tile");
            Check(pixels.Where((b, i) => i % 4 == 3).All(a => a == 255), name + " opaque coverage");
            Check(pixels.Where((b, i) => i % 4 == 0).Distinct().Count() >= 2, name + " not a flat colour");
            hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels)));
            spec.Scale = 1.25;
            Check(ReferenceEquals(image, LutTextures.Create(spec, rotate: -90).ImageSource), name + " scale/rotation share source cache");
            spec.Contrast = 0;
            byte[] flat = Bytes((BitmapSource)LutTextures.Create(spec).ImageSource);
            Check(flat.Where((b, i) => i % 4 != 3).All(c => c == 128), name + " contrast zero maps to LUT midpoint");
            Check(Bytes(image).SequenceEqual(pixels), name + " cache source not mutated by new settings");
            spec.Contrast = 4;
            byte[] strong = Bytes((BitmapSource)LutTextures.Create(spec).ImageSource);
            Check(!strong.SequenceEqual(flat), name + " nonzero contrast restored");
        }
        Check(hashes.Count == LutTextures.BuiltInNames.Count, "all six procedural patterns are distinct");
        var live = new TextureSpec { Name = "scanlines", UsePalette = true };
        Color?[] palette = { Colors.Black, Colors.Red };
        var a = LutTextures.Create(live, palette).ImageSource;
        palette[1] = Colors.Blue;
        var b = LutTextures.Create(live, palette).ImageSource;
        Check(!ReferenceEquals(a, b) && !Bytes((BitmapSource)a).SequenceEqual(Bytes((BitmapSource)b)), "live palette invalidates coloured image");
        for (int i = 0; i < 40; i++)
            LutTextures.Create(new TextureSpec { Lut = new List<Color> { Colors.Black, Color.FromRgb((byte)i, 10, 30) } });
        var cache = (IDictionary)typeof(LutTextures).GetField("Images", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Check(cache.Count <= 16, "coloured image cache remains bounded");
    }

    private static BitmapSource Synthetic(byte grey, byte alpha = 255, bool colour = false)
    {
        byte[] pixels = { grey, grey, (byte)(colour ? grey + 1 : grey), alpha };
        return BitmapSource.Create(1, 1, 144, 120, PixelFormats.Bgra32, null, pixels, 4);
    }

    private static void CustomPng()
    {
        string folder = Path.Combine(temporary, "Textures"); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "custom.png");
        SavePng(Synthetic(128, 128), path);
        var spec = new TextureSpec { Name = "custom.png", Lut = new List<Color> { Color.FromArgb(128, 0, 0, 0), Color.FromArgb(128, 255, 255, 255) } };
        var brush = LutTextures.Create(spec, texturesFolder: folder);
        var image = (BitmapSource)brush.ImageSource;
        byte[] pixels = Bytes(image);
        Check(pixels[0] == 128 && pixels[1] == 128 && pixels[2] == 128 && pixels[3] == 64, "LUT alpha multiplied by PNG coverage");
        Check(image.DpiX == 96 && image.DpiY == 96 && brush.Viewport.Width == 1 && brush.Viewport.Height == 1, "ignore PNG DPI metadata");
        Check(ReferenceEquals(image, LutTextures.Create(spec, texturesFolder: folder).ImageSource), "custom content cache hit");
        // A same-size uncompressed RGBA PNG with a different grey value and the same timestamp
        // must not keep an old image. Build this fixture with Deflate's no-compression mode.
        byte[] oldPng = RawPng(40), newPng = RawPng(210);
        Check(oldPng.Length == newPng.Length, "same-length replacement fixture");
        File.WriteAllBytes(path, oldPng);
        var before = LutTextures.Create(spec, texturesFolder: folder).ImageSource;
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        File.WriteAllBytes(path, newPng); File.SetLastWriteTimeUtc(path, timestamp);
        var after = LutTextures.Create(spec, texturesFolder: folder).ImageSource;
        Check(!ReferenceEquals(before, after) && Bytes((BitmapSource)before)[0] == 40 && Bytes((BitmapSource)after)[0] == 210, "same-length/timestamp replacement invalidates");
        // A cached bitmap must not keep the source locked or conceal its deletion.
        File.Delete(path);
        Throws<FileNotFoundException>(() => LutTextures.Create(spec, texturesFolder: folder));
        SavePng(Synthetic(50, colour: true), path);
        Throws<FormatException>(() => LutTextures.Create(spec, texturesFolder: folder));
        SavePng(Synthetic(50, 0, colour: true), path);
        Check(Bytes((BitmapSource)LutTextures.Create(spec, texturesFolder: folder).ImageSource).All(x => x == 0), "transparent hidden colour is ignored");
        // Native greyscale and indexed-grey PNG formats also use the production decoder.
        var grey = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Gray8, null, new byte[] { 0, 255 }, 2);
        SavePng(grey, path);
        spec.Lut = new List<Color> { Colors.Red, Colors.Blue };
        pixels = Bytes((BitmapSource)LutTextures.Create(spec, texturesFolder: folder).ImageSource);
        Check(pixels[2] == 255 && pixels[4] == 255, "Gray8 endpoint mapping");
        var indexed = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Indexed8,
            new BitmapPalette(new List<Color> { Colors.Black, Colors.White }), new byte[] { 0, 1 }, 2);
        SavePng(indexed, path);
        Check(Bytes((BitmapSource)LutTextures.Create(spec, texturesFolder: folder).ImageSource).SequenceEqual(pixels), "indexed grey mapping");
        byte[] bad = RawPng(90); bad[16] = 127; File.WriteAllBytes(path, bad);
        Throws<FormatException>(() => LutTextures.Create(spec, texturesFolder: folder));
        File.WriteAllBytes(path, new byte[40]);
        Throws<FormatException>(() => LutTextures.Create(spec, texturesFolder: folder));
        using (var oversized = File.Create(path)) oversized.SetLength(LutTextures.MaxFileBytes + 1);
        Throws<FormatException>(() => LutTextures.Create(spec, texturesFolder: folder));
        SavePng(grey, path);
        Check(LutTextures.CustomNames(folder).SequenceEqual(new[] { "custom.png" }), "custom filename discovery");
        var missingPalette = new TextureSpec { Name = "custom.png", UsePalette = true };
        Throws<FormatException>(() => LutTextures.Create(missingPalette, new Color?[] { Colors.Red }, texturesFolder: folder));
    }

    // Minimal real PNG fixture; CRC covers type+data, Adler-32 is written by ZLibStream.
    private static byte[] RawPng(byte grey)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        void Big(uint number) => output.Write(new[] { (byte)(number >> 24), (byte)(number >> 16), (byte)(number >> 8), (byte)number });
        void Chunk(string type, byte[] data)
        {
            byte[] combined = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
            uint crc = 0xffffffff;
            foreach (byte v in combined)
            {
                crc ^= v;
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
            }
            Big((uint)data.Length); output.Write(combined); Big(~crc);
        }
        Chunk("IHDR", new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0 });
        using var compressed = new MemoryStream();
        using (var z = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.NoCompression, true))
            z.Write(new byte[] { 0, grey, grey, grey, 255 });
        Chunk("IDAT", compressed.ToArray()); Chunk("IEND", Array.Empty<byte>());
        return output.ToArray();
    }
}
