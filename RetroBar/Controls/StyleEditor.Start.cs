using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ManagedShell.Common.Logging;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-010): an explicit, reversible Start recolour, plus palette extraction.
    public partial class StyleEditor
    {
        private void ColoursFromStart_OnClick(object sender, RoutedEventArgs e)
        {
            Color[] colours = StartButtonColourways.Extract(StyleLayer.ThemeResource,
                warning => ShellLogger.Warning($"UXUI-Extreme: {warning}"));
            if (colours.Length == 0)
            {
                Show(Text("uxui_start_no_palette"), MessageBoxImage.Information);
                return;
            }
            if (_style.Palette.Any(c => c.HasValue) && !Confirm(Text("uxui_start_palette_replace"))) return;

            cboPreset.SelectedIndex = -1;
            _base.Value = colours[0];
            _accent.Value = colours.Length > 1 ? colours[1] : Colourways.Lighter(colours[0], 0.30);
            Array.Clear(_style.Palette, 0, _style.Palette.Length);
            for (int i = 0; i < colours.Length && i < _style.Palette.Length; i++) _style.Palette[i] = colours[i];
            _palette?.ShowPalette(_style.Palette);
            SaveActive();
            // Applying a generated scheme remains the existing, separately confirmed action.
        }

        private void BuildStartPart()
        {
            bool supported = StartButtonColourways.HasImage(StyleLayer.ThemeResource);
            Color[] originals = supported ? StartButtonColourways.Extract(StyleLayer.ThemeResource) : Array.Empty<Color>();
            var note = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 400, Margin = new Thickness(2, 2, 2, 5) };
            note.SetResourceReference(TextBlock.TextProperty, supported ? "uxui_start_note" : "uxui_start_classic");
            panelProperties.Children.Add(note);

            var enabled = new CheckBox { IsChecked = _style.StartRecolour.HasValue, IsEnabled = supported, Margin = new Thickness(2) };
            enabled.SetResourceReference(ContentControl.ContentProperty, "uxui_start_enable");
            panelProperties.Children.Add(enabled);
            var field = new ColourField(_style.StartRecolour ?? (originals.Length > 0 ? originals[0] : Colors.Gray))
            { IsEnabled = supported && enabled.IsChecked == true };
            panelProperties.Children.Add(field);

            enabled.Checked += (s, e) =>
            {
                field.IsEnabled = true;
                _style.StartRecolour = field.Value;
                field.Value = _style.StartRecolour;
                ScheduleSave();
            };
            enabled.Unchecked += (s, e) =>
            {
                field.IsEnabled = false;
                _style.StartRecolour = null;
                ScheduleSave();
            };
            field.Changed += (s, e) =>
            {
                _style.StartRecolour = field.Value;
                // Do not rewrite hex text mid-keystroke: #rgb and #argb can both be prefixes
                // of #rrggbb. The model ignores alpha without interfering with editing.
                ScheduleSave();
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            foreach (bool accent in new[] { false, true })
            {
                var button = new Button { IsEnabled = supported, Style = (Style)FindResource("CompactButton") };
                button.SetResourceReference(ContentControl.ContentProperty, accent ? "uxui_start_use_accent" : "uxui_start_use_base");
                button.Click += (s, e) =>
                {
                    if ((accent ? _accent.Value : _base.Value) is Color colour)
                    {
                        _style.StartRecolour = colour;
                        SaveActive();
                        BuildPart();
                    }
                };
                row.Children.Add(button);
            }
            panelProperties.Children.Add(row);
        }
    }
}
