using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RetroBar.Controls;
using RetroBar.Utilities;

internal static partial class Program
{
    private static void GradientSafety()
    {
        // Legal JSON numbers can overflow a double. A bad brush must be isolated at import,
        // before the editor's first automatic save tries to serialize Infinity.
        foreach (string bad in new[]
        {
            "{\"angle\":1e999,\"stops\":[\"red\",\"blue\"]}",
            "{\"angle\":-1e999,\"stops\":[\"red\",\"blue\"]}",
            "{\"angle\":\"NaN\",\"stops\":[\"red\",\"blue\"]}",
            "{\"stops\":[{\"color\":\"red\",\"offset\":1e999},\"blue\"]}",
            "{\"stops\":[{\"color\":\"red\",\"offset\":-1e999},\"blue\"]}",
        })
        {
            var warnings = new List<string>();
            var style = StyleDocument.Parse("{\"parts\":{\"bar\":{\"background\":" + bad +
                "},\"clock\":{\"foreground\":\"cyan\"}}}", warnings.Add);
            Check(style.Get("bar", "background") == null && warnings.Count == 1 && warnings[0].Contains("bar.background"),
                "invalid gradient is isolated during import: " + bad);
            Check(Parse(style.ToJson()).Get("clock", "foreground").Color == Colors.Cyan,
                "remaining imported style can be saved and reloaded");
        }

        foreach (double angle in new[] { 1e308, -1e308, double.MaxValue, -double.MaxValue })
        {
            var style = Parse("{\"parts\":{\"bar\":{\"background\":{\"angle\":" + angle.ToString("R", CultureInfo.InvariantCulture) +
                ",\"stops\":[\"red\",\"blue\"]}}}}");
            var fill = Parse(style.ToJson()).Get("bar", "background");
            Check(fill.Angle == angle, "large finite gradient angle survives a save/load round trip");
            foreach (double rotate in new[] { 0.0, -90.0, angle })
            {
                var brush = (LinearGradientBrush)fill.ToBrush(rotate);
                Check(brush.IsFrozen && new[] { brush.StartPoint.X, brush.StartPoint.Y, brush.EndPoint.X, brush.EndPoint.Y }
                    .All(n => double.IsFinite(n) && n >= 0 && n <= 1), "large finite angles produce bounded finite gradient endpoints");
                Check(brush.GradientStops.Count == 2 && brush.GradientStops[0].Color == Colors.Red &&
                    brush.GradientStops[1].Color == Colors.Blue, "large angle retains the chosen colours");
            }
        }

        foreach (var sample in new[]
        {
            (Angle: 450.0, Rotate: 0.0, Start: new Point(0.5, 0), End: new Point(0.5, 1)),
            (Angle: 90.0, Rotate: -90.0, Start: new Point(0, 0.5), End: new Point(1, 0.5)),
            (Angle: -90.0, Rotate: 0.0, Start: new Point(0.5, 1), End: new Point(0.5, 0)),
        })
        {
            var brush = (LinearGradientBrush)BrushSpec.Gradient(sample.Angle, Colors.Red, Colors.Blue).ToBrush(sample.Rotate);
            Check((brush.StartPoint - sample.Start).Length < 1e-12 && (brush.EndPoint - sample.End).Length < 1e-12,
                "ordinary and wrapped angles retain their existing orientation");
        }
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Throws<FormatException>(() => BrushSpec.Gradient(invalid, Colors.Red, Colors.Blue).ToBrush());
            Throws<ArgumentOutOfRangeException>(() => BrushSpec.Gradient(90, Colors.Red, Colors.Blue).ToBrush(invalid));
        }

