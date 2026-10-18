using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-011): texture UI is isolated from the existing colour/gradient editor.
    public partial class BrushEditor
    {
        private readonly Func<Color?[]> _paletteSource;
        private Action _refreshTexturePreview;
        private static readonly DependencyProperty ModeLabelsProperty = DependencyProperty.Register(
            "ModeLabels", typeof(Array), typeof(BrushEditor), new PropertyMetadata(null, (d, e) =>
                ((BrushEditor)d).SetModeLabels(e.NewValue as Array)));

        private int CurrentMode => _value == null ? MODE_THEME : _value.IsTexture ? MODE_TEXTURE :
            _value.IsGradient ? MODE_GRADIENT : MODE_COLOUR;

        private string TextureText(string key) => TryFindResource(key) as string ?? key;

        private void InitializeTextureModeLabels()
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/RetroBar;component/Controls/TextureStrings.xaml", UriKind.Relative),
            });
            SetResourceReference(ModeLabelsProperty, "uxui_style_mode_values");
            if (_mode.Items.Count == 0) SetModeLabels(null);
        }

        private void SetModeLabels(Array labels)
        {
            // Keep all three existing translated labels. A language-resource reload must not
            // fire an edit or silently turn a texture into a solid colour.
            bool wasUpdating = _updating;
            _updating = true;
            _mode.ItemsSource = (labels ?? new[] { "Theme default", "Colour", "Gradient" }).Cast<object>()
                .Take(3).Concat(new object[] { TextureText("uxui_texture_mode") }).ToArray();
            _mode.SelectedIndex = CurrentMode;
            _updating = wasUpdating;
        }

        private Color[] SeedColours(BrushSpec seed)
        {
            Color[] colours;
            if (seed.IsTexture)
            {
                try { colours = seed.Texture.ResolveLut(_paletteSource?.Invoke()); }
                catch (FormatException) { colours = seed.Texture.Lut?.ToArray() ?? new[] { Colors.Gray }; }
            }
            else if (seed.IsGradient) colours = seed.Stops.OrderBy(s => s.Offset).Select(s => s.Color).ToArray();
            else colours = new[] { Lighten(seed.Color, 0.25), seed.Color };
            if (colours.Length == 0) colours = new[] { Colors.Gray };
            if (colours.Length == 1) return new[] { colours[0], colours[0] };
            if (colours.Length <= MAX_STOPS) return colours;
            // Explicit LUTs are bounded; preserve both endpoints when seeding from a 12-slot
            // palette or a hand-written gradient with more than six stops.
            return Enumerable.Range(0, MAX_STOPS)
                .Select(i => colours[(int)Math.Round(i * (colours.Length - 1.0) / (MAX_STOPS - 1))]).ToArray();
        }

        public void RefreshTexturePreview() => _refreshTexturePreview?.Invoke();

        private void BuildTextureDetails()
        {
            TextureSpec texture = _value.Texture;
            var pattern = new ComboBox { Width = 150, HorizontalAlignment = HorizontalAlignment.Left, Name = "TexturePattern" };
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 520, Margin = new Thickness(0, 1, 0, 0), Visibility = Visibility.Collapsed };
            var preview = new Border
            {
                Height = 26, Margin = new Thickness(0, 2, 0, 2), BorderThickness = new Thickness(1),
                BorderBrush = SystemColors.ControlDarkDarkBrush, Name = "TexturePreview",
            };
            Action<string> say = text =>
            {
                status.Text = text;
                status.Foreground = SystemColors.ControlTextBrush;
                status.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
            };
            _refreshTexturePreview = () =>
            {
                try
                {
                    preview.Background = _value.ToBrush(palette: _paletteSource?.Invoke());
                    if (_configBox != null && !_configBox.IsKeyboardFocusWithin) _configBox.Text = TextureConfig.ToText(_value.Texture);
                    preview.ToolTip = TextureText("uxui_texture_hint"); // the standing note lives in a tooltip, not a paragraph
                    say("");
                }
                catch (Exception e)
                {
                    preview.Background = null;
                    status.Text = TextureText("uxui_texture_fallback") + " " + e.Message;
                    status.Foreground = Brushes.Firebrick;
                    status.Visibility = Visibility.Visible;
                }
            };
            var choices = LutTextures.BuiltInNames.ToList();
            try { choices.AddRange(LutTextures.CustomNames()); }
            catch (Exception) { /* Preview reports unreadable selected assets; built-ins remain usable. */ }
            if (!choices.Contains(texture.Name)) choices.Add(texture.Name);
            pattern.ItemsSource = choices;
            pattern.SelectedItem = texture.Name;
            pattern.SelectionChanged += (s, e) =>
            {
                if (!(pattern.SelectedItem is string name)) return;
                texture.Name = name;
                RefreshTexturePreview();
                RaiseChanged();
                Dispatcher.BeginInvoke(new Action(BuildDetails)); // parameter rows differ per pattern
            };
            var patternRow = new DockPanel();
            var label = new Label { Content = TextureText("uxui_texture_pattern"), Target = pattern };
            DockPanel.SetDock(label, Dock.Left);
            patternRow.Children.Add(label);
            patternRow.Children.Add(pattern);
            _details.Children.Add(patternRow);
            _details.Children.Add(PresetRow(texture, say)); // UXUI-Extreme: presets, import / export

            var presets = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
            var ratrod = new Button { Content = TextureText("uxui_texture_ratrod"), Margin = new Thickness(0, 0, 4, 0) };
            ratrod.Click += (s, e) =>
            {
                _value.Texture = new TextureSpec { Name = "brushed", Lut = new[] { "#2b2f33", "#5a6066", "#8a9096" }.Select(BrushSpec.ParseColor).ToList() };
                BuildDetails(); RaiseChanged();
            };
            var theme = new Button { Content = TextureText("uxui_texture_theme"), IsEnabled = _themeSeed?.Invoke() != null };
            theme.Click += (s, e) =>
            {
                _value.Texture = new TextureSpec { Name = "scanlines", Lut = SeedColours(_themeSeed?.Invoke() ?? BrushSpec.Solid(Colors.Gray)).ToList() };
                BuildDetails(); RaiseChanged();
            };
            presets.Children.Add(ratrod); presets.Children.Add(theme);

            var palette = new CheckBox
            {
                Content = TextureText("uxui_texture_palette"), IsChecked = texture.UsePalette,
                Margin = new Thickness(0, 2, 0, 2), Name = "TexturePalette",
            };
            palette.Click += (s, e) =>
            {
                texture.UsePalette = palette.IsChecked == true;
                if (!texture.UsePalette && (texture.Lut == null || texture.Lut.Count < 2))
                    texture.Lut = new[] { Colors.Black, Colors.White }.ToList();
                BuildDetails(); RaiseChanged();
            };
            // "Use palette" and the two starting points share a row.
            presets.Margin = new Thickness(0);
            DockPanel.SetDock(presets, Dock.Right);
            var paletteRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            paletteRow.Children.Add(presets);
            paletteRow.Children.Add(palette);
            _details.Children.Add(paletteRow);
            if (!texture.UsePalette)
            {
                var lutGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
                for (int i = 0; i < texture.Lut.Count; i++)
                {
                    int index = i;
                    var row = (StackPanel)ColourRow(() => texture.Lut[index], c =>
                    {
                        texture.Lut[index] = c; RefreshTexturePreview();
                    }, null);
                    if (texture.Lut.Count > 2)
                    {
                        var remove = new Button { Content = "×", Width = 20, MinWidth = 0, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0), ToolTip = TextureText("uxui_texture_remove") };
                        remove.Click += (s, e) => { texture.Lut.RemoveAt(index); BuildDetails(); RaiseChanged(); };
                        row.Children.Add(remove);
                    }
                    lutGrid.Children.Add(row);
                }
                _details.Children.Add(lutGrid);
                if (texture.Lut.Count < MAX_STOPS)
                {
                    var add = new Button { Content = TextureText("uxui_texture_add"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2) };
                    add.Click += (s, e) =>
                    {
                        int last = texture.Lut.Count - 1;
                        texture.Lut.Insert(last, LutTextures.Sample(new[] { texture.Lut[last - 1], texture.Lut[last] }, 0.5));
                        BuildDetails(); RaiseChanged();
                    };
                    _details.Children.Add(add);
                }
            }
            // UXUI-Extreme: every pattern shows the same controls in the same cells (generator-only
            // ones leave a gap when the pattern doesn't use them); rows with nothing in them collapse.
            var cells = new UIElement[15];
            TextureGenerators.Param uses = TextureGenerators.Uses(texture.Name);
            if (uses.HasFlag(TextureGenerators.Param.Seed))
                cells[0] = TextureNumber("uxui_texture_seed", "TextureSeed", 0, TextureSpec.MaxSeed, 1,
                    () => texture.Seed, n => texture.Seed = (int)Math.Round(n));
            if (uses.HasFlag(TextureGenerators.Param.Frequency))
                cells[1] = TextureNumber("uxui_texture_frequency", "TextureFrequency", 1, texture.Name == "noise" ? 64 : 32, 1,
                    () => TextureGenerators.Frequency(texture), n => texture.Frequency = (int)Math.Round(n));
            if (uses.HasFlag(TextureGenerators.Param.Octaves))
                cells[2] = TextureNumber("uxui_texture_octaves", "TextureOctaves", 1, TextureSpec.MaxOctaves, 1,
                    () => TextureGenerators.Octaves(texture), n => texture.Octaves = (int)Math.Round(n));
            if (uses.HasFlag(TextureGenerators.Param.Strength))
                cells[3] = TextureNumber("uxui_texture_strength", "TextureStrength", TextureSpec.MinStrength, TextureSpec.MaxStrength, 0.1,
                    () => texture.Strength, n => texture.Strength = n);
            cells[4] = TextureNumber("uxui_texture_scale", "TextureScale", TextureSpec.MinScale, TextureSpec.MaxScale, 0.25,
                () => texture.Scale, n => texture.Scale = n);
            // UXUI-Extreme (UX-013)
            cells[5] = TextureNumber("uxui_texture_angle", "TextureAngle", TextureSpec.MinAngle, TextureSpec.MaxAngle, 5,
                () => texture.Angle, n => texture.Angle = n);
            cells[6] = TextureNumber("uxui_texture_contrast", "TextureContrast", TextureSpec.MinContrast, TextureSpec.MaxContrast, 0.1,
                () => texture.Contrast, n => texture.Contrast = n);
            // UXUI-Extreme: tone
            cells[7] = TextureNumber("uxui_texture_brightness", "TextureBrightness", TextureSpec.MinBrightness, TextureSpec.MaxBrightness, 0.05,
                () => texture.Brightness, n => texture.Brightness = n);
            cells[8] = TextureNumber("uxui_texture_saturation", "TextureSaturation", TextureSpec.MinSaturation, TextureSpec.MaxSaturation, 0.05,
                () => texture.Saturation, n => texture.Saturation = n);
            cells[9] = TextureNumber("uxui_texture_white", "TextureWhite", TextureSpec.MinWhite, TextureSpec.MaxWhite, 0.05,
                () => texture.WhiteLevel, n => texture.WhiteLevel = n);
            // UXUI-Extreme: scaling quality and sharpening
            var resample = new ComboBox { Width = 90, HorizontalAlignment = HorizontalAlignment.Left, Name = "TextureResample" };
            resample.ItemsSource = TextureResampler.Methods.Select(m => TextureText("uxui_texture_resample_" + m)).ToList();
            resample.SelectedIndex = Math.Max(0, TextureResampler.Methods.ToList().IndexOf(texture.Resample));
            resample.SelectionChanged += (s, e) =>
            {
                if (resample.SelectedIndex < 0) return;
                texture.Resample = TextureResampler.Methods[resample.SelectedIndex];
                RefreshTexturePreview(); RaiseChanged();
            };
            var resampleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            resampleRow.Children.Add(new Label { Content = TextureText("uxui_texture_resample"), Target = resample, Width = 66, Padding = new Thickness(2, 2, 2, 2) });
            resampleRow.Children.Add(resample);
            cells[10] = TextureNumber("uxui_texture_sharpen", "TextureSharpen", 0, TextureSpec.MaxSharpen, 0.1,
                () => texture.Sharpen, n => texture.Sharpen = n);
            cells[11] = TextureNumber("uxui_texture_sharpen_radius", "TextureSharpenRadius", TextureSpec.MinRadius, TextureSpec.MaxRadius, 0.1,
                () => texture.SharpenRadius, n => texture.SharpenRadius = n);

            var numbers = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            for (int c = 0; c < 3; c++) numbers.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int gridRow = 0;
            for (int r = 0; r < cells.Length; r += 3)
            {
                if (!cells.Skip(r).Take(3).Any(c => c != null)) continue;
                numbers.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                for (int c = 0; c < 3 && r + c < cells.Length; c++)
                {
                    if (cells[r + c] == null) continue;
                    Grid.SetRow(cells[r + c], gridRow);
                    Grid.SetColumn(cells[r + c], c);
                    numbers.Children.Add(cells[r + c]);
                }
                gridRow++;
            }
            _details.Children.Add(numbers);

            // Scaling and the preview strip on the left; Config and the palette slot beside them.
            var lower = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            lower.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 250 });
            lower.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            left.Children.Add(resampleRow);
            left.Children.Add(preview);
            var right = new StackPanel();
            right.Children.Add(ConfigRow(texture, say));
            if (_texturePalette != null) right.Children.Add(PaletteRow(texture, say));
            Grid.SetColumn(right, 1);
            lower.Children.Add(left);
            lower.Children.Add(right);
            _details.Children.Add(lower);
            _details.Children.Add(status);
            RefreshTexturePreview();
        }

        private UIElement TextureNumber(string labelKey, string name, double min, double max, double step, Func<double> get, Action<double> set)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true,
                Width = 52, Value = get(), VerticalAlignment = VerticalAlignment.Center };
            var box = new TextBox { Name = name, Width = 40, Margin = new Thickness(4, 0, 4, 0), Text = get().ToString("0.###", CultureInfo.CurrentCulture) };
            row.Children.Add(new Label { Content = TextureText(labelKey), Target = box, Width = 66, Padding = new Thickness(2) });
            row.Children.Add(slider); row.Children.Add(box);
            bool updating = false;
            slider.ValueChanged += (s, e) =>
            {
                if (updating) return;
                set(slider.Value);
                updating = true;
                box.Text = get().ToString("0.###", CultureInfo.CurrentCulture);
                box.ClearValue(Control.BorderBrushProperty);
                updating = false;
                RefreshTexturePreview(); RaiseChanged();
            };
            box.TextChanged += (s, e) =>
            {
                if (updating) return;
                if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double n) && double.IsFinite(n) && n >= min && n <= max)
                {
                    box.ClearValue(Control.BorderBrushProperty);
                    set(n);
                    updating = true; slider.Value = n; updating = false;
                    RefreshTexturePreview(); RaiseChanged();
                }
                else box.BorderBrush = Brushes.Red;
            };
            box.LostFocus += (s, e) =>
            {
                updating = true;
                box.Text = get().ToString("0.###", CultureInfo.CurrentCulture);
                box.ClearValue(Control.BorderBrushProperty);
                updating = false;
            };
            return row;
        }
    }
}
