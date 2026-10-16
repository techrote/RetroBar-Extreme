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
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 310, Margin = new Thickness(0, 3, 0, 2) };
            var preview = new Border
            {
                Height = 36, Margin = new Thickness(0, 4, 0, 4), BorderThickness = new Thickness(1),
                BorderBrush = SystemColors.ControlDarkDarkBrush, Name = "TexturePreview",
            };
            _refreshTexturePreview = () =>
            {
                try
                {
                    preview.Background = _value.ToBrush(palette: _paletteSource?.Invoke());
                    status.Text = TextureText("uxui_texture_hint");
                    status.Foreground = SystemColors.ControlTextBrush;
                }
                catch (Exception e)
                {
                    preview.Background = null;
                    status.Text = TextureText("uxui_texture_fallback") + " " + e.Message;
                    status.Foreground = Brushes.Firebrick;
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
            };
            var patternRow = new DockPanel();
            var label = new Label { Content = TextureText("uxui_texture_pattern"), Target = pattern };
            DockPanel.SetDock(label, Dock.Left);
            patternRow.Children.Add(label);
            patternRow.Children.Add(pattern);
            _details.Children.Add(patternRow);

            var presets = new WrapPanel { Margin = new Thickness(0, 3, 0, 3) };
            var ratrod = new Button { Content = TextureText("uxui_texture_ratrod"), Margin = new Thickness(0, 0, 6, 0) };
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
            _details.Children.Add(presets);

            var palette = new CheckBox
            {
                Content = TextureText("uxui_texture_palette"), IsChecked = texture.UsePalette,
                Margin = new Thickness(0, 3, 0, 3), Name = "TexturePalette",
            };
            palette.Click += (s, e) =>
            {
                texture.UsePalette = palette.IsChecked == true;
                if (!texture.UsePalette && (texture.Lut == null || texture.Lut.Count < 2))
                    texture.Lut = new[] { Colors.Black, Colors.White }.ToList();
                BuildDetails(); RaiseChanged();
            };
            _details.Children.Add(palette);
            if (!texture.UsePalette)
            {
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
                    _details.Children.Add(row);
                }
                if (texture.Lut.Count < MAX_STOPS)
                {
                    var add = new Button { Content = TextureText("uxui_texture_add"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 3, 0, 3) };
                    add.Click += (s, e) =>
                    {
                        int last = texture.Lut.Count - 1;
                        texture.Lut.Insert(last, LutTextures.Sample(new[] { texture.Lut[last - 1], texture.Lut[last] }, 0.5));
                        BuildDetails(); RaiseChanged();
                    };
                    _details.Children.Add(add);
                }
            }
            _details.Children.Add(TextureNumber("uxui_texture_scale", "TextureScale", TextureSpec.MinScale, TextureSpec.MaxScale, 0.25,
                () => texture.Scale, n => texture.Scale = n));
            _details.Children.Add(TextureNumber("uxui_texture_contrast", "TextureContrast", TextureSpec.MinContrast, TextureSpec.MaxContrast, 0.1,
                () => texture.Contrast, n => texture.Contrast = n));
            // UXUI-Extreme (UX-013)
            _details.Children.Add(TextureNumber("uxui_texture_angle", "TextureAngle", TextureSpec.MinAngle, TextureSpec.MaxAngle, 5,
                () => texture.Angle, n => texture.Angle = n));
            _details.Children.Add(preview);
            _details.Children.Add(status);
            RefreshTexturePreview();
        }

        private UIElement TextureNumber(string labelKey, string name, double min, double max, double step, Func<double> get, Action<double> set)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            var slider = new Slider { Minimum = min, Maximum = max, TickFrequency = step, IsSnapToTickEnabled = true,
                Width = 125, Value = get(), VerticalAlignment = VerticalAlignment.Center };
            var box = new TextBox { Name = name, Width = 48, Margin = new Thickness(6, 0, 0, 0), Text = get().ToString("0.###", CultureInfo.CurrentCulture) };
            row.Children.Add(new Label { Content = TextureText(labelKey), Target = box, Width = 72 });
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
