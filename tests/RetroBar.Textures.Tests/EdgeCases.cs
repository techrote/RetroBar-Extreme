using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RetroBar.Utilities;

internal static partial class Program
{
    private static void EdgeCases()
    {
        // A custom modern theme may expose only an overlay, without this part's Fill key.
        string overlay = StyleMap.Targets(StyleMap.Family.Modern, "buttonHover.background")
            .First(t => t.Role == StyleMap.Role.Clear).Key;
        Application.Current.Resources = new ResourceDictionary
        {
            ["TaskbarBackground"] = Brushes.Black, [overlay] = Brushes.Magenta,
        };
        var style = new StyleDocument();
        style.Set("buttonHover", "background", Texture("missing-" + Guid.NewGuid().ToString("N") + ".png"));
        var layer = new ResourceDictionary();
        var warnings = new List<string>();
        Check(StyleLayer.ApplyBrushOverrides(layer, style, StyleMap.Detect(), warnings.Add) == 0 && layer.Count == 0 && warnings.Count == 1,
            "missing texture cannot clear an overlay-only theme resource");
        Check(ReferenceEquals(Application.Current.Resources[overlay], Brushes.Magenta), "theme overlay remains untouched");
        var palette = Texture(); palette.Texture.UsePalette = true;
        style.Set("buttonHover", "background", palette); warnings.Clear();
        Check(StyleLayer.ApplyBrushOverrides(layer, style, StyleMap.Detect(), warnings.Add) == 0 && layer.Count == 0 && warnings.Count == 1,
            "invalid palette cannot clear an overlay-only resource");
        style.Set("buttonHover", "background", Texture()); warnings.Clear();
        Check(StyleLayer.ApplyBrushOverrides(layer, style, StyleMap.Detect(), warnings.Add) == 1 && ReferenceEquals(layer[overlay], Brushes.Transparent) && warnings.Count == 0,
            "valid texture retains the normal overlay-clearing contract");

        foreach (string name in new[] { "CON.png", "con.extra.png", "AUX.png", "PRN.png", "NUL.png", "COM1.png", "LPT9.png", "COM¹.png", "LPT².png", "CONIN$.png", "CONOUT$.png", "CON .png" })
        {
            Check(!TextureSpec.IsValidName(name), "reserved filename " + name);
            Throws<FormatException>(() => LutTextures.Create(new TextureSpec { Name = name }));
        }
        foreach (string name in new[] { "my-carbon.PNG", "composite.png", "steel tile.png" })
            Check(TextureSpec.IsValidName(name), "ordinary filename " + name);
        warnings.Clear();
        var duplicate = StyleDocument.Parse("{\"parts\":{\"bar\":{\"background\":{\"texture\":\"brushed\",\"texture\":\"noise\",\"lut\":[\"black\",\"white\"]}}}}", warnings.Add);
        Check(duplicate.Get("bar", "background") == null && warnings.Count == 1, "duplicate texture member is not silently last-wins");
        Check(LutTextures.Create(new TextureSpec { Scale = 8 }).Viewport.Width == 512, "maximum texture scale accepted");

        string target = Path.Combine(temporary, "junction-target"), link = Path.Combine(temporary, "junction-link");
        Directory.CreateDirectory(target);
        SavePng(Synthetic(128), Path.Combine(target, "tile.png"));
        using (var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
        {
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            Check(process.WaitForExit(10000) && process.ExitCode == 0, "create isolated junction fixture: " + output);
        }
        try
        {
            Throws<IOException>(() => LutTextures.Create(new TextureSpec { Name = "tile.png" }, texturesFolder: link));
            Throws<IOException>(() => LutTextures.CustomNames(link));
        }
        finally { Directory.Delete(link); }
    }
}
