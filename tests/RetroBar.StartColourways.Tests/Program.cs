using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RetroBar.Controls;
using RetroBar.Utilities;
using Kind = RetroBar.Utilities.StartButtonColourways.SpriteKind;

// UXUI-Extreme (UX-010): real WPF/STA tests, not a second implementation of the algorithm.
// Never launches RetroBar's shell integration or writes to its active Style.json.
internal static class Program
{
    private static int assertions;
    private static string evidence;
    private static readonly List<object> results = new();
    private static readonly Color Purple = Color.FromRgb(140, 36, 255);
    private static readonly (string Key, string File)[] Orbs =
    {
        ("StartButtonImageLarge", "startvistabuttonlg.png"),
        ("StartButtonImageLargeScaled", "startvistabuttonlgscaled.png"),
        ("StartButtonImageBottom", "startvistabuttonbot.png"),
        ("StartButtonImageBottomScaled", "startvistabuttonbotscaled.png"),
        ("StartButtonImageTop", "startvistabuttontop.png"),
        ("StartButtonImageTopScaled", "startvistabuttontopscaled.png"),
    };

    [STAThread]
    private static int Main(string[] args)
    {
        evidence = Path.GetFullPath(args.Length > 0 ? args[0] : "start-colourways-evidence");
        Directory.CreateDirectory(evidence);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int failures = 0;
        foreach (var test in new (string Name, Action Body)[]
        {
            ("Style persistence, compatibility, clone, reset and generator isolation", Styles),
            ("Straight/premultiplied pixels, coverage, shading and bounded cache", PixelsAndCache),
            ("Real XP, Longhorn and all six Vista sprites in all three states", Fixtures),
            ("Original-resource lookup, live reset, theme replacement and fail-open", Resources),
            ("Compiled editor XAML, localisation, classic fallback and hex typing", Editor),
        })
        {
            try { test.Body(); results.Add(new { test.Name, Passed = true }); Console.WriteLine("PASS " + test.Name); }
            catch (Exception e) { failures++; results.Add(new { test.Name, Passed = false, Error = e.ToString() }); Console.Error.WriteLine("FAIL " + test.Name + "\n" + e); }
            finally
            {
                // A failed group must not leave theme keys/windows for the next group.
                foreach (Window window in app.Windows.Cast<Window>().ToArray()) window.Close();
                app.Resources = new ResourceDictionary();
            }
        }
        File.WriteAllText(Path.Combine(evidence, "results.json"), JsonSerializer.Serialize(new
        {
            Head = Environment.GetEnvironmentVariable("UX010_HEAD"), Framework = RuntimeInformation.FrameworkDescription,
            Assertions = assertions, Failures = failures, Tests = results,
            OwnerVisualAcceptance = "Not performed by this automated test",
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{assertions} assertions; {failures} failed groups");
        app.Shutdown();
        return failures == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Throws<T>(Action body) where T : Exception
    {
        try { body(); } catch (T) { assertions++; return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static StyleDocument Parse(string json) => StyleDocument.Parse(json, s => throw new Exception(s));

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Styles()
    {
        Check(Parse("{\"parts\":{}}").IsEmpty, "legacy empty style");
        var style = Parse("{\"start\":{\"recolour\":\"#008c24ff\"}}");
        Check(style.StartRecolour == Purple && !style.IsEmpty, "Start-only/alpha-normalised style must persist");
        style.Name = "Start regression";
        style.Palette[7] = Colors.Teal;
        style.Set("bar", "background", BrushSpec.Gradient(45, Colors.Black, Colors.SlateGray));
        var copy = Parse(style.Clone().ToJson());
        Check(copy.StartRecolour == Purple && copy.Name == style.Name && copy.Palette[7] == Colors.Teal, "clone/JSON metadata");
        Check(copy.Get("bar", "background").IsGradient, "existing gradient retained");
        Colourways.Apply(copy, Colors.Olive, Colors.Orange);
        Check(copy.StartRecolour == Purple && copy.Palette[7] == Colors.Teal && copy.Get("bar", "background").IsGradient, "UX-009 must not replace Start/palette/bar");
        style.StartRecolour = null;
        Check(!style.ToJson().Contains("recolour") && copy.StartRecolour == Purple, "reset and clone independence");
        copy.StartRecolour = null;
        Check(copy.Get("button", "background") != null, "Start reset must retain generated scheme");
        string folder = Path.Combine(Path.GetTempPath(), "ux010-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(folder, "saved.json");
            var startOnly = new StyleDocument { StartRecolour = Purple };
            startOnly.Save(path);
            Check(StyleDocument.Load(path, _ => { }).StartRecolour == Purple, "saved style reload");
            startOnly.StartRecolour = null;
            Check(startOnly.IsEmpty, "reset Start-only style becomes empty");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        foreach (string bad in new[] { "false", "[]", "123", "\"nonsense\"", "{\"stops\":[\"red\"]}" })
        {
            var warnings = new List<string>();
            var parsed = StyleDocument.Parse("{\"parts\":{\"bar\":{\"background\":\"red\"}},\"start\":{\"recolour\":" + bad + "}}", warnings.Add);
            Check(parsed.StartRecolour == null && parsed.Get("bar", "background").Color == Colors.Red && warnings.Count == 1, "bad Start value must not discard brushes: " + bad);
        }
        Check(Parse("{\"parts\":{},\"start\":{\"recolour\":null}}").IsEmpty, "explicit null restores theme");
        Throws<FormatException>(() => Parse("[]"));
        Throws<FormatException>(() => Parse("{\"parts\":false,\"start\":{}}"));
        Throws<FormatException>(() => Parse("{"));
    }

    private static BitmapSource Synthetic(PixelFormat format)
    {
        byte[] bytes = new byte[12 * 3 * 4];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 12; x++)
            {
                Color c = x == 8 ? Colors.White : x == 9 ? Colors.Black : Color.FromRgb(40, 160, 60);
                byte a = x == 10 ? (byte)0 : x == 11 ? (byte)128 : (byte)255;
                int i = (y * 12 + x) * 4;
                bytes[i] = c.B; bytes[i + 1] = c.G; bytes[i + 2] = c.R; bytes[i + 3] = a;
                if (format == PixelFormats.Pbgra32)
                    for (int k = 0; k < 3; k++) bytes[i + k] = (byte)((bytes[i + k] * a + 127) / 255);
            }
        var source = BitmapSource.Create(12, 3, 120, 144, format, null, bytes, 48);
        source.Freeze();
        return source;
    }
    private static byte[] Bytes(BitmapSource source)
    {
        BitmapSource converted = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        byte[] bytes = new byte[source.PixelWidth * source.PixelHeight * 4];
        converted.CopyPixels(bytes, source.PixelWidth * 4, 0);
        return bytes;
    }
    private static void PixelContract(BitmapSource source, BitmapSource result, string name)
    {
        Check(source.PixelWidth == result.PixelWidth && source.PixelHeight == result.PixelHeight, name + " dimensions");
        Check(source.DpiX == result.DpiX && source.DpiY == result.DpiY && result.IsFrozen, name + " DPI/frozen");
        byte[] a = Bytes(source), b = Bytes(result);
        for (int i = 0; i < a.Length; i += 4)
        {
            Check(a[i + 3] == b[i + 3], name + " alpha at " + i);
            int la = Math.Max(a[i], Math.Max(a[i + 1], a[i + 2])) + Math.Min(a[i], Math.Min(a[i + 1], a[i + 2]));
            int lb = Math.Max(b[i], Math.Max(b[i + 1], b[i + 2])) + Math.Min(b[i], Math.Min(b[i + 1], b[i + 2]));
            Check(Math.Abs(la - lb) <= 1, name + " HSL lightness at " + i);
        }
    }
    private static void PixelsAndCache()
    {
        foreach (PixelFormat format in new[] { PixelFormats.Bgra32, PixelFormats.Pbgra32 })
        {
            BitmapSource source = Synthetic(format);
            byte[] before = Bytes(source);
            BitmapSource first = StartButtonColourways.Recolour(source, Purple);
            PixelContract(source, first, format.ToString());
            Check(Bytes(source).SequenceEqual(before), "source mutation");
            Check(!Bytes(first).SequenceEqual(before), "actual recolouring");
            Check(ReferenceEquals(first, StartButtonColourways.Recolour(source, Purple)), "repeat uses cached image");
            Check(ReferenceEquals(first, StartButtonColourways.Recolour(source, Color.FromArgb(0, 140, 36, 255))), "target alpha ignored");
            byte[] after = Bytes(first);
            foreach (int x in new[] { 8, 9, 10 })
                Check(before.Skip(x * 4).Take(4).SequenceEqual(after.Skip(x * 4).Take(4)), "white/black/transparent must be exact");
            Check(!before.Skip(44).Take(3).SequenceEqual(after.Skip(44).Take(3)), "semi-transparent fringe transforms in straight RGB");
            StartButtonColourways.Recolour(source, Colors.Orange);
            Check(Bytes(first).SequenceEqual(Bytes(StartButtonColourways.Recolour(source, Purple))), "changing colours must not compound");
            Color[] p = StartButtonColourways.Extract(source); p[0] = Colors.Red;
            Check(StartButtonColourways.Extract(source)[0] != Colors.Red, "palette caller cannot mutate cache");
            var mutable = new WriteableBitmap(source);
            var old = StartButtonColourways.Recolour(mutable, Purple);
            byte[] dark = Enumerable.Range(0, 8).SelectMany(_ => new byte[] { 32, 32, 32, 255 }).ToArray();
            mutable.WritePixels(new Int32Rect(0, 0, 8, 1), dark, 32, 0);
            Check(!ReferenceEquals(old, StartButtonColourways.Recolour(mutable, Purple)), "mutable source invalidates cache");
        }
        byte[] transparent = { 90, 80, 70, 0 };
        var empty = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, transparent, 4);
        Check(StartButtonColourways.Extract(empty).Length == 0 && ReferenceEquals(empty, StartButtonColourways.Recolour(empty, Purple)), "transparent fallback");
        var red = BitmapSource.Create(12, 3, 96, 96, PixelFormats.Bgra32, null,
            Enumerable.Range(0, 36).SelectMany(_ => new byte[] { 20, 10, 240, 255 }).ToArray(), 48);
        byte[] cyan = Bytes(StartButtonColourways.Recolour(red, Colors.Cyan));
        Check(cyan[0] > 200 && cyan[1] > 200 && cyan[2] < 30, "shortest hue wrap across red");
        byte[] grey = Bytes(StartButtonColourways.Recolour(red, Colors.Gray));
        Check(grey[0] == grey[1] && grey[1] == grey[2], "neutral target removes face saturation");
        Throws<ArgumentNullException>(() => StartButtonColourways.Extract((BitmapSource)null));
        Throws<ArgumentException>(() => StartButtonColourways.Extract(empty, Kind.Vista));
        var oversized = BitmapSource.Create(1025, 1024, 96, 96, PixelFormats.Bgra32, null, new byte[1025 * 1024 * 4], 1025 * 4);
        Throws<ArgumentException>(() => StartButtonColourways.Extract(oversized));
    }

    private static BitmapSource Load(string file)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", file));
        return BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
    }
    private static void SavePng(BitmapSource image, string name)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(Path.Combine(evidence, name)); encoder.Save(file);
    }
    private static void Fixtures()
    {
        foreach (var entry in new[] { ("startxpbluebutton.png", Kind.Background), ("startxpgreenbutton.png", Kind.Background),
            ("startnoir.png", Kind.Background), ("startxproyalebutton.png", Kind.Background), ("startlhaerobutton.png", Kind.Longhorn) }
            .Concat(Orbs.Select(o => (o.File, Kind.Vista))))
        {
            BitmapSource source = Load(entry.Item1);
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", entry.Item1);
            byte[] hash = SHA256.HashData(File.ReadAllBytes(path));
            Color[] palette = StartButtonColourways.Extract(source, entry.Item2);
            Check(palette.Length >= 1 && palette.Length <= 3 && palette.All(c => c.A == 255), entry.Item1 + " usable palette");
            if (entry.Item1 == "startxpbluebutton.png" || entry.Item1 == "startxpgreenbutton.png")
            {
                Colourways.ToHsl(palette[0], out double hue, out _, out _);
                Check(hue > 0.15 && hue < 0.40, "XP extraction must select face green/olive, not transparent/logo pixels");
            }
            var result = StartButtonColourways.Recolour(source, Purple, entry.Item2);
            PixelContract(source, result, entry.Item1);
            byte[] before = Bytes(source), after = Bytes(result);
            int frameSize = before.Length / 3;
            for (int state = 0; state < 3; state++)
                Check(!before.Skip(state * frameSize).Take(frameSize).SequenceEqual(after.Skip(state * frameSize).Take(frameSize)), entry.Item1 + " unchanged state " + state);
            if (entry.Item1 == "startnoir.png")
            {
                Colourways.ToHsl(Purple, out double targetHue, out _, out _);
                // The original hover/pressed faces are green, unlike the blue-black normal
                // face. Counting any changed pixel let a tinted border hide an unchanged face.
                // Check the central face region in every state against the requested hue.
                int frameHeight = source.PixelHeight / 3;
                for (int state = 0; state < 3; state++)
                    for (int y = frameHeight / 3; y < frameHeight * 2 / 3; y++)
                        for (int x = source.PixelWidth / 4; x < source.PixelWidth * 3 / 4; x++)
                        {
                            int i = (state * frameHeight * source.PixelWidth + y * source.PixelWidth + x) * 4;
                            Color c = Color.FromRgb(after[i + 2], after[i + 1], after[i]);
                            Colourways.ToHsl(c, out double hue, out double saturation, out _);
                            double distance = Math.Abs(hue - targetHue);
                            Check(saturation > 0.12 && Math.Min(distance, 1 - distance) < 0.10,
                                "Royale Noir face did not follow target in state " + state);
                        }
            }
            if (entry.Item2 != Kind.Background)
            {
                var points = entry.Item2 == Kind.Vista ? new[] { (26, 15), (35, 19), (24, 25), (33, 28) }
                    : new[] { (31, 9), (39, 11), (30, 18), (37, 20) };
                int width = entry.Item2 == Kind.Vista ? 60 : 78, height = entry.Item2 == Kind.Vista ? 46 : 28;
                foreach (var p in points)
                    for (int state = 0; state < 3; state++)
                    {
                        int x = p.Item1 * source.PixelWidth / width, y = p.Item2 * (source.PixelHeight / 3) / height;
                        int i = state * frameSize + (y * source.PixelWidth + x) * 4;
                        Check(before.Skip(i).Take(4).SequenceEqual(after.Skip(i).Take(4)), entry.Item1 + " flag pane/state changed");
                    }
            }
            SavePng(source, "original-" + entry.Item1);
            SavePng(result, "purple-" + entry.Item1);
            SavePng(StartButtonColourways.Recolour(source, Colors.DarkOrange, entry.Item2), "orange-" + entry.Item1);
            Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))), "theme asset file changed");
        }
    }

    private static void Resources()
    {
        var root = Application.Current.Resources;
        var theme = new ResourceDictionary();
        foreach (var orb in Orbs) theme[orb.Key] = Load(orb.File);
        var flag = Load("startxpblue.png"); theme["StartIconImage"] = flag;
        root.MergedDictionaries.Add(theme);
        var overlay = new ResourceDictionary();
        Func<string, object> original = key => StyleLayer.FindThemeResource(root, overlay, key);
        Check(StartButtonColourways.Apply(overlay, Purple, original, s => throw new Exception(s)) == 6, "all six Vista resources");
        Check(!overlay.Contains("StartIconImage") && ReferenceEquals(original("StartIconImage"), flag), "separate flag never overridden");
        var image = new Image();
        // Application resource changes invalidate registered window trees, not detached Images.
        // Do not show the host: this tests real WPF propagation without launching shell UI.
        _ = new Window { Content = image, ShowInTaskbar = false };
        image.SetResourceReference(Image.SourceProperty, "StartButtonImageLarge");
        DrainDispatcher();
        Check(ReferenceEquals(image.Source, theme["StartButtonImageLarge"]), "initial live source");
        root.MergedDictionaries.Add(overlay);
        DrainDispatcher();
        Check(ReferenceEquals(image.Source, overlay["StartButtonImageLarge"]), "live override");
        Check(ReferenceEquals(original("StartButtonImageLarge"), theme["StartButtonImageLarge"]), "lookup excludes own preview");
        Check(StartButtonColourways.Extract(original).SequenceEqual(StartButtonColourways.Extract((BitmapSource)theme["StartButtonImageLarge"], Kind.Vista)), "extraction from original while recoloured");
        Color reference = StartButtonColourways.Extract(original)[0];
        foreach (var orb in Orbs)
            Check(Bytes((BitmapSource)overlay[orb.Key]).SequenceEqual(Bytes(StartButtonColourways.Recolour((BitmapSource)theme[orb.Key], Purple, Kind.Vista, reference))), "shared orb reference " + orb.Key);
        root.MergedDictionaries.Remove(overlay);
        DrainDispatcher();
        Check(ReferenceEquals(image.Source, theme["StartButtonImageLarge"]), "reset restores exact original resource");
        root.MergedDictionaries.Remove(theme);
        Check(!StartButtonColourways.HasImage(original), "classic theme has no bitmap override");
        var xp = new ResourceDictionary { ["StartButtonImage"] = Load("startxpbluebutton.png") };
        root.MergedDictionaries.Add(xp);
        Check(ReferenceEquals(original("StartButtonImage"), xp["StartButtonImage"]), "live theme switch sees new source");
        root.MergedDictionaries.Remove(xp);
        var nested = new ResourceDictionary(); nested.MergedDictionaries.Add(xp);
        var outer = new ResourceDictionary(); outer.MergedDictionaries.Add(nested); outer.MergedDictionaries.Add(overlay);
        Check(ReferenceEquals(StyleLayer.FindThemeResource(outer, overlay, "StartButtonImage"), xp["StartButtonImage"]), "nested lookup");
        outer["StartButtonImage"] = flag;
        Check(ReferenceEquals(StyleLayer.FindThemeResource(outer, overlay, "StartButtonImage"), flag), "direct resources take precedence");
        var warnings = new List<string>();
        var fallback = new ResourceDictionary { ["ButtonFace"] = Brushes.Red };
        object Broken(string key) => key == "StartButtonImageLarge" ? throw new IOException("missing custom bitmap") : key == "StartButtonImage" ? xp[key] : null;
        Check(StartButtonColourways.HasImage(Broken), "bad lazy image must not break editor");
        Check(StartButtonColourways.Apply(fallback, Purple, Broken, warnings.Add) == 1 && warnings.Count > 0 && fallback["ButtonFace"] == Brushes.Red, "per-image fail-open retains valid brushes");
        Check(StartButtonColourways.Apply(new ResourceDictionary(), null, _ => throw new Exception("must not lookup"), null) == 0, "unset colour is no-op");
    }

    private static void Editor()
    {
        var resources = Application.Current.Resources.MergedDictionaries;
        foreach (string path in new[] { "Controls/StartColourwaysStrings.xaml", "Languages/English.xaml", "Themes/System.xaml" })
            resources.Add(new ResourceDictionary { Source = new Uri("/RetroBar;component/" + path, UriKind.Relative) });
        var editor = new StyleEditor();
        // A non-shown logical root permits DynamicResource invalidation without IsVisibleChanged
        // loading user styles. Never show this host or run the save timer in these tests.
        _ = new Window { Content = editor, ShowInTaskbar = false };
        DrainDispatcher();
        var parts = (ComboBox)editor.FindName("cboPart");
        Check(parts.Items.Count == StyleMap.Parts.Length + 1, "all existing part labels plus Start");
        parts.SelectedIndex = StyleMap.Parts.Length;
        var panel = (StackPanel)editor.FindName("panelProperties");
        Check(!panel.Children.OfType<CheckBox>().Single().IsEnabled, "classic disables bitmap recolouring");
        var theme = new ResourceDictionary { ["StartButtonImage"] = Load("startxpbluebutton.png") };
        resources.Add(theme);
        parts.SelectedIndex = 0; parts.SelectedIndex = StyleMap.Parts.Length;
        var enabled = panel.Children.OfType<CheckBox>().Single();
        Check(enabled.IsEnabled, "bitmap theme enables recolouring");
        // Do not pump the dispatcher with a pending save; this test must not touch active styles.
        var timer = (DispatcherTimer)typeof(StyleEditor).GetField("_saveTimer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
        try
        {
            enabled.IsChecked = true;
            var field = panel.Children.OfType<ColourField>().Single();
            var hex = field.Children.OfType<TextBox>().Single();
            hex.Text = "#";
            foreach (char digit in "123456") hex.Text += digit;
            Check(hex.Text == "#123456" && field.Value == Color.FromRgb(0x12, 0x34, 0x56), "hex typing not rewritten after shorthand prefix");
            hex.Text = "#80123456";
            var style = (StyleDocument)typeof(StyleEditor).GetField("_style", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
            Check(style.StartRecolour?.A == 255 && style.StartRecolour == Color.FromRgb(0x12, 0x34, 0x56), "target alpha ignored in saved model");
            enabled.IsChecked = false;
            Check(style.StartRecolour == null, "uncheck restores theme in model");
        }
        finally { timer.Stop(); resources.Remove(theme); }
        // The non-shown ComboBox has not generated its dropdown containers. Attach the
        // deferred item to a non-shown logical root, as the real dropdown does when opened.
        var startItem = (ComboBoxItem)parts.Items[StyleMap.Parts.Length];
        if (LogicalTreeHelper.GetParent(startItem) == null)
            _ = new Window { Content = startItem, ShowInTaskbar = false };
        DrainDispatcher();
        Application.Current.Resources["uxui_start_part"] = "Start localised";
        DrainDispatcher(); // The editor save timer was stopped in the finally block above.
        Check(((ComboBoxItem)parts.Items[StyleMap.Parts.Length]).Content as string == "Start localised", "Start label remains dynamic/localisable");
    }
}
