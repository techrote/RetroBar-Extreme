using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-009, reworked after owner feedback): the palette lives in the Colours tab
    // instead of a separate window. Its 12 slots carry colours between parts (copy / paste),
    // feed palette-coloured textures (UX-011), and drive the colourway: slot 1 is the base
    // colour and slot 2 the accent. Presets and "Colours from Start button" fill the palette;
    // "Apply to parts" generates matching buttons, tray, clock and 3D edges from slots 1 and 2.
    public partial class StyleEditor
    {
        private const int PaletteColumns = 3;
        private bool _showingPalette;

        private void BuildPaletteGrid()
        {
            int rows = (StyleDocument.PALETTE_SIZE + PaletteColumns - 1) / PaletteColumns;
            for (int c = 0; c < PaletteColumns; c++)
            {
                gridPalette.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            for (int r = 0; r < rows; r++)
            {
                gridPalette.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            for (int i = 0; i < StyleDocument.PALETTE_SIZE; i++)
            {
                int slot = i;
                var field = new ColourField(null, allowEmpty: true, compact: true)
                {
                    Margin = new Thickness(0, 1, i % PaletteColumns == PaletteColumns - 1 ? 0 : 8, 1),
                    ToolTip = SlotTip(slot),
                };
                field.Changed += (s, e) =>
                {
                    if (_showingPalette)
                    {
                        return;
                    }

                    _style.Palette[slot] = field.Value;
                    // UXUI-Extreme (UX-011): palette textures follow, without rebuilding focused controls.
                    RefreshTexturePreviews();
                    ScheduleSave();
                };
                Grid.SetColumn(field, i % PaletteColumns);
                Grid.SetRow(field, i / PaletteColumns);
                gridPalette.Children.Add(field);
                _paletteFields[i] = field;
            }
        }

        private object SlotTip(int slot)
        {
            string key = slot == 0 ? "uxui_palette_slot_base" : slot == 1 ? "uxui_palette_slot_accent" : "uxui_palette_slot";
            return string.Format(TryFindResource(key) as string ?? "{0}", slot + 1);
        }

        private void ShowPaletteFields()
        {
            _showingPalette = true;
            for (int i = 0; i < _paletteFields.Length; i++)
            {
                _paletteFields[i].Value = _style.Palette[i];
            }
            _showingPalette = false;
        }

        private void RefreshTexturePreviews()
        {
            foreach (object child in panelProperties.Children)
            {
                if (child is BrushEditor editor)
                {
                    editor.RefreshTexturePreview();
                }
            }
        }

        // Base and accent for the colourway and the Start button: palette slots 1 and 2.
        private Color? PaletteBase => _style.Palette[0];
        private Color? PaletteAccent => _style.Palette[1] ?? (PaletteBase is Color b ? Colourways.Lighter(b, 0.30) : (Color?)null);

        // A preset puts its base and accent into slots 1 and 2; the other slots are left alone.
        private void CboPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboPreset.SelectedIndex < 0)
            {
                return;
            }

            Colourways.Preset preset = Colourways.Presets[cboPreset.SelectedIndex];
            _style.Palette[0] = preset.Base;
            _style.Palette[1] = preset.Accent;
            ShowPaletteFields();
            RefreshTexturePreviews();
            ScheduleSave();
        }

        private void ApplyColourway_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(PaletteBase is Color baseColour) || !(PaletteAccent is Color accent))
            {
                Show(Text("uxui_palette_need_base"), MessageBoxImage.Information);
                return;
            }

            if (Colourways.GeneratedParts.Any(_style.Parts.ContainsKey) && !Confirm(Text("uxui_colourway_replace_confirm")))
            {
                return;
            }

            Colourways.Apply(_style, baseColour, accent);
            SaveActive();
            BuildPart();
        }

        private void ClearPalette_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_style.Palette.Any(c => c.HasValue) || !Confirm(Text("uxui_palette_clear_confirm")))
            {
                return;
            }

            Array.Clear(_style.Palette, 0, _style.Palette.Length);
            cboPreset.SelectedIndex = -1;
            ShowPaletteFields();
            RefreshTexturePreviews();
            ScheduleSave();
        }
    }
}
