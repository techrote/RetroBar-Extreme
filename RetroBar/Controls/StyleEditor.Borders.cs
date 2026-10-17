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
            var combo = new ComboBox { Width = 110, HorizontalAlignment = HorizontalAlignment.Right };
            combo.SetResourceReference(ItemsControl.ItemsSourceProperty, "uxui_border_thickness_values");
            combo.SelectedIndex = (int)Math.Clamp(_style.BorderFactor(part), 0, StyleDocument.MaxBorderFactor);
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex < 0)
                {
                    combo.SelectedIndex = (int)_style.BorderFactor(part); // item list reloaded (language change)
                    return;
                }

                if (combo.SelectedIndex != (int)_style.BorderFactor(part))
                {
                    _style.SetBorderFactor(part, combo.SelectedIndex);
                    SaveActive(); // the taskbar reloads its theme with the new borders
                }
            };
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
