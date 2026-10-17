using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RetroBar.Utilities;

// UXUI-Extreme (UX-011): test the production assembly on real WPF/STA, not a port of it.
// All file writes use a temporary test directory or the requested evidence directory.
// Never start RetroBar's shell, show its Properties window or write the user's active style.
internal static partial class Program
{
    private static int assertions;
    private static string evidence, temporary;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [STAThread]
    private static int Main(string[] args)
    {
        evidence = Path.GetFullPath(args.Length > 0 ? args[0] : "texture-evidence");
        temporary = Path.Combine(Path.GetTempPath(), "ux011-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        Directory.CreateDirectory(temporary);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var results = new List<object>();
        int failures = 0;
        foreach (var test in new (string Name, Action Body)[]
        {
            ("JSON, clone, reset, legacy brushes and palette contracts", Documents),
            ("LUT pixels, alpha, contrast and bounded immutable cache", Pixels),
            ("Bounded custom PNG decoding, replacement and fail-open", CustomPng),
            ("Real WPF tiling at fractional DPI, texture and legacy scales", Tiling),
            ("All part mappings, vertical brushes and per-property isolation", Resources),
            ("Overlay-only fallback, reserved filenames and linked directories", EdgeCases),
            ("Compiled editor, mode transitions, locale input and live palette", Editor),
            ("Tone adjustments, resampling, sharpening and generator parameters", Adjustments),
            ("Layout and Icons tabs build from XAML", Tabs),
            ("Quick launch drop creates shortcuts", QuickLaunchDrop),
            ("Presets, configs and the texture palette", Presets),
            ("Clock and Colours tabs build", ClockAndStyleTabs),
            ("Palette fill, texture palette size and row rule lists", PalettesAndRows),
            ("Start/Quick sections, grips and row rules", SectionsAndDefaults),
            ("Tabs render for review", RenderTabs),
        })
        {
            try { test.Body(); results.Add(new { test.Name, Passed = true }); Console.WriteLine("PASS " + test.Name); }
            catch (Exception e) { failures++; results.Add(new { test.Name, Passed = false, Error = e.ToString() }); Console.Error.WriteLine("FAIL " + test.Name + "\n" + e); }
            finally
            {
                foreach (Window window in app.Windows.Cast<Window>().ToArray()) window.Close();
                app.Resources = new ResourceDictionary();
            }
        }
        File.WriteAllText(Path.Combine(evidence, "results.json"), JsonSerializer.Serialize(new
        {
            Head = Environment.GetEnvironmentVariable("UX011_HEAD"), Framework = RuntimeInformation.FrameworkDescription,
            Assertions = assertions, Failures = failures, Tests = results,
            OwnerVisualAcceptance = "Not performed. Owner must test exact CI build on their taskbar.",
        }, new JsonSerializerOptions { WriteIndented = true }));
        Directory.Delete(temporary, true);
        Console.WriteLine($"{assertions} assertions; {failures} failed groups");
        app.Shutdown();
        return failures == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { assertions++; return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static StyleDocument Parse(string json) => StyleDocument.Parse(json, s => throw new InvalidOperationException(s));
    private static BrushSpec Texture(string name = "brushed") => new BrushSpec { Texture = new TextureSpec { Name = name } };
    private static byte[] Bytes(BitmapSource image)
    {
        BitmapSource source = image.Format == PixelFormats.Bgra32 ? image : new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        byte[] bytes = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(bytes, source.PixelWidth * 4, 0);
        return bytes;
    }
    private static void SavePng(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file);
    }
    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
