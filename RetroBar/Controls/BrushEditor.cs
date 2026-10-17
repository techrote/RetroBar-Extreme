using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-004): edits one style property (e.g. the task button background) in the
    // Colours tab: theme default, a colour, or a linear gradient (angle + colour stops).
    // Colours are picked with the classic Windows colour dialog or typed as #rrggbb / #aarrggbb.
    public partial class BrushEditor : StackPanel // UXUI-Extreme (UX-011): texture controls in BrushEditor.Textures.cs.
    {
        private const int MODE_THEME = 0, MODE_COLOUR = 1, MODE_GRADIENT = 2, MODE_TEXTURE = 3;
        private const int MAX_STOPS = 6;

        private readonly ComboBox _mode = new ComboBox { Width = 110, HorizontalAlignment = HorizontalAlignment.Right };
        private readonly StackPanel _details = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(14, 0, 0, 2) };
        private readonly Func<BrushSpec> _themeSeed;
        private BrushSpec _value;
        private bool _updating;

        // Raised when the user changes the brush; Value is null for the theme default.
        public event EventHandler Changed;

        public BrushEditor(string labelResourceKey, BrushSpec value, Func<BrushSpec> themeSeed, bool usedByTheme, Func<Color?[]> paletteSource = null, TexturePaletteHost texturePalette = null)
        {
            _texturePalette = texturePalette; // UXUI-Extreme: the style's texture palette, when the editor has one
            Orientation = Orientation.Vertical;
            _themeSeed = themeSeed;
            _value = value?.Clone();
            _paletteSource = paletteSource; // UXUI-Extreme (UX-011): always read the current style's palette.

            var row = new DockPanel();
            var label = new Label { VerticalAlignment = VerticalAlignment.Center, Target = _mode };
            var text = new AccessText();
            text.SetResourceReference(AccessText.TextProperty, labelResourceKey);
            label.Content = text;

            InitializeTextureModeLabels(); // UXUI-Extreme (UX-011): retain translated original modes.
            _mode.SelectionChanged += Mode_SelectionChanged;
            DockPanel.SetDock(_mode, Dock.Right);
            row.Children.Add(_mode);

            if (!usedByTheme)
            {
                var notUsed = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), FontStyle = FontStyles.Italic };
                notUsed.SetResourceReference(TextBlock.TextProperty, "uxui_style_not_used");
                DockPanel.SetDock(notUsed, Dock.Right);
                row.Children.Add(notUsed);
            }

            row.Children.Add(label);
            Children.Add(row);
            Children.Add(_details);

            _updating = true;
            _mode.SelectedIndex = CurrentMode;
            _updating = false;
            BuildDetails();
        }

        public BrushSpec Value => _value?.Clone();

        private void Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || _mode.SelectedIndex < 0)
            {
                return;
            }

            int mode = _mode.SelectedIndex;
            BrushSpec seed = _value ?? _themeSeed?.Invoke() ?? BrushSpec.Solid(Colors.Gray);

            switch (mode)
            {
                case MODE_THEME:
                    _value = null;
                    break;
                case MODE_COLOUR:
                    _value = BrushSpec.Solid(seed.IsTexture ? SeedColours(seed)[0] : seed.IsGradient ? seed.Stops.OrderBy(s => s.Offset).First().Color : seed.Color);
                    break;
                case MODE_GRADIENT:
                    _value = seed.IsGradient ? seed.Clone() : seed.IsTexture ? BrushSpec.Gradient(90, SeedColours(seed)) : BrushSpec.Gradient(90, Lighten(seed.Color, 0.25), seed.Color);
                    break;
                case MODE_TEXTURE: // UXUI-Extreme (UX-011)
                    _value = seed.IsTexture ? seed.Clone() : new BrushSpec { Texture = new TextureSpec { Lut = SeedColours(seed).ToList() } };
                    break;
            }

            BuildDetails();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void BuildDetails()
        {
            _details.Children.Clear();
            _refreshTexturePreview = null; // UXUI-Extreme (UX-011)
            _configBox = null;

            if (_value == null)
            {
                return;
            }

            if (_value.IsTexture) // UXUI-Extreme (UX-011)
            {
                BuildTextureDetails();
                return;
            }

            if (!_value.IsGradient)
            {
                _details.Children.Add(ColourRow(() => _value.Color, c => _value.Color = c, null));
                return;
            }

            // Angle
            var angleRow = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            var angleLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            angleLabel.SetResourceReference(TextBlock.TextProperty, "uxui_style_angle");
            // The slider moves in 5° steps; any angle can be typed in the box next to it.
            var angleValue = new TextBox
            {
                Width = 36,
                Margin = new Thickness(4, 0, 0, 0),
                Text = FormatAngle(_value.Angle),
                HorizontalContentAlignment = HorizontalAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var degrees = new TextBlock { Text = "°", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };
            var angle = new Slider
            {
                Minimum = 0,
                Maximum = 355,
                TickFrequency = 5,
                IsSnapToTickEnabled = true,
                Width = 120,
                Value = NormaliseAngle(_value.Angle),
                VerticalAlignment = VerticalAlignment.Center,
            };
            bool typing = false;
            var preview = new Border
            {
                Height = 16,
                BorderThickness = new Thickness(1),
                BorderBrush = SystemColors.ControlDarkDarkBrush,
                Margin = new Thickness(6, 0, 0, 0),
                Background = _value.ToBrush(),
            };
            angle.ValueChanged += (s, e) =>
            {
                if (typing)
                {
                    return;
                }

                _value.Angle = angle.Value;
                angleValue.Text = FormatAngle(angle.Value);
                angleValue.ClearValue(Control.BorderBrushProperty);
                preview.Background = _value.ToBrush();
                RaiseChanged();
            };
            angleValue.TextChanged += (s, e) =>
            {
                if (!angleValue.IsKeyboardFocusWithin)
                {
                    return;
                }

                if (double.TryParse(angleValue.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double typed) && !double.IsNaN(typed) && !double.IsInfinity(typed))
                {
                    angleValue.ClearValue(Control.BorderBrushProperty);
                    _value.Angle = NormaliseAngle(typed);
                    typing = true;
                    angle.Value = _value.Angle;
                    typing = false;
                    preview.Background = _value.ToBrush();
                    RaiseChanged();
                }
                else
                {
                    angleValue.BorderBrush = Brushes.Red;
                }
            };
            angleValue.LostFocus += (s, e) =>
            {
                angleValue.Text = FormatAngle(_value.Angle);
                angleValue.ClearValue(Control.BorderBrushProperty);
            };
            DockPanel.SetDock(angleLabel, Dock.Left);
            DockPanel.SetDock(angle, Dock.Left);
            DockPanel.SetDock(angleValue, Dock.Left);
            DockPanel.SetDock(degrees, Dock.Left);
            angleRow.Children.Add(angleLabel);
            angleRow.Children.Add(angle);
            angleRow.Children.Add(angleValue);
            angleRow.Children.Add(degrees);
            angleRow.Children.Add(preview);
            _details.Children.Add(angleRow);

            // Stops, in offset order
            _value.Stops = _value.Stops.OrderBy(s => s.Offset).ToList();
            Panel lastRow = null;
            for (int i = 0; i < _value.Stops.Count; i++)
            {
                int index = i;
                lastRow = (Panel)ColourRow(
                    () => _value.Stops[index].Color,
                    c => { _value.Stops[index].Color = c; preview.Background = _value.ToBrush(); },
                    index);
                _details.Children.Add(lastRow);
            }

            if (_value.Stops.Count < MAX_STOPS)
            {
                // UX-009: on the end of the last colour row, to save a row.
                var add = new Button { MinWidth = 0, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(6, 0, 6, 0) };
                add.SetResourceReference(ContentControl.ContentProperty, "uxui_style_add_stop");
                add.Click += (s, e) =>
                {
                    // Add a stop half way along the largest gap, coloured to match its neighbours.
                    var stops = _value.Stops;
                    int gap = 0;
                    double best = -1;
                    for (int j = 0; j + 1 < stops.Count; j++)
                    {
                        if (stops[j + 1].Offset - stops[j].Offset > best)
                        {
                            best = stops[j + 1].Offset - stops[j].Offset;
                            gap = j;
                        }
                    }

                    if (stops.Count == 1)
                    {
                        stops.Add(new GradientStop(stops[0].Color, 1));
                    }
                    else
                    {
                        Color a = stops[gap].Color, b = stops[gap + 1].Color;
                        var mid = Color.FromArgb((byte)((a.A + b.A) / 2), (byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));
                        stops.Insert(gap + 1, new GradientStop(mid, (stops[gap].Offset + stops[gap + 1].Offset) / 2));
                    }

                    BuildDetails();
                    RaiseChanged();
                };
                lastRow.Children.Add(add);
            }
        }

        // One colour: swatch (opens the colour dialog) and hex box; for gradient stops also the
        // position (0-100 %) and a remove button.
        private UIElement ColourRow(Func<Color> get, Action<Color> set, int? stopIndex)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            // UX-009: shared colour field (swatch, hex, copy, paste).
            var field = new ColourField(get());
            field.Changed += (s, e) =>
            {
                if (field.Value is Color c)
                {
                    set(c);
                    RaiseChanged();
                }
            };
            row.Children.Add(field);

            if (stopIndex is int index)
            {
                var position = new TextBox
                {
                    Width = 36,
                    Margin = new Thickness(6, 0, 0, 0),
                    Text = Math.Round(_value.Stops[index].Offset * 100).ToString(CultureInfo.CurrentCulture),
                    HorizontalContentAlignment = HorizontalAlignment.Right,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    ToolTip = FindResource("uxui_style_stop_position"),
                };
                position.TextChanged += (s, e) =>
                {
                    if (double.TryParse(position.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double percent) && percent >= 0 && percent <= 100)
                    {
                        position.ClearValue(Control.BorderBrushProperty);
                        _value.Stops[index].Offset = percent / 100;
                        RaiseChanged();
                    }
                    else
                    {
                        position.BorderBrush = Brushes.Red;
                    }
                };
                // Re-sort once the user is done typing.
                position.LostFocus += (s, e) => BuildDetails();

                row.Children.Add(position);
                row.Children.Add(new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) });

                if (_value.Stops.Count > 1)
                {
                    var remove = new Button { Content = "×", Width = 20, MinWidth = 0, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0), ToolTip = FindResource("uxui_style_remove_stop") };
                    remove.Click += (s, e) =>
                    {
                        _value.Stops.RemoveAt(index);
                        BuildDetails();
                        RaiseChanged();
                    };
                    row.Children.Add(remove);
                }
            }

            return row;
        }

        private static string FormatAngle(double angle) => Math.Round(angle, 1).ToString(CultureInfo.CurrentCulture);

        // Any typed angle, as 0 to under 360.
        private static double NormaliseAngle(double angle) => ((angle % 360) + 360) % 360;

        private static Color Lighten(Color c, double amount)
        {
            byte up(byte v) => (byte)Math.Round(v + (255 - v) * amount);
            return Color.FromArgb(c.A, up(c.R), up(c.G), up(c.B));
        }
    }
}
