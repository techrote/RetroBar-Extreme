using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroBar.Utilities;

// UXUI-Extreme: tone adjustments, resampling, sharpening and generator parameters.
internal static partial class Program
{
    private static TextureSpec Plain(string name) => new TextureSpec { Name = name };

    private static byte[] TileOf(TextureSpec spec) => Bytes((BitmapSource)LutTextures.Create(spec).ImageSource);

    private static void Adjustments()
    {
        // Defaults do nothing; each adjustment changes the result.
        byte[] baseline = TileOf(Plain("clouds"));
        var bright = Plain("clouds"); bright.Brightness = 0.3;
        var brightBytes = TileOf(bright);
        Check(brightBytes.Where((b, i) => i % 4 != 3).Average(b => (double)b) > baseline.Where((b, i) => i % 4 != 3).Average(b => (double)b), "brightness lightens");
        var grey = Plain("clouds"); grey.Lut = new[] { Colors.Red, Colors.Blue }.ToList(); grey.Saturation = 0;
        var greyBytes = TileOf(grey);
        Check(Enumerable.Range(0, greyBytes.Length / 4).All(i => greyBytes[i * 4] == greyBytes[i * 4 + 1] && greyBytes[i * 4 + 1] == greyBytes[i * 4 + 2]), "saturation 0 is grey");
        var white = Plain("clouds"); white.WhiteLevel = 0.5;
        Check(TileOf(white).Where((b, i) => i % 4 == 0).Max() == 255, "white level pushes highlights to white");

        // Resampling: new tile size, seamless wrap, kernels preserve a flat field.
        foreach (string method in TextureResampler.Methods.Skip(1))
        {
            var spec = Plain("checker"); spec.Scale = 2; spec.Resample = method;
            var brush = LutTextures.Create(spec);
            var image = (BitmapSource)brush.ImageSource;
            Check(image.PixelWidth == 128 && image.PixelHeight == 128, method + " scaled tile size");
            Check(brush.Viewport.Width == 128, method + " drawn 1:1");
            spec.Scale = 0.5;
            Check(((BitmapSource)LutTextures.Create(spec).ImageSource).PixelWidth == 32, method + " shrinks");
            var flat = Plain("checker"); flat.Contrast = 0; flat.Scale = 1.5; flat.Resample = method;
            Check(TileOf(flat).Where((b, i) => i % 4 != 3).All(c => Math.Abs(c - 128) <= 1), method + " keeps a flat field flat");
        }
        var smooth = Plain("checker"); smooth.Scale = 2;
        Check(((BitmapSource)LutTextures.Create(smooth).ImageSource).PixelWidth == 64, "smooth leaves scaling to WPF");

        // Sharpening raises local contrast and does not touch a flat tile.
        var soft = Plain("clouds");
        var sharp = Plain("clouds"); sharp.Sharpen = 2;
        double spread(byte[] b) { var v = b.Where((x, i) => i % 4 == 0).Select(x => (double)x).ToArray(); double m = v.Average(); return v.Average(x => (x - m) * (x - m)); }
        Check(spread(TileOf(sharp)) > spread(TileOf(soft)), "sharpen increases contrast");

        // Generator parameters: seeds, octaves and frequency change the right patterns.
        foreach (string name in LutTextures.BuiltInNames)
        {
            var a = Plain(name); var b = Plain(name);
            var uses = TextureGenerators.Uses(name);
            b.Seed = 7;
            Check(uses.HasFlag(TextureGenerators.Param.Seed) == !TileOf(a).SequenceEqual(TileOf(b)), name + " seed matters only where used");
            b = Plain(name); b.Frequency = TextureGenerators.DefaultFrequency(name) == 2 ? 4 : 2;
            if (uses.HasFlag(TextureGenerators.Param.Frequency))
                Check(!TileOf(a).SequenceEqual(TileOf(b)), name + " frequency changes the pattern");
            b = Plain(name); b.Octaves = 1;
            if (uses.HasFlag(TextureGenerators.Param.Octaves) && TextureGenerators.DefaultOctaves(name) > 1)
                Check(!TileOf(a).SequenceEqual(TileOf(b)), name + " octaves change the pattern");
        }

        // JSON round trip keeps the new settings and omits defaults.
        var full = Plain("marble");
        full.Seed = 12; full.Octaves = 6; full.Frequency = 3; full.Strength = 1.5; full.Brightness = -0.2; full.Saturation = 1.4;
        full.WhiteLevel = 0.8; full.Resample = "lanczos"; full.Sharpen = 1.2; full.SharpenRadius = 1.5;
        string json = Write(full);
        var back = TextureSpec.Parse(JsonDocument.Parse(json).RootElement);
        Check(back.Seed == 12 && back.Octaves == 6 && back.Frequency == 3 && back.Strength == 1.5 && back.Brightness == -0.2 &&
              back.Saturation == 1.4 && back.WhiteLevel == 0.8 && back.Resample == "lanczos" && back.Sharpen == 1.2 && back.SharpenRadius == 1.5, "new settings round-trip");
        string plainJson = Write(Plain("marble"));
        Check(!plainJson.Contains("seed") && !plainJson.Contains("resample") && !plainJson.Contains("sharpen") && !plainJson.Contains("brightness"), "defaults omitted from JSON");
        var bad = Plain("marble"); bad.Resample = "bogus";
        Throws<FormatException>(() => bad.Validate());
        bad = Plain("marble"); bad.WhiteLevel = 0;
        Throws<FormatException>(() => bad.Validate());
    }

