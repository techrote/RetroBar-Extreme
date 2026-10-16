using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

internal static partial class Program
{
    private static void Documents()
    {
        var style = new StyleDocument { Name = "Texture regression", StartRecolour = Colors.Orange };
        var bar = Texture(); bar.Texture.Lut = new List<Color> { Colors.Black, Colors.SlateGray, Colors.White };
        style.Set("bar", "background", bar);
        style.Set("clock", "foreground", BrushSpec.Solid(Colors.Cyan));
        style.Set("button", "background", BrushSpec.Gradient(45, Colors.Navy, Colors.Teal));
        style.Palette[3] = Colors.Red; style.Palette[11] = Colors.Blue;
        var copy = style.Clone();
        copy.Get("bar", "background").Texture.Lut[0] = Colors.Green;
        copy.Palette[3] = Colors.Yellow;
        Check(bar.Texture.Lut[0] == Colors.Black && style.Palette[3] == Colors.Red, "deep clone independence");
        var restored = Parse(style.ToJson());
        Check(restored.Get("bar", "background").IsTexture && !restored.Get("bar", "background").IsGradient, "separate texture kind");
        Check(restored.Get("bar", "background").Texture.Lut.SequenceEqual(bar.Texture.Lut), "LUT round trip");
        Check(restored.Name == style.Name && restored.StartRecolour == style.StartRecolour && restored.Palette[11] == Colors.Blue, "metadata round trip");
        Check(restored.Get("clock", "foreground").Color == Colors.Cyan && restored.Get("button", "background").IsGradient, "legacy brushes retained");
        Colourways.Apply(restored, Colors.Olive, Colors.Orange);
        Check(restored.Get("bar", "background").IsTexture && restored.StartRecolour == style.StartRecolour, "UX-009 generator isolation");
        string path = Path.Combine(temporary, "style.json");
        restored.Save(path);
        Check(StyleDocument.Load(path, s => throw new Exception(s)).Get("bar", "background").IsTexture, "disk save/load");
        restored.Set("bar", "background", null);
        Check(restored.Get("bar", "background") == null && restored.Get("button", "background") != null, "part reset preserves neighbours");
        var only = new StyleDocument(); only.Set("bar", "background", Texture());
        Check(!only.IsEmpty, "texture-only style not empty");
        only.Set("bar", "background", null); Check(only.IsEmpty, "reset texture-only style");
        Check(BrushSpec.FromBrush(BrushSpec.Gradient(135, Colors.Red, Colors.Blue).ToBrush()).Angle == 135, "gradient seed keeps both axes");
        Check(Parse("{\"parts\":{\"bar\":{\"background\":{\"angle\":90,\"stops\":[{\"color\":\"red\",\"offset\":0.2},\"blue\"]}}}}").Get("bar", "background").Stops[0].Offset == 0.2, "legacy gradient offsets");

        foreach (int count in Enumerable.Range(2, 5))
        {
            var spec = Texture(); spec.Texture.Lut = Enumerable.Range(0, count).Select(i => Color.FromRgb((byte)(i * 20), 4, 8)).ToList();
            var doc = new StyleDocument(); doc.Set("bar", "background", spec);
            Check(Parse(doc.ToJson()).Get("bar", "background").Texture.Lut.Count == count, "explicit LUT count " + count);
        }
        var paletteTexture = Texture(); paletteTexture.Texture.UsePalette = true;
        var palette = new Color?[12]; palette[4] = Colors.Red; palette[11] = Colors.Blue;
        Check(paletteTexture.Texture.ResolveLut(palette).SequenceEqual(new[] { Colors.Red, Colors.Blue }), "sparse palette uses slot order");
        for (int i = 0; i < 12; i++) palette[i] = Color.FromRgb((byte)(i * 20), 9, 10);
        Check(paletteTexture.Texture.ResolveLut(palette).Length == 12, "palette does not truncate at six");
        style.Set("bar", "background", paletteTexture);
        Check(Parse(style.ToJson()).Get("bar", "background").Texture.UsePalette, "palette reference stays live on disk");
        Throws<FormatException>(() => paletteTexture.Texture.ResolveLut(new Color?[] { Colors.Red, null }));
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0, 0.24, 8.01 })
        {
            var spec = new TextureSpec { Scale = bad };
            Throws<FormatException>(() => spec.Validate());
        }
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, -0.01, 4.01 })
            Throws<FormatException>(() => new TextureSpec { Contrast = bad }.Validate());
        foreach (string name in new[] { "../x.png", "..\\x.png", "C:\\x.png", "https://example.com/x.png", "x.png:stream", "x.jpg", "", ".png", "x.png " })
            Check(!TextureSpec.IsValidName(name), "reject path or non-PNG " + name);
        foreach (string bad in new[]
        {
            "{\"texture\":\"brushed\"}",
            "{\"texture\":\"unknown\",\"lut\":[\"red\",\"blue\"]}",
            "{\"texture\":false,\"lut\":[\"red\",\"blue\"]}",
            "{\"texture\":\"brushed\",\"lut\":[\"red\"]}",
            "{\"texture\":\"brushed\",\"lut\":[\"red\",false]}",
            "{\"texture\":\"brushed\",\"lut\":[\"red\",\"not a colour\"]}",
            "{\"texture\":\"brushed\",\"lut\":[\"red\",\"blue\"],\"scale\":1e999}",
            "{\"texture\":\"brushed\",\"lut\":\"palette\",\"contrast\":\"1\"}",
            "{\"texture\":\"brushed\",\"lut\":\"palette\",\"stops\":[\"red\",\"blue\"]}",
            "{\"texture\":\"brushed\",\"lut\":[\"red\",\"red\",\"red\",\"red\",\"red\",\"red\",\"red\"]}",
        })
        {
            var warnings = new List<string>();
            var parsed = StyleDocument.Parse("{\"parts\":{\"bar\":{\"background\":" + bad + "},\"clock\":{\"foreground\":\"cyan\"}}}", warnings.Add);
            Check(parsed.Get("bar", "background") == null && parsed.Get("clock", "foreground").Color == Colors.Cyan && warnings.Count == 1, "bad property isolated: " + bad);
        }
    }

    private static void Resources()
    {
        foreach (StyleMap.Family family in Enum.GetValues<StyleMap.Family>())
        {
            var theme = new ResourceDictionary();
            foreach (var part in StyleMap.PartProperties)
                foreach (string property in part.Value)
                    foreach (var target in StyleMap.Targets(family, part.Key + "." + property)) theme[target.Key] = Brushes.Magenta;
            theme["TaskButton"] = new Style(typeof(Button));
            Application.Current.Resources = theme;
            var style = new StyleDocument();
            foreach (var part in StyleMap.PartProperties)
                foreach (string property in part.Value) style.Set(part.Key, property, Texture("scanlines"));
            var warnings = new List<string>();
            var layer = new ResourceDictionary();
            Check(StyleLayer.ApplyBrushOverrides(layer, style, family, warnings.Add) > 0 && warnings.Count == 0, family + " all slots");
            foreach (object key in layer.Keys)
            {
                Check(layer[key] is ImageBrush || layer[key] is SolidColorBrush || layer[key] is Style, family + " valid override " + key);
                if (layer[key] is ImageBrush brush) Check(brush.IsFrozen && brush.TileMode == TileMode.Tile, "frozen tiled brush " + key);
            }
            if (family != StyleMap.Family.Modern)
                Check(((Style)layer["TaskButton"]).Setters.OfType<Setter>().Any(s => s.Property == Control.BackgroundProperty && s.Value is ImageBrush), "classic button style background");
            foreach (var target in StyleMap.Targets(family, "bar.background").Where(t => t.Role == StyleMap.Role.FillVertical))
                Check(((ImageBrush)layer[target.Key]).Transform is RotateTransform r && r.Angle == -90, "vertical orientation " + target.Key);

            var mixed = new StyleDocument();
            mixed.Set("bar", "background", Texture("missing-" + Guid.NewGuid().ToString("N") + ".png"));
            mixed.Set("clock", "foreground", BrushSpec.Solid(Colors.Cyan));
            layer = new ResourceDictionary(); warnings.Clear();
            StyleLayer.ApplyBrushOverrides(layer, mixed, family, warnings.Add);
            Check(warnings.Count == 1 && warnings[0].Contains("bar.background"), family + " failure names the property");
            foreach (var target in StyleMap.Targets(family, "bar.background"))
                Check(!layer.Contains(target.Key), family + " no partial fill or Clear keys after failure");
            foreach (var target in StyleMap.Targets(family, "clock.foreground"))
                Check(layer[target.Key] is SolidColorBrush b && b.Color == Colors.Cyan, family + " unrelated clock retained");
            mixed.Set("bar", "background", null);
            layer = new ResourceDictionary(); warnings.Clear();
            StyleLayer.ApplyBrushOverrides(layer, mixed, family, warnings.Add);
            Check(warnings.Count == 0, "reset removes failure");
        }
    }
}
