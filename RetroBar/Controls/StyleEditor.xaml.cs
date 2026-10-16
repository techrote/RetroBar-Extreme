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

        // UX-009: colourway pickers and the floating palette.
        private readonly ColourField _base = new ColourField(Colourways.Presets[0].Base, compact: true);
        private readonly ColourField _accent = new ColourField(Colourways.Presets[0].Accent, compact: true);
        private PaletteWindow _palette;

        public StyleEditor()
        {
            InitializeComponent();

            _saveTimer.Tick += (s, e) => SaveActive();
            cboStyleName.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => UpdateButtons()));

            hostBase.Content = _base;
            hostAccent.Content = _accent;
            foreach (Colourways.Preset preset in Colourways.Presets)
            {
                cboPreset.Items.Add(preset.Name);
            }
        }

        // UX-009: a preset fills in the base and accent colours; Apply writes the colourway.
        private void CboPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboPreset.SelectedIndex >= 0)
            {
                Colourways.Preset preset = Colourways.Presets[cboPreset.SelectedIndex];
                _base.Value = preset.Base;
                _accent.Value = preset.Accent;
            }
        }

        private void ApplyColourway_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(_base.Value is Color baseColour) || !(_accent.Value is Color accent))
            {
                return;
            }

            bool replacing = false;
            foreach (string part in Colourways.GeneratedParts)
            {
                replacing |= _style.Parts.ContainsKey(part);
            }

            if (replacing && !Confirm(Text("uxui_colourway_replace_confirm")))
            {
                return;
            }

            Colourways.Apply(_style, baseColour, accent);
            SaveActive();
            BuildPart();
        }

        private void Palette_OnClick(object sender, RoutedEventArgs e)
        {
            if (_palette != null)
            {
                _palette.Activate();
                return;
            }

            _palette = new PaletteWindow(Window.GetWindow(this), _style.Palette);
            _palette.SlotChanged += (slot, colour) =>
            {
                _style.Palette[slot] = colour;
                // UXUI-Extreme (UX-011): update previews without rebuilding focused controls.
                foreach (object child in panelProperties.Children)
                    if (child is BrushEditor editor) editor.RefreshTexturePreview();
                ScheduleSave();
            };
            _palette.Closed += (s, args) => _palette = null;
            _palette.Show();
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
            _palette?.ShowPalette(_style.Palette);

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
            cboStyleName.ItemsSource = StyleDocument.SavedNames();
            cboStyleName.Text = text;
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool saved = !string.IsNullOrWhiteSpace(cboStyleName.Text) && File.Exists(StyleDocument.SavedPath(cboStyleName.Text));
            btnLoad.IsEnabled = saved;
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
                    () => _style.Palette); // UXUI-Extreme (UX-011): live palette, not a stale copy.
                editor.Changed += (s, e) =>
                {
                    _style.Set(part, p, ((BrushEditor)s).Value);
                    ScheduleSave();
                };
                panelProperties.Children.Add(editor);
            }
        }

        private void CboStyleName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtons();
        }

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
            _palette?.ShowPalette(_style.Palette);
            BuildPart();
            UpdateButtons();
        }

        // Loads a style file, telling the user about anything skipped. Null if it can't be used.
        private StyleDocument ReadStyle(string path)
        {
            var warnings = new List<string>();
            try
            {
                StyleDocument style = StyleDocument.Load(path, warnings.Add);
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

        private void Load_OnClick(object sender, RoutedEventArgs e)
        {
            string name = cboStyleName.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            StyleDocument style = ReadStyle(StyleDocument.SavedPath(name));
            if (style != null)
            {
                UseStyle(style, name);
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
