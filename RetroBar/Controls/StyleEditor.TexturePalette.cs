using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the texture palette in the Colours tab. Twelve slots, each a saved texture shown
    // as a swatch. Selecting one shows its config text, which can be copied, pasted over, or cleared.
    public partial class StyleEditor
    {
        private int _textureSlot;

        private void ShowTextureSlots()
        {
            if (panelTextureSlots == null)
            {
                return;
            }

            panelTextureSlots.Children.Clear();
            for (int i = 0; i < StyleDocument.TEXTURE_PALETTE_SIZE; i++)
            {
                int slot = i;
                TextureSpec texture = _style.TexturePalette[i];
                var swatch = new Border
                {
                    Width = 30, Height = 20, BorderThickness = new Thickness(1),
                    BorderBrush = SystemColors.ControlDarkBrush,
                    Background = SwatchBrush(texture),
                };
                if (texture == null)
                {
                    swatch.Child = new TextBlock { Text = (i + 1).ToString(), HorizontalAlignment = HorizontalAlignment.Center, Foreground = SystemColors.GrayTextBrush };
                }
                var button = new Button
                {
                    Content = swatch, Padding = new Thickness(1), Margin = new Thickness(0, 0, 3, 3), MinWidth = 0, MinHeight = 0,
                    BorderThickness = new Thickness(slot == _textureSlot ? 2 : 1),
                    BorderBrush = slot == _textureSlot ? SystemColors.HighlightBrush : SystemColors.ControlDarkBrush,
                    ToolTip = texture == null ? string.Format(Text("uxui_texpal_empty_tip"), i + 1) : $"{i + 1}: {texture.Name}",
                };
                button.Click += (s, e) =>
                {
                    _textureSlot = slot;
                    ShowTextureSlots();
                };
                panelTextureSlots.Children.Add(button);
            }

            txtTextureSlotConfig.Text = _style.TexturePalette[_textureSlot] == null ? "" : TextureConfig.ToText(_style.TexturePalette[_textureSlot]);
            txtTextureSlotStatus.Text = string.Format(Text("uxui_texpal_selected"), _textureSlot + 1);
        }

        private Brush SwatchBrush(TextureSpec texture)
        {
            if (texture == null)
            {
                return Brushes.Transparent;
            }

            try
            {
                return new BrushSpec { Texture = texture }.ToBrush(palette: _style.Palette);
            }
            catch (Exception)
            {
                return Brushes.Transparent; // e.g. a palette texture while the palette is nearly empty
            }
        }

        private void SetTextureSlot(TextureSpec texture)
        {
            _style.TexturePalette[_textureSlot] = texture;
            ShowTextureSlots();
            ScheduleSave();
        }

        private void ApplyTextureSlotText()
        {
            string text = txtTextureSlotConfig.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetTextureSlot(null);
                return;
            }

            try
            {
                TextureSpec parsed = TextureConfig.FromText(text, new TextureSpec());
                parsed.Label = "";
                SetTextureSlot(parsed);
            }
            catch (FormatException e)
            {
                txtTextureSlotStatus.Text = Text("uxui_texture_config_failed") + " " + e.Message;
            }
        }

        private void TextureSlotConfig_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return)
            {
                ApplyTextureSlotText();
                e.Handled = true;
            }
        }

        private void TextureSlotCopy_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_style.TexturePalette[_textureSlot] != null)
                {
                    Clipboard.SetText(TextureConfig.ToText(_style.TexturePalette[_textureSlot]));
                    txtTextureSlotStatus.Text = Text("uxui_texture_config_copied");
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                txtTextureSlotStatus.Text = Text("uxui_texture_config_clipboard");
            }
        }

        private void TextureSlotPaste_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    txtTextureSlotStatus.Text = Text("uxui_texture_config_clipboard");
                    return;
                }
                txtTextureSlotConfig.Text = Clipboard.GetText();
                ApplyTextureSlotText();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                txtTextureSlotStatus.Text = Text("uxui_texture_config_clipboard");
            }
        }

        private void TextureSlotClear_OnClick(object sender, RoutedEventArgs e) => SetTextureSlot(null);
    }
}