        var clamped = Parse("{\"parts\":{\"bar\":{\"background\":{\"stops\":[" +
            "{\"color\":\"red\",\"offset\":-2},{\"color\":\"blue\",\"offset\":3}]}}}}").Get("bar", "background");
        Check(clamped.Stops[0].Offset == 0 && clamped.Stops[1].Offset == 1, "finite legacy offsets keep their existing clamping behavior");
    }

    private static void StylePartReset()
    {
        var baseline = new StyleDocument { Name = "Reset regression", StartRecolour = Colors.Orange };
        foreach (var part in StyleMap.PartProperties)
            foreach (string property in part.Value)
                baseline.Set(part.Key, property, BrushSpec.Solid(Colors.Teal));
        foreach (string part in StyleDocument.BorderParts) baseline.SetBorderFactor(part, 4);
        baseline.Palette[2] = Colors.Purple;
        baseline.TexturePalette[7] = new TextureSpec { Name = "carbon" };

        foreach (string part in StyleMap.Parts.Concat(new[] { "start" }))
        {
            StyleDocument reset = baseline.Clone();
            reset.ResetPart(part);
            Check(!reset.Parts.ContainsKey(part) && !reset.Borders.ContainsKey(part) && reset.BorderFactor(part) == 1,
                part + " reset removes all brush and border overrides");
            Check(reset.StartRecolour == (part == "start" ? (Color?)null : baseline.StartRecolour),
                part + " reset owns only its own Start recolouring");
            Check(reset.Name == baseline.Name && reset.Palette[2] == Colors.Purple && reset.TexturePalette[7].Name == "carbon",
                part + " reset preserves the name and both palettes");
            foreach (var neighbour in baseline.Parts.Where(p => p.Key != part))
                Check(reset.Parts.TryGetValue(neighbour.Key, out var properties) && properties.Count == neighbour.Value.Count &&
                    properties.All(p => p.Value.Color == neighbour.Value[p.Key].Color), part + " reset preserves " + neighbour.Key);
            foreach (var neighbour in baseline.Borders.Where(b => b.Key != part))
                Check(reset.BorderFactor(neighbour.Key) == neighbour.Value, part + " reset preserves border " + neighbour.Key);

            string path = Path.Combine(temporary, "reset-" + part + ".json");
            reset.Save(path);
            var loaded = StyleDocument.Load(path, s => throw new InvalidOperationException(s));
            Check(!loaded.Parts.ContainsKey(part) && loaded.BorderFactor(part) == 1 && loaded.StartRecolour == reset.StartRecolour,
                part + " reset persists through save and reload");
        }

        var borderOnly = new StyleDocument(); borderOnly.SetBorderFactor("bar", 0);
        borderOnly.ResetPart("bar");
        Check(borderOnly.IsEmpty, "resetting a border-only part restores an empty style");
        var startOnly = new StyleDocument { StartRecolour = Colors.Orange }; startOnly.SetBorderFactor("start", 2);
        startOnly.ResetPart("start");
        Check(startOnly.IsEmpty, "Start reset clears both its image recolouring and borders");

        BorderEditorDebounce();
    }

    private static void BorderEditorDebounce()
    {
        foreach (string path in new[] { "Controls/StartColourwaysStrings.xaml", "Controls/LayoutStrings.xaml", "Languages/English.xaml", "Themes/System.xaml" })
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RetroBar;component/" + path, UriKind.Relative) });

        // The compiled editor stays offscreen. Inject a private in-memory document and stop
        // its shared save timer before dispatching anything; never invoke SaveActive or flush.
        var editor = new StyleEditor();
        var original = new StyleDocument();
        original.SetBorderFactor("bar", 2);
        var replacement = new StyleDocument();
        replacement.SetBorderFactor("bar", 3);
        var document = typeof(StyleEditor).GetField("_style", PrivateInstance);
        var save = (DispatcherTimer)typeof(StyleEditor).GetField("_saveTimer", PrivateInstance).GetValue(editor);
        var panel = (StackPanel)editor.FindName("panelProperties");
        var elapsed = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        try
        {
            document.SetValue(editor, original);
            panel.Children.Clear();
            typeof(StyleEditor).GetMethod("AddBorderRow", PrivateInstance).Invoke(editor, new object[] { "bar" });
            var slider = panel.Children.OfType<DockPanel>().Single().Children.OfType<Slider>().Single();
            Check(slider.Value == 2 && !save.IsEnabled, "border row construction does not schedule a save");

            slider.Value = 4;
            // These observations happen before any dispatcher turn. Only publication may be
            // delayed; the selected document must already own the chosen border value.
            bool updatedImmediately = original.BorderFactor("bar") == 4;
            bool usesSharedDebounce = save.IsEnabled;
            save.Stop();
            Check(updatedImmediately && usesSharedDebounce, "border change updates the model immediately and uses the common save debounce");

            original.ResetPart("bar");
            panel.Children.Clear(); // detach the edited row
            document.SetValue(editor, replacement);
            var frame = new DispatcherFrame();
            elapsed.Tick += (s, e) => { elapsed.Stop(); frame.Continue = false; };
            elapsed.Start();
            Dispatcher.PushFrame(frame); // beyond the former private 350 ms timer
            Check(original.BorderFactor("bar") == 1 && replacement.BorderFactor("bar") == 3 && !save.IsEnabled,
                "a detached slider cannot undo a reset or mutate the replacement document later");
        }
        finally
        {
            elapsed.Stop();
            save.Stop();
        }
    }
}
