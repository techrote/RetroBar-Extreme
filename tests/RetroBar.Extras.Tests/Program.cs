using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroBar.Utilities;

// UXUI-Extreme (UX-017): built-in fonts, built-in example styles and the Extras folder.
// Real WPF/STA checks against RetroBar's own code and resources. Reads only: never writes to
// RetroBar's settings, Style.json or Styles folder.
internal static class Program
{
    private static int assertions;
    private static string evidence;
    private static readonly List<object> results = new();

    // Characters a taskbar shows: titles, clock, trimming and common marks.
    private static readonly string Needed =
        new string(Enumerable.Range(0x20, 0x5F).Select(c => (char)c).ToArray()) + "…–—‘’“”•·°×®©™€£ÄÖÜäöüßéèàçñ";

    [STAThread]
    private static int Main(string[] args)
    {
        evidence = Path.GetFullPath(args.Length > 0 ? args[0] : "extras-evidence");
        Directory.CreateDirectory(evidence);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int failures = 0;
        foreach (var test in new (string Name, Action Body)[]
        {
            ("Every built-in font resolves from the executable, with real Regular and Bold faces", Fonts),
            ("Built-in fonts cover the characters the taskbar shows", Coverage),
            ("Knurled Swarf: face and size selection, and pixel-exact rendering in WPF", PixelFont),
            ("Example styles are built in, parse cleanly and are listed after saved styles", ExampleStyles),
            ("The Extras folder ships beside the build: styles, icon pack, fonts and licence", ExtrasFolder),
        })
        {
            try { test.Body(); results.Add(new { test.Name, Passed = true }); Console.WriteLine("PASS " + test.Name); }
            catch (Exception e) { failures++; results.Add(new { test.Name, Passed = false, Error = e.ToString() }); Console.Error.WriteLine("FAIL " + test.Name + "\n" + e); }
        }

        File.WriteAllText(Path.Combine(evidence, "results.json"), JsonSerializer.Serialize(new { assertions, failures, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{assertions} assertions, {failures} failed groups");
        app.Shutdown();
        return failures == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static GlyphTypeface Face(FontFamily family, FontWeight weight, FontStyle style, out Typeface typeface)
    {
        typeface = new Typeface(family, style, weight, FontStretches.Normal);
        Check(typeface.TryGetGlyphTypeface(out GlyphTypeface glyphs), $"{family.Source} {weight}: no face");
        return glyphs;
    }

    private static void Fonts()
    {
        Check(BundledFonts.Names.Count == 24, "24 built-in families");
        foreach (string name in BundledFonts.Names)
        {
            FontFamily family = BundledFonts.Resolve(name, 12);
            foreach (FontWeight weight in new[] { FontWeights.Normal, FontWeights.Bold })
            {
                GlyphTypeface glyphs = Face(family, weight, FontStyles.Normal, out Typeface typeface);
                string file = glyphs.FontUri.ToString();
                Check(file.IndexOf("/fonts/", StringComparison.OrdinalIgnoreCase) >= 0, $"{name} {weight} came from {file}, not the built-in fonts");
                Check(!typeface.IsBoldSimulated, $"{name} {weight}: bold is simulated");
            }
        }

        // The vault's own names for the renamed faces still work; other names are left to Windows.
        Check(BundledFonts.Canonical("Ferrule Regular") == "Ferrule" && BundledFonts.Canonical("pebble bold") == "Pebble", "aliases");
        Check(!BundledFonts.IsBundled("Segoe UI") && BundledFonts.Resolve("Segoe UI").Source == "Segoe UI", "installed fonts pass through");
        Check(BundledFonts.Resolve("") == null && BundledFonts.Resolve(null) == null, "empty name");
        Check(ReferenceEquals(BundledFonts.Resolve("Ferrule"), BundledFonts.Resolve("ferrule")), "families are cached and case-insensitive");
    }

    private static void Coverage()
    {
        foreach (string name in BundledFonts.Names)
        {
            GlyphTypeface glyphs = Face(BundledFonts.Resolve(name, 12), FontWeights.Normal, FontStyles.Normal, out _);
            string missing = new string(Needed.Where(c => !glyphs.CharacterToGlyphMap.ContainsKey(c)).ToArray());
            Check(missing.Length == 0, $"{name} lacks \"{missing}\"");
        }
    }

    private static void PixelFont()
    {
        Check(BundledFonts.IsPixelFont("Knurled Swarf") && !BundledFonts.IsPixelFont("Ferrule"), "pixel font flag");
        foreach (var (px, face, size) in new[] { (5.0, 6, 6), (11.0, 11, 11), (13.75, 14, 14), (16.0, 16, 16), (22.0, 11, 22), (27.5, 14, 28), (40.0, 13, 39) })
        {
            Check(BundledFonts.PixelFace(px) == face, $"face for {px}px: {BundledFonts.PixelFace(px)}, expected {face}");
            Check(BundledFonts.PixelSize(px) == size, $"size for {px}px: {BundledFonts.PixelSize(px)}, expected {size}");
        }
        for (int n = 6; n <= 16; n++)
        {
            GlyphTypeface glyphs = Face(BundledFonts.Resolve("Knurled Swarf", n), FontWeights.Normal, FontStyles.Normal, out _);
            Check(glyphs.FontUri.ToString().IndexOf($"knurledswarf{n}px", StringComparison.OrdinalIgnoreCase) >= 0, $"{n}px face: {glyphs.FontUri}");
        }

        // Drawn at its own size on whole pixels, every pixel is pure ink or pure paper.
        foreach (int n in new[] { 11, 12, 16 })
        {
            var text = new TextBlock
            {
                Text = "Task Manager 01:23 HWiNFO® ©™ Iñtë",
                FontFamily = BundledFonts.Resolve("Knurled Swarf", n),
                FontSize = n,
                Foreground = Brushes.Black,
                Background = Brushes.White,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
            };
            TextOptions.SetTextFormattingMode(text, TextFormattingMode.Display);
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            text.Arrange(new Rect(new Point(0, 0), text.DesiredSize));
            int w = (int)Math.Ceiling(text.ActualWidth), h = (int)Math.Ceiling(text.ActualHeight);
            var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(text);
            var pixels = new byte[w * h * 4];
            bmp.CopyPixels(pixels, w * 4, 0);
            int ink = 0, grey = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte v = pixels[i];
                if (v == 0) ink++;
                else if (v != 255) grey++;
            }
            Save(bmp, $"knurled-{n}px.png");
            Check(ink > 50, $"{n}px: nothing drawn");
            Check(grey == 0, $"{n}px: {grey} grey pixels, so the pixel font is blurred");
        }
    }

    private static void ExampleStyles()
    {
        List<string> names = StyleDocument.ExampleNames();
        Check(names.Count == 27, $"27 example styles, found {names.Count}");
        Check(!names.Contains("Broken"), "the deliberately broken test style isn't an example");
        foreach (string name in names)
        {
            var warnings = new List<string>();
            StyleDocument style = StyleDocument.Parse(StyleDocument.ExampleJson(name), warnings.Add);
            Check(warnings.Count == 0, $"{name}: {string.Join("; ", warnings)}");
            Check(style.Name == name, $"{name}: its name inside is \"{style.Name}\"");
            foreach (var part in style.Parts) foreach (var prop in part.Value) prop.Value.ToBrush(0, style.Palette);
            Check(style.Palette.Count(c => c.HasValue) >= 8, $"{name}: at least 8 colours in its palette");
            Check(style.TexturePalette.Count(t => t != null) == 10, $"{name}: ten textures in its texture palette");
            foreach (TextureSpec texture in style.TexturePalette.Where(t => t != null)) LutTextures.Create(texture, style.Palette);
        }
        Check(StyleDocument.ExampleJson("neon night") != null, "names are case-insensitive");
        Check(StyleDocument.ExampleJson("No Such Style") == null && !StyleDocument.IsExample("No Such Style"), "unknown names");

        List<string> saved = StyleDocument.SavedNames();
        List<string> listed = StyleDocument.ListedNames();
        Check(listed.Take(saved.Count).SequenceEqual(saved), "saved styles come first");
        Check(listed.Count == listed.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "a saved style replaces the example of the same name");
        Check(names.All(n => listed.Contains(n, StringComparer.OrdinalIgnoreCase)), "every example is listed");
    }

    private static void ExtrasFolder()
    {
        string extras = Path.Combine(AppContext.BaseDirectory, "Extras");
        string[] styles = Directory.GetFiles(Path.Combine(extras, "Styles"), "*.json");
        Check(styles.Length == 27 && !styles.Any(f => f.EndsWith("Broken.json")), $"Extras\\Styles: {styles.Length} styles");
        Check(File.Exists(Path.Combine(extras, "Styles", "README.md")) && File.Exists(Path.Combine(extras, "Styles", "preview.png")), "styles README and preview");
        int icons = Directory.GetFiles(Path.Combine(extras, "Icons"), "*.png", SearchOption.AllDirectories).Count(f => !f.EndsWith("preview.png"));
        Check(icons == 234, $"Extras\\Icons: {icons} icons (74 originals, 100 button symbols, 24 Show desktop, 36 notification area)");
        foreach (string kind in new[] { "showdesktop", "tray", "start", "quicklaunch" })
            Check(Directory.Exists(Path.Combine(extras, "Icons", kind)), $"Extras\\Icons\\{kind} exists (the Choose PNG buttons open it)");
        Check(!Directory.Exists(Path.Combine(extras, "Icons", "startbuttons")), "Start buttons are not mixed in with the icons");
        foreach (var (folder, height) in new[] { ("3state", 84), ("1state", 28) })
        {
            string[] buttons = Directory.GetFiles(Path.Combine(extras, "StartButtons", folder), "*.png");
            Check(buttons.Length == 25, $"Extras StartButtons/{folder}: {buttons.Length} Start buttons");
            foreach (string button in buttons)
            {
                var frame = new System.Windows.Media.Imaging.BitmapImage(new Uri(button));
                Check(frame.PixelWidth == 96 && frame.PixelHeight == height, folder + "\\" + Path.GetFileName(button) + $" is 96 x {height}");
            }
        }
        Check(File.Exists(Path.Combine(extras, "StartButtons", "README.md")) && File.Exists(Path.Combine(extras, "StartButtons", "template-3state.png")), "Start button guide and template");
        Check(Directory.Exists(Path.Combine(extras, "Icons", "start", "16px")) && Directory.Exists(Path.Combine(extras, "Icons", "toggle", "100%")), "icon pack folders");
        int fonts = Directory.GetFiles(Path.Combine(extras, "Fonts"), "*.ttf").Length;
        Check(fonts == 63, $"Extras\\Fonts: {fonts} font files");
        Check(File.ReadAllText(Path.Combine(extras, "Fonts", "OFL.txt")).Contains("SIL Open Font License"), "the font licence ships with the fonts");
    }

    private static void Save(BitmapSource bitmap, string file)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(Path.Combine(evidence, file));
        encoder.Save(stream);
    }
}
