using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RetroBar.Controls;
using RetroBar.Utilities;

internal static partial class Program
{
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
            if (child is DependencyObject d)
            {
                if (d is T match) yield return match;
                foreach (T nested in Descendants<T>(d)) yield return nested;
            }
    }
    private static T Named<T>(DependencyObject root, string name) where T : FrameworkElement => Descendants<T>(root).Single(x => x.Name == name);
    private static void Click(DependencyObject root, string text) => Descendants<Button>(root).Single(b => b.Content as string == text).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static void Editor()
    {
        foreach (string path in new[] { "Controls/StartColourwaysStrings.xaml", "Controls/LayoutStrings.xaml", "Languages/English.xaml", "Themes/System.xaml" })
            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/RetroBar;component/" + path, UriKind.Relative) });
        var seed = BrushSpec.Gradient(90, Colors.Indigo, Colors.Cyan);
        Color?[] palette = { Colors.Black, Colors.Red };
        var original = Texture("carbon");
        var editor = new BrushEditor("uxui_style_prop_background", original, () => seed, true, () => palette);
        _ = new Window { Content = editor, ShowInTaskbar = false }; // Never shown.
        var mode = (ComboBox)typeof(BrushEditor).GetField("_mode", PrivateInstance).GetValue(editor);
        int changes = 0; editor.Changed += (s, e) => changes++;
        DrainDispatcher();
        Check(mode.Items.Count == 4 && mode.SelectedIndex == 3 && changes == 0, "construction keeps texture and does not write");
        Check(Named<Border>(editor, "TexturePreview").Background is ImageBrush, "live texture preview");
        Click(editor, "Theme scanlines");
        Check(editor.Value.Texture.Name == "scanlines" && editor.Value.Texture.Lut.SequenceEqual(seed.Stops.Select(s => s.Color)), "use actual current theme colours, not a guessed CyberGarish palette");
        Click(editor, "Ratrod brushed");
        Check(editor.Value.Texture.Name == "brushed" && editor.Value.Texture.Lut.Count == 3, "Ratrod shortcut");
        while (editor.Value.Texture.Lut.Count < 6) Click(editor, "Add LUT colour");
        Check(!Descendants<Button>(editor).Any(b => b.Content as string == "Add LUT colour"), "six-stop upper bound");
        while (editor.Value.Texture.Lut.Count > 2)
            Descendants<Button>(editor).First(b => b.Content as string == "×").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(!Descendants<Button>(editor).Any(b => b.Content as string == "×"), "two-stop lower bound");
        var colour = Descendants<ColourField>(editor).First();
        var hex = colour.Children.OfType<TextBox>().Single();
        hex.Text = "#123456";
        Check(editor.Value.Texture.Lut[0] == Color.FromRgb(0x12, 0x34, 0x56), "shared colour field edits LUT");
        Check(original.Texture.Name == "carbon" && original.Texture.Lut[0] == Colors.Black, "editor never mutates incoming model");

        var scale = Named<TextBox>(editor, "TextureScale");
        int before = changes;
        scale.Text = "NaN"; scale.Text = "-1"; scale.Text = "8.01";
        Check(changes == before && editor.Value.Texture.Scale == 1, "invalid scale not saved");
        CultureInfo old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            scale.Text = "1,25";
            Check(editor.Value.Texture.Scale == 1.25, "locale-aware fractional scale input");
        }
        finally { CultureInfo.CurrentCulture = old; }
        Named<TextBox>(editor, "TextureContrast").Text = "0";
        Check(editor.Value.Texture.Contrast == 0, "contrast zero accepted");
        Named<TextBox>(editor, "TextureContrast").Text = "1";
        var usePalette = Named<CheckBox>(editor, "TexturePalette");
        usePalette.IsChecked = true; usePalette.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(editor.Value.Texture.UsePalette, "palette mode checkbox");
        var first = (BitmapSource)((ImageBrush)Named<Border>(editor, "TexturePreview").Background).ImageSource;
        before = changes;
        palette = new Color?[] { Colors.Black, Colors.Blue }; editor.RefreshTexturePreview();
        var second = (BitmapSource)((ImageBrush)Named<Border>(editor, "TexturePreview").Background).ImageSource;
        Check(!Bytes(first).SequenceEqual(Bytes(second)) && changes == before, "palette callback refreshes without spurious writes");
        palette = new Color?[] { Colors.Red }; editor.RefreshTexturePreview();
        Check(Named<Border>(editor, "TexturePreview").Background == null && editor.Value.Texture.UsePalette, "unusable palette shows fallback without dropping setting");
        palette = new Color?[] { Colors.Black, Colors.Blue }; editor.RefreshTexturePreview();
        Check(Named<Border>(editor, "TexturePreview").Background is ImageBrush, "palette recovery");
        before = changes;
        Application.Current.Resources["uxui_style_mode_values"] = new[] { "Default translated", "Colour translated", "Gradient translated" };
        DrainDispatcher();
        Check(mode.SelectedIndex == 3 && mode.Items[0] as string == "Default translated" && changes == before, "language resource reload retains texture without editing");
        mode.SelectedIndex = 2;
        Check(editor.Value.IsGradient && editor.Value.Stops.First().Color == Colors.Black && editor.Value.Stops.Last().Color == Colors.Blue, "palette texture to gradient retains colours");
        mode.SelectedIndex = 3; Check(editor.Value.IsTexture, "gradient to texture");
        mode.SelectedIndex = 1; Check(!editor.Value.IsGradient && !editor.Value.IsTexture, "texture to solid");
        mode.SelectedIndex = 0; Check(editor.Value == null, "theme default removes only this override");
        mode.SelectedIndex = 3; Check(editor.Value.Texture.Lut.SequenceEqual(seed.Stops.Select(s => s.Color)), "theme default to texture uses theme seed");

        // Verify the actual parent editor supplies the current document's palette and
        // persists child changes in memory. Stop its timer before pumping any messages.
        var parent = new StyleEditor();
        _ = new Window { Content = parent, ShowInTaskbar = false };
        var style = new StyleDocument();
        style.Palette[0] = Colors.Black; style.Palette[1] = Colors.Red;
        var spec = Texture("scanlines"); spec.Texture.UsePalette = true; style.Set("bar", "background", spec);
        var field = typeof(StyleEditor).GetField("_style", PrivateInstance); field.SetValue(parent, style);
        var timer = (DispatcherTimer)typeof(StyleEditor).GetField("_saveTimer", PrivateInstance).GetValue(parent);
        try
        {
            ((ComboBox)parent.FindName("cboPart")).SelectedIndex = 0;
            typeof(StyleEditor).GetMethod("BuildPart", PrivateInstance).Invoke(parent, null);
            var child = ((StackPanel)parent.FindName("panelProperties")).Children.OfType<BrushEditor>().First();
            Named<TextBox>(child, "TextureScale").Text = "1.5";
            Check(style.Get("bar", "background").Texture.Scale == 1.5, "child changes reach active model");
            timer.Stop();
            var oldImage = ((ImageBrush)Named<Border>(child, "TexturePreview").Background).ImageSource;
            var replacement = style.Clone(); replacement.Palette[1] = Colors.Blue; field.SetValue(parent, replacement);
            child.RefreshTexturePreview();
            Check(!ReferenceEquals(oldImage, ((ImageBrush)Named<Border>(child, "TexturePreview").Background).ImageSource), "parent callback reads current document, not captured stale palette");
        }
        finally { timer.Stop(); }
    }
}
