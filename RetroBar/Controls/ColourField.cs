using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-004, UX-009): one colour in the style editor. A swatch (opens the classic
    // Windows colour dialog), a hex box (#rrggbb / #aarrggbb / colour name), and copy / paste
    // buttons so colours can be carried between parts and the palette. Optionally empty (palette
    // slots), shown as a blank swatch and hex box.
    public class ColourField : StackPanel
    {
        // Shared by every field so picked colours stay in the dialog's custom colours row.
        private static int[] _customColors = new int[16];

        // Segoe MDL2 Assets has proper copy / paste glyphs on Windows 10 and 11; older Windows
        // gets plain letters.
        private static readonly bool HasIconFont = Fonts.SystemFontFamilies.Any(f => f.Source == "Segoe MDL2 Assets");

        private readonly Button _swatch;
        private readonly TextBox _hex;
        private readonly bool _allowEmpty;
        private Color? _value;
        private bool _updating;

        public event EventHandler Changed;

        // compact: narrower swatch and hex box, for two fields side by side.
        public ColourField(Color? value, bool allowEmpty = false, bool compact = false)
        {
            Orientation = Orientation.Horizontal;
            Margin = new Thickness(0, 1, 0, 1);
            _allowEmpty = allowEmpty;
            _value = value;

            _swatch = new Button
            {
                Width = compact ? 26 : 36,
                MinWidth = 0,
                Height = 18,
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = Application.Current.TryFindResource("uxui_style_pick_colour"),
            };
            _swatch.Click += Swatch_Click;

            _hex = new TextBox { Width = compact ? 64 : 76, VerticalContentAlignment = VerticalAlignment.Center };
            _hex.TextChanged += Hex_TextChanged;
            _hex.LostFocus += (s, e) => ShowValue();

            Children.Add(_swatch);
            Children.Add(_hex);
            Children.Add(SmallButton(HasIconFont ? "" : "C", "uxui_style_copy_colour", (s, e) => Copy()));
            Children.Add(SmallButton(HasIconFont ? "" : "V", "uxui_style_paste_colour", (s, e) => Paste()));

            ShowValue();
        }

        public Color? Value
        {
            get => _value;
            set
            {
                _value = value;
                ShowValue();
            }
        }

        private Button SmallButton(string glyph, string tooltipKey, RoutedEventHandler click)
        {
            var button = new Button
            {
                Content = glyph,
                Width = 20,
                Height = 18,
                MinWidth = 0,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 0, 0),
                ToolTip = Application.Current.TryFindResource(tooltipKey),
            };
            if (HasIconFont)
            {
                button.FontFamily = new FontFamily("Segoe MDL2 Assets");
                button.FontSize = 10;
            }
            button.Click += click;
            return button;
        }

        private void ShowValue()
        {
            _updating = true;
            _swatch.Background = _value is Color c ? new SolidColorBrush(c) : SystemColors.ControlBrush;
            _hex.Text = _value is Color v ? BrushSpec.FormatColor(v) : "";
            _hex.ClearValue(Control.BorderBrushProperty);
            _updating = false;
        }

        private void SetColour(Color? colour, bool updateText)
        {
            if (colour == _value)
            {
                return;
            }

            _value = colour;
            _swatch.Background = colour is Color c ? new SolidColorBrush(c) : SystemColors.ControlBrush;
            if (updateText)
            {
                ShowValue();
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Hex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updating)
            {
                return;
            }

            if (_allowEmpty && string.IsNullOrWhiteSpace(_hex.Text))
            {
                _hex.ClearValue(Control.BorderBrushProperty);
                SetColour(null, false);
            }
            else if (BrushSpec.TryParseColor(_hex.Text, out Color c))
            {
                _hex.ClearValue(Control.BorderBrushProperty);
                SetColour(c, false);
            }
            else
            {
                _hex.BorderBrush = Brushes.Red;
            }
        }

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            Color current = _value ?? Colors.Gray;
            using var dialog = new System.Windows.Forms.ColorDialog
            {
                AnyColor = true,
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B),
                CustomColors = _customColors,
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _customColors = dialog.CustomColors;
                // The dialog has no transparency; keep the colour's existing alpha.
                SetColour(Color.FromArgb(_value.HasValue ? current.A : (byte)255, dialog.Color.R, dialog.Color.G, dialog.Color.B), true);
            }
        }

        private void Copy()
        {
            if (_value is Color c)
            {
                try
                {
                    Clipboard.SetText(BrushSpec.FormatColor(c));
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    // Clipboard busy (another app has it open); the user can just try again.
                }
            }
        }

        private void Paste()
        {
            string text;
            try
            {
                text = Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return;
            }

            if (BrushSpec.TryParseColor(text?.Trim(), out Color c))
            {
                SetColour(c, true);
            }
            else
            {
                System.Media.SystemSounds.Beep.Play();
            }
        }
    }
}