    private static string Write(TextureSpec spec)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) spec.Write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

internal static partial class Program
{
    // The Layout and Icons tabs build from their XAML, fill from settings and measure without throwing.
    private static void Tabs()
    {
        foreach (string path in new[] { "Controls/LayoutStrings.xaml", "Languages/English.xaml", "Themes/System.xaml" })
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RetroBar;component/" + path, UriKind.Relative) });

        foreach (var editor in new System.Windows.Controls.UserControl[] { new RetroBar.Controls.LayoutEditor(), new RetroBar.Controls.IconsEditor() })
        {
            _ = new Window { Content = editor, ShowInTaskbar = false }; // Never shown.
            editor.GetType().GetMethod("Load", PrivateInstance).Invoke(editor, null);
            editor.Measure(new Size(500, 2000));
            Check(editor.DesiredSize.Height > 100, editor.GetType().Name + " builds and measures");
        }
        var layout = (RetroBar.Controls.LayoutEditor)new RetroBar.Controls.LayoutEditor();
        Check(Named<System.Windows.Controls.ComboBox>(layout, "cboTaskAlign").Items.Count == 3, "task button alignment choices");
        Check(Named<System.Windows.Controls.ComboBox>(layout, "cboTogglePlacement").Items.Count == 3, "toggle placement choices");
    }
}

internal static partial class Program
{
    // Dropped programs and shortcuts become shortcuts in the quick launch folder.
    private static void QuickLaunchDrop()
    {
        Application.Current.Resources["menuTextRenderingModeConverter"] = new RetroBar.Converters.BoolToTextRenderingModeConverter(); // App.xaml's, which the Show desktop cell in the toolbar uses
        string folder = System.IO.Path.Combine(temporary, "ql");
        System.IO.Directory.CreateDirectory(folder);
        string exe = System.IO.Path.Combine(Environment.SystemDirectory, "notepad.exe");
        var toolbar = new RetroBar.Controls.Toolbar();
        typeof(RetroBar.Controls.Toolbar).GetProperty("Path").DeclaringType.GetField("PathProperty").GetValue(null);
        toolbar.SetValue(RetroBar.Controls.Toolbar.PathProperty, folder);
        var list = new System.Collections.Specialized.StringCollection { exe };
        toolbar.AddToSource(list);
        Check(System.IO.File.Exists(System.IO.Path.Combine(folder, "notepad.lnk")), "dropped program becomes a shortcut");
        string lnk = System.IO.Path.Combine(folder, "notepad.lnk");
        toolbar.AddToSource(new System.Collections.Specialized.StringCollection { lnk });
        Check(System.IO.File.Exists(System.IO.Path.Combine(folder, "notepad (1).lnk")), "dropped shortcut is copied under a new name");
    }
}

