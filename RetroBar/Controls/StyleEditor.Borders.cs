using System;
using System.Windows;
using System.Windows.Controls;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-013): border thickness for the selected part (None / Theme / ×2 / ×3 / ×4).
    // Borders are baked into the theme as it loads, so a change saves the style and the taskbar
    // reloads its theme (a moment's flicker), unlike colour changes which apply in place.
    public partial class StyleEditor
    {
        private void AddBorderRow(string part)
        {
            if (Array.IndexOf(StyleDocument.BorderParts, part) < 0)
            {
                return; // e.g. mouse-over buttons share the task button's borders; 3D edges have none
            }

            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 2) };
            // A slider from 0 (none) through 1 (the theme's own) up to 16 times. The theme is rebuilt
            // with the new borders, so the change is applied a moment after the slider stops.
            var combo = new Slider
            {
                Minimum = 0, Maximum = StyleDocument.MaxBorderFactor, TickFrequency = 1, IsSnapToTickEnabled = true,
                Width = 170, Value = Math.Clamp(_style.BorderFactor(part), 0, StyleDocument.MaxBorderFactor),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right,
            };
            var shown = new TextBlock { Width = 62, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            Func<double, string> describe = value => value < 0.5 ? (TryFindResource("uxui_border_none") as string ?? "None") : value < 1.5 ? (TryFindResource("uxui_border_theme") as string ?? "Theme") : "×" + Math.Round(value);
            shown.Text = describe(combo.Value);
            var settle = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            settle.Tick += (s, e) =>
            {
                settle.Stop();
                int factor = (int)Math.Round(combo.Value);
                if (factor != (int)_style.BorderFactor(part))
                {
                    _style.SetBorderFactor(part, factor);
                    SaveActive(); // the taskbar reloads its theme with the new borders
                }
            };
            combo.ValueChanged += (s, e) =>
            {
                shown.Text = describe(combo.Value);
                settle.Stop();
                settle.Start();
            };
            DockPanel.SetDock(shown, Dock.Right);
            row.Children.Add(shown);
            DockPanel.SetDock(combo, Dock.Right);

            var label = new Label { VerticalAlignment = VerticalAlignment.Center, Target = combo };
            var text = new AccessText();
            text.SetResourceReference(AccessText.TextProperty, "uxui_border_thickness");
            label.Content = text;

            row.Children.Add(combo);
            row.Children.Add(label);
            panelProperties.Children.Add(row);

            // Classic Start buttons are drawn only with their bevel, on the bar's own colour.
            if (part == "start" && !StartButtonColourways.HasImage(StyleLayer.ThemeResource))
            {
                var note = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 400, Margin = new Thickness(4, 0, 4, 2) };
                note.SetResourceReference(TextBlock.TextProperty, "uxui_border_start_classic");
                panelProperties.Children.Add(note);
            }
        }
    }
}
