using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ManagedShell.Common.Logging;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-004): style editor for the Colours tab in Properties.
    //
    // Edits the active style (Style.json) in place: every change is written to the file and the
    // style layer reapplied, so the taskbar previews it live and it survives a restart. Named
    // styles are copies kept in %LOCALAPPDATA%\RetroBar\Styles\; Load copies one into the active
    // style. Import/Export read and write style files anywhere. Fonts stay on the Style tab.
    public partial class StyleEditor : UserControl
    {
        private StyleDocument _style = new StyleDocument();
        private readonly DispatcherTimer _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        private bool _subscribed;

        // UXUI-Extreme: lets texture editors store to and use the style's texture palette.
        private TexturePaletteHost _textureHost;

        // UX-009: the palette grid (StyleEditor.Palette.cs).
        private readonly ColourField[] _paletteFields = new ColourField[StyleDocument.PALETTE_SIZE];

        public StyleEditor()
        {
            InitializeComponent();

            _saveTimer.Tick += (s, e) => SaveActive();
            cboStyleName.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => UpdateButtons()));

            _textureHost = new TexturePaletteHost
            {
                Slots = () => _style.TexturePalette,
                Changed = () =>
                {
                    ShowTextureSlots();
                    ScheduleSave();
                },
            };
            BuildPaletteGrid();
            foreach (Colourways.Preset preset in Colourways.Presets)
            {
                cboPreset.Items.Add(preset.Name);
            }
        }

        private void StyleEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                // Pick up edits made outside (by hand, or another copy of the style).
                LoadActive();

                if (!_subscribed)
                {
                    Settings.Instance.PropertyChanged += Settings_PropertyChanged;
                    _subscribed = true;
                }
            }
            else
            {
                FlushSave();

                if (_subscribed)
                {
                    Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
                    _subscribed = false;
                }
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Which settings do anything, and the theme's starting colours, depend on the theme.
            if (e.PropertyName == nameof(Settings.Theme))
            {
                BuildPart();
            }
        }

        private void LoadActive()
        {
            _style = new StyleDocument();

            if (File.Exists(StyleLayer.StylePath))
            {
                try
                {
                    _style = StyleDocument.Load(StyleLayer.StylePath, warning => ShellLogger.Warning($"UXUI-Extreme: {warning}"));
                }
                catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
                {
                    // Editing starts from scratch; the broken file is only replaced on the first change.
                    ShellLogger.Warning($"UXUI-Extreme: style editor could not read {StyleLayer.StylePath}: {e.Message}");
                }
            }

            RefreshNames();
            cboStyleName.Text = _style.Name;
            ShowPaletteFields();

            if (cboPart.SelectedIndex < 0)
            {
                cboPart.SelectedIndex = 0;
            }
            else
            {
                BuildPart();
            }
        }

        private void RefreshNames()
        {
            string text = cboStyleName.Text;
            cboStyleName.ItemsSource = StyleDocument.ListedNames(); // UX-017: examples too
            cboStyleName.Text = text;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool saved = !string.IsNullOrWhiteSpace(cboStyleName.Text) && File.Exists(StyleDocument.SavedPath(cboStyleName.Text));
            btnLoad.IsEnabled = saved || StyleDocument.IsExample(cboStyleName.Text); // UX-017: built-in examples load too
            btnDelete.IsEnabled = saved;
        }

        // UXUI-Extreme (UX-010): the bitmap part follows the existing localised brush parts.
        private string CurrentPart => cboPart.SelectedIndex == StyleMap.Parts.Length ? "start"
            : cboPart.SelectedIndex >= 0 && cboPart.SelectedIndex < StyleMap.Parts.Length ? StyleMap.Parts[cboPart.SelectedIndex] : null;

        private void CboPart_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BuildPart();
        }

        private void BuildPart()
        {
            panelProperties.Children.Clear();
            btnFromStart.IsEnabled = StartButtonColourways.HasImage(StyleLayer.ThemeResource);

            string part = CurrentPart;
            if (part == null)
            {
                return;
            }
            if (part == "start") // UXUI-Extreme (UX-010): not a brush or gradient property.
            {
                BuildStartPart();
                return;
            }

            foreach (string property in StyleMap.PartProperties[part])
            {
                string p = property;
                var editor = new BrushEditor(
                    $"uxui_style_prop_{p}",
                    _style.Get(part, p),
                    () => BrushSpec.FromBrush(StyleLayer.ThemeBrush(part, p)),
                    StyleLayer.IsUsed(part, p),
                    () => _style.Palette, // UXUI-Extreme (UX-011): live palette, not a stale copy.
                    _textureHost);
                editor.Changed += (s, e) =>
                {
                    _style.Set(part, p, ((BrushEditor)s).Value);
                    ScheduleSave();
                };
                panelProperties.Children.Add(editor);
            }

            AddBorderRow(part); // UXUI-Extreme (UX-013)
        }

        private bool _switchingStyle;

        // Picking a style in the list loads it straight away (typing a name does not).
        private void CboStyleName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();

            if (!_switchingStyle && e.AddedItems.Count == 1 && e.AddedItems[0] is string name && cboStyleName.IsDropDownOpen)
            {
                LoadNamed(name);
            }
        }

        // Steps through the saved + example styles in list order (wrapping), loading each at once.
        private void Step(int direction)
        {
            List<string> names = StyleDocument.ListedNames();
            if (names.Count == 0)
            {
                return;
            }

            int at = names.FindIndex(n => string.Equals(n, cboStyleName.Text?.Trim(), StringComparison.OrdinalIgnoreCase));
            int next = at < 0 ? (direction > 0 ? 0 : names.Count - 1) : (at + direction + names.Count) % names.Count;
            LoadNamed(names[next]);
        }

        private void Prev_OnClick(object sender, RoutedEventArgs e) => Step(-1);

        private void Next_OnClick(object sender, RoutedEventArgs e) => Step(1);

        private void ScheduleSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void FlushSave()
        {
            if (_saveTimer.IsEnabled)
            {
                SaveActive();
            }
        }

        // Writes the active style and reapplies the layer so the taskbar shows it at once. An
        // empty style removes Style.json, which leaves the plain theme.
        private void SaveActive()
        {
            _saveTimer.Stop();

            try
            {
                if (_style.IsEmpty)
                {
                    File.Delete(StyleLayer.StylePath);
                }
                else
                {
                    _style.Save(StyleLayer.StylePath);
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                ShellLogger.Warning($"UXUI-Extreme: style editor could not save {StyleLayer.StylePath}: {e.Message}");
                return;
            }

            StyleLayer.Apply();
        }

        private void UseStyle(StyleDocument style, string fallbackName)
        {
            _style = style;
            if (string.IsNullOrWhiteSpace(style.Name))
            {
                _style.Name = fallbackName;
            }

            cboStyleName.Text = _style.Name;
            SaveActive();
            ShowPaletteFields();
            BuildPart();
            UpdateButtons();
        }

        // Loads a style file, telling the user about anything skipped. Null if it can't be used.
        private StyleDocument ReadStyle(string path) => ReadStyle(warn => StyleDocument.Load(path, warn));

        // UX-017: a built-in example style.
        private StyleDocument ReadExample(string name) => ReadStyle(warn => StyleDocument.Parse(StyleDocument.ExampleJson(name) ?? "", warn));

        private StyleDocument ReadStyle(Func<Action<string>, StyleDocument> load)
        {
            var warnings = new List<string>();
            try
            {
                StyleDocument style = load(warnings.Add);
                if (warnings.Count > 0)
                {
                    Show(string.Format(Text("uxui_style_load_warnings"), string.Join(Environment.NewLine, warnings)), MessageBoxImage.Warning);
                }
                return style;
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                Show(string.Format(Text("uxui_style_load_failed"), e.Message), MessageBoxImage.Error);
                return null;
            }
        }

        private void Save_OnClick(object sender, RoutedEventArgs e)
        {
            string name = cboStyleName.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                Show(Text("uxui_style_need_name"), MessageBoxImage.Information);
                cboStyleName.Focus();
                return;
            }

            string path = StyleDocument.SavedPath(name);
            if (File.Exists(path) && !Confirm(string.Format(Text("uxui_style_overwrite"), name)))
            {
                return;
            }

            _style.Name = name;
            try
            {
                _style.Save(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Show(string.Format(Text("uxui_style_save_failed"), ex.Message), MessageBoxImage.Error);
                return;
            }

            SaveActive();
            RefreshNames();
        }

        private void Load_OnClick(object sender, RoutedEventArgs e) => LoadNamed(cboStyleName.Text?.Trim());

        private void LoadNamed(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            string path = StyleDocument.SavedPath(name);
            StyleDocument style = File.Exists(path) || !StyleDocument.IsExample(name) ? ReadStyle(path) : ReadExample(name);
            if (style != null)
            {
                _switchingStyle = true;
                try
                {
                    UseStyle(style, name);
                    cboStyleName.Text = name; // a style file's own Name may differ; show the entry that was picked
                }
                finally
                {
                    _switchingStyle = false;
                }
            }
        }

        private void Delete_OnClick(object sender, RoutedEventArgs e)
        {
            string name = cboStyleName.Text?.Trim();
            string path = StyleDocument.SavedPath(name ?? "");
            if (string.IsNullOrEmpty(name) || !File.Exists(path) || !Confirm(string.Format(Text("uxui_style_delete_confirm"), name)))
            {
                return;
            }

            try
            {
                // The active style is a separate copy, so the taskbar keeps its look.
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Show(string.Format(Text("uxui_style_save_failed"), ex.Message), MessageBoxImage.Error);
            }

            RefreshNames();
        }

        private void Import_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = Text("uxui_style_file_filter"), DefaultExt = ".json" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            StyleDocument style = ReadStyle(dialog.FileName);
            if (style != null)
            {
                UseStyle(style, Path.GetFileNameWithoutExtension(dialog.FileName));
            }
        }

        private void Export_OnClick(object sender, RoutedEventArgs e)
        {
            FlushSave();

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = Text("uxui_style_file_filter"),
                DefaultExt = ".json",
                FileName = string.IsNullOrWhiteSpace(_style.Name) ? "Style" : _style.Name,
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                _style.Save(dialog.FileName);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Show(string.Format(Text("uxui_style_save_failed"), ex.Message), MessageBoxImage.Error);
            }
        }

        private void ResetAll_OnClick(object sender, RoutedEventArgs e)
        {
            if (_style.IsEmpty && string.IsNullOrEmpty(_style.Name))
            {
                return;
            }

            if (!Confirm(Text("uxui_style_reset_all_confirm")))
            {
                return;
            }

            // The palette is a working set of colours, not part of the look, so it stays.
            var cleared = new StyleDocument();
            System.Array.Copy(_style.Palette, cleared.Palette, StyleDocument.PALETTE_SIZE);
            for (int i = 0; i < StyleDocument.TEXTURE_PALETTE_SIZE; i++) cleared.TexturePalette[i] = _style.TexturePalette[i]; // saved textures stay too
            UseStyle(cleared, "");
        }

        private void ResetPart_OnClick(object sender, RoutedEventArgs e)
        {
            string part = CurrentPart;
            if (part == null)
            {
                return;
            }
            if (part == "start") // UXUI-Extreme (UX-010): restore the original bitmap only.
            {
                _style.StartRecolour = null;
                SaveActive();
                BuildPart();
                return;
            }

            foreach (string property in StyleMap.PartProperties[part])
            {
                _style.Set(part, property, null);
            }

            SaveActive();
            BuildPart();
        }

        private string Text(string key) => FindResource(key) as string ?? key;

        private void Show(string message, MessageBoxImage icon)
        {
            MessageBox.Show(Window.GetWindow(this), message, Text("retrobar_properties"), MessageBoxButton.OK, icon);
        }

        private bool Confirm(string message)
        {
            return MessageBox.Show(Window.GetWindow(this), message, Text("retrobar_properties"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }
}