internal static partial class Program
{
    // Presets, copy / paste configs and the texture palette.
    private static void Presets()
    {
        Check(TexturePresets.BuiltInCount == 65, "five built-in presets for each of the 13 patterns");
        foreach (string pattern in LutTextures.BuiltInNames)
        {
            var list = TexturePresets.ForPattern(pattern);
            Check(list.Count == 5, pattern + " has five presets");
            Check(list.Select(p => p.Label).Distinct().Count() == 5 && list.All(p => p.Name == pattern && !string.IsNullOrEmpty(p.Label)), pattern + " preset names are distinct");
            foreach (TextureSpec preset in list)
            {
                preset.Validate();
                Check(LutTextures.Create(preset) != null, pattern + " / " + preset.Label + " renders");
                string text = TextureConfig.ToText(preset);
                var back = TextureConfig.FromText(text);
                Check(TextureConfig.ToText(back) == text && back.Label == preset.Label, pattern + " / " + preset.Label + " survives text");
            }
        }

        // Parameter-only configs keep the colours they are applied to.
        var mine = new TextureSpec { Name = "clouds", Lut = new System.Collections.Generic.List<System.Windows.Media.Color> { Colors.Red, Colors.Blue } };
        var parsed = TextureConfig.FromText("{ \"texture\": \"cells\", \"frequency\": 9 }", mine);
        Check(parsed.Name == "cells" && parsed.Frequency == 9 && parsed.Lut.SequenceEqual(mine.Lut), "config without colours keeps the current ones");
        Throws<FormatException>(() => TextureConfig.FromText("{ \"texture\": \"cells\" }"));
        Throws<FormatException>(() => TextureConfig.FromText("not json"));
        Throws<FormatException>(() => TextureConfig.FromText("{ \"lut\": [\"#000\", \"#fff\"] }"));
        Throws<FormatException>(() => TextureConfig.FromText("{ \"texture\": \"cells\", \"bogus\": 1, \"lut\": \"palette\" }"));

        var bricks = new TextureSpec { Name = "bricks", Frequency = 6, Label = "x" };
        mine.ApplyParameters(bricks);
        Check(mine.Name == "bricks" && mine.Frequency == 6 && mine.Lut.Count == 2 && mine.Lut[0] == Colors.Red, "applying a preset keeps colours");

        // Preset files: a list, and one bare config.
        var file = TextureConfig.WriteFile(new[] { TexturePresets.ForPattern("marble")[0], TexturePresets.ForPattern("dots")[1] });
        var read = TextureConfig.ReadFile(file);
        Check(read.Count == 2 && read[0].Label == "Classic veins" && read[1].Name == "dots", "preset file round-trips");
        Check(TextureConfig.ReadFile(TextureConfig.ToText(TexturePresets.ForPattern("waves")[0])).Count == 1, "a single config is a one-preset file");

        // The texture palette is saved with the style.
        var doc = new StyleDocument();
        doc.TexturePalette[2] = new TextureSpec { Name = "weave", Frequency = 6, UsePalette = true };
        doc.TexturePalette[7] = new TextureSpec { Name = "dots", Lut = new System.Collections.Generic.List<System.Windows.Media.Color> { Colors.Black, Colors.Gold } };
        Check(!doc.IsEmpty, "a texture palette alone is not an empty style");
        var warnings = new System.Collections.Generic.List<string>();
        var again = StyleDocument.Parse(doc.ToJson(), warnings.Add);
        Check(warnings.Count == 0 && again.TexturePalette[2]?.Name == "weave" && again.TexturePalette[2].UsePalette && again.TexturePalette[7]?.Lut[1] == Colors.Gold && again.TexturePalette[0] == null, "texture palette round-trips");
        Check(doc.Clone().TexturePalette[7] != doc.TexturePalette[7] && doc.Clone().TexturePalette[7].Name == "dots", "clone copies texture palette slots");
        var damaged = StyleDocument.Parse("{ \"parts\": {}, \"texturePalette\": [ { \"texture\": \"nope\", \"lut\": \"palette\" }, { \"texture\": \"dots\", \"lut\": \"palette\" } ] }", warnings.Add);
        Check(damaged.TexturePalette[0] == null && damaged.TexturePalette[1]?.Name == "dots" && warnings.Count == 1, "a bad palette slot is skipped, the rest kept");
    }

    // Renders each tab to a PNG in the evidence folder, so layout can be looked at.
    private static void RenderTabs()
    {
        foreach (string path in new[] { "Controls/LayoutStrings.xaml", "Controls/TextureStrings.xaml", "Languages/English.xaml", "Themes/System.xaml" })
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RetroBar;component/" + path, UriKind.Relative) });

        var style = new RetroBar.Controls.StyleEditor();
        var doc = typeof(RetroBar.Controls.StyleEditor).GetField("_style", PrivateInstance);
        var loaded = StyleDocument.Parse(StyleDocument.ExampleJson("Industreel"), _ => { });
        doc.SetValue(style, loaded);
        typeof(RetroBar.Controls.StyleEditor).GetMethod("ShowPaletteFields", PrivateInstance).Invoke(style, null);
        var partCombo = (System.Windows.Controls.ComboBox)typeof(RetroBar.Controls.StyleEditor).GetField("cboPart", PrivateInstance)?.GetValue(style) ?? Named<System.Windows.Controls.ComboBox>(style, "cboPart");
        partCombo.SelectedIndex = 0;

        foreach (var (name, element) in new (string, System.Windows.FrameworkElement)[]
        {
            ("colours", style),
            ("layout", new RetroBar.Controls.LayoutEditor()),
            ("icons", new RetroBar.Controls.IconsEditor()),
            ("clock", new RetroBar.Controls.ClockEditor()),
            ("startquick", StartQuickTab()),
        })
        {
            var window = new Window { Content = element, ShowInTaskbar = false, SizeToContent = SizeToContent.WidthAndHeight };
            element.Measure(new Size(2000, 4000));
            element.Arrange(new Rect(element.DesiredSize));
            element.UpdateLayout();
            int w = (int)Math.Ceiling(element.DesiredSize.Width), h = (int)Math.Ceiling(element.DesiredSize.Height);
            var target = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var back = new System.Windows.Shapes.Rectangle { Width = w, Height = h, Fill = Brushes.White };
            back.Measure(new Size(w, h)); back.Arrange(new Rect(0, 0, w, h));
            target.Render(back);
            target.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(target));
            using (var file = System.IO.File.Create(System.IO.Path.Combine(evidence, "tab-" + name + ".png"))) encoder.Save(file);
            Check(w > 200 && h > 100, name + " tab rendered at " + w + " x " + h);
        }
    }

    private static System.Windows.Controls.Grid StartQuickTab()
    {
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(8) };
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
        var left = new RetroBar.Controls.LayoutEditor { Sections = "StartQuick", VerticalAlignment = VerticalAlignment.Top };
        var right = new System.Windows.Controls.StackPanel();
        right.Children.Add(new RetroBar.Controls.QuickLayoutEditor());
        right.Children.Add(new RetroBar.Controls.IconsEditor { Sections = "StartQuick" });
        System.Windows.Controls.Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private static void SectionsAndDefaults()
    {
        foreach (string path in new[] { "Controls/LayoutStrings.xaml", "Controls/TextureStrings.xaml", "Languages/English.xaml", "Themes/System.xaml" })
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RetroBar;component/" + path, UriKind.Relative) });

        var all = new RetroBar.Controls.LayoutEditor();
        var main = new RetroBar.Controls.LayoutEditor { Sections = "Main" };
        var startQuick = new RetroBar.Controls.LayoutEditor { Sections = "StartQuick" };
        Visibility V(System.Windows.FrameworkElement root, string name) => Named<System.Windows.Controls.GroupBox>(root, name).Visibility;
        Check(V(all, "grpStart") == Visibility.Visible && V(all, "grpToggle") == Visibility.Visible, "all sections show everything");
        Check(V(main, "grpStart") == Visibility.Collapsed && V(main, "grpDesktop") == Visibility.Collapsed && V(main, "grpTaskButtons") == Visibility.Visible, "the Layout tab keeps toggle and task buttons");
        Check(V(startQuick, "grpStart") == Visibility.Visible && V(startQuick, "grpDesktop") == Visibility.Visible && V(startQuick, "grpTaskButtons") == Visibility.Collapsed, "the Start/Quick tab takes the Start button and Show desktop groups");
        var icons = new RetroBar.Controls.IconsEditor { Sections = "StartQuick" };
        Check(V(icons, "grpStartPics") == Visibility.Visible && V(icons, "grpQuickIcons") == Visibility.Visible && V(icons, "grpTray") == Visibility.Collapsed, "start and quick launch pictures move to the Start/Quick tab");
        Check(Named<System.Windows.Controls.ComboBox>(all, "cboShowDesktopPosition").Items.Count == 4, "Show desktop can go inside quick launch");
        Check(Named<System.Windows.Controls.ComboBox>(all, "cboTaskVAlign").Items.Count == 3, "task button vertical position choices");

        var rule = new TaskRowRule { Row = 2, HeightAdjust = 6, Align = TaskButtonAlignOption.Right };
        Check(rule.Clone().HeightAdjust == 6 && rule.Clone().Align == TaskButtonAlignOption.Right, "row rules carry height and alignment");
        Check(Math.Abs(RetroBar.Converters.ThumbOverlayConverter.GripThickness - 3) < 0.01, "grips are a thin, fixed strip");
    }

    private static void PalettesAndRows()
    {
        // Fill with complementary: new, distinct colours for every empty slot asked for.
        var seeds = new System.Collections.Generic.List<Color> { Color.FromRgb(0x20, 0x40, 0x80), Color.FromRgb(0xe0, 0xa0, 0x20) };
        var filled = PaletteFill.Generate(seeds, 6);
        Check(filled.Count == 6, "six colours generated for six empty slots");
        Check(filled.All(c => seeds.All(x => PaletteFill.Distance(c, x) > 15)), "generated colours are not copies of the chosen ones");
        Check(filled.Select(c => c.ToString()).Distinct().Count() == 6, "generated colours are distinct");
        var complement = PaletteFill.Generate(new System.Collections.Generic.List<Color> { Color.FromRgb(0, 0, 255) }, 1)[0];
        Check(complement.R > 150 && complement.G > 150 && complement.B < 120, "the first relative of blue is its complement (yellow)");
        Check(PaletteFill.Generate(new System.Collections.Generic.List<Color>(), 3).Count == 0, "nothing to base colours on, nothing generated");

        Check(StyleDocument.TEXTURE_PALETTE_SIZE == 20 && new StyleDocument().TexturePalette.Length == 20, "twenty texture palette slots");
        var doc = new StyleDocument();
        doc.TexturePalette[19] = new TextureSpec { Name = "waves", UsePalette = true };
        var again = StyleDocument.Parse(doc.ToJson(), _ => { });
        Check(again.TexturePalette[19]?.Name == "waves", "the twentieth slot survives a save");

        // Row rule app lists: names, optional .exe, mixed separators.
        var apps = RetroBar.Controls.TaskRowPanel.Split("Claude.exe, codex;calc,HWiNFO64 ");
        Check(apps.SequenceEqual(new[] { "claude", "codex", "calc", "hwinfo64" }), "app lists split into plain lower-case names");
    }

    private static void ClockAndStyleTabs()
    {
        foreach (string path in new[] { "Controls/LayoutStrings.xaml", "Controls/TextureStrings.xaml", "Languages/English.xaml", "Themes/System.xaml" } /* what App.xaml loads */)
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RetroBar;component/" + path, UriKind.Relative) });
        var clock = new RetroBar.Controls.ClockEditor();
        _ = new Window { Content = clock, ShowInTaskbar = false };
        clock.GetType().GetMethod("Load", PrivateInstance).Invoke(clock, null);
        clock.Measure(new Size(900, 2000));
        Check(clock.DesiredSize.Width > 300 && clock.DesiredSize.Height > 100, "Clock tab builds");
        Check(Named<System.Windows.Controls.ComboBox>(clock, "cboClockMargin").Items.Count == 4, "clock margin choices");
        var settingsType = typeof(RetroBar.Controls.ClockEditor).Assembly.GetType("RetroBar.Utilities.Settings");
        object fresh = Activator.CreateInstance(settingsType, true);
        Check((bool)settingsType.GetProperty("ClockMonospace").GetValue(fresh), "equal-width clock digits are the default");
        var style = new RetroBar.Controls.StyleEditor();
        _ = new Window { Content = style, ShowInTaskbar = false };
        style.GetType().GetMethod("ShowTextureSlots", PrivateInstance).Invoke(style, null); // looks up its own strings
        style.Measure(new Size(1200, 3000));
        Check(style.DesiredSize.Width > 700, "Colours tab is two columns wide");
    }
}
