using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // Gives a texture editor access to the style's texture palette (twelve saved textures).
    public class TexturePaletteHost
    {
        public Func<TextureSpec[]> Slots;
        public Action Changed;
    }

    // UXUI-Extreme: the texture editor's presets, copy / paste config field and texture palette row.
    public partial class BrushEditor
    {
        private readonly TexturePaletteHost _texturePalette;
        private TextBox _configBox;

        private static Button ToolButton(string text, string tip = null) => new Button
        {
            Content = text, MinWidth = 0, MinHeight = 21, Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(0, 0, 4, 0),
            ToolTip = tip, VerticalAlignment = VerticalAlignment.Center,
        };

        private Window Owner => Window.GetWindow(this);

        private void Report(Action<string> status, string text) => status?.Invoke(text);

        // ---- presets ----------------------------------------------------------------------------
        // The buttons (Save, Delete, Import, Export) come back as the row; the picker comes out separately
        // (it sits in the editor's heading, and says "Preset" until something is chosen).
        private UIElement PresetRow(TextureSpec texture, Action<string> status, out UIElement picker)
        {
            var row = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
            var combo = new ComboBox { Width = 120, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

            var builtIn = TexturePresets.ForPattern(texture.Name).ToList();
            var user = TexturePresets.LoadUser();
            var entries = builtIn.Select(p => (spec: p, text: p.Label)).Concat(user.Select(p => (spec: p, text: "★ " + p.Label))).ToList();
            combo.ItemsSource = entries.Select(e => e.text).ToList();
            combo.SelectedIndex = -1;
            combo.ToolTip = TextureText("uxui_texture_preset_tip");

            var hint = new TextBlock
            {
                Text = TextureText("uxui_texture_preset"), IsHitTestVisible = false, Margin = new Thickness(11, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left, Foreground = SystemColors.GrayTextBrush,
            };
            var host = new Grid();
            host.Children.Add(combo);
            host.Children.Add(hint);
            combo.SelectionChanged += (s, e) => hint.Visibility = combo.SelectedIndex < 0 ? Visibility.Visible : Visibility.Collapsed;
            picker = host;

            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex < 0 || combo.SelectedIndex >= entries.Count) return;
                TextureSpec preset = entries[combo.SelectedIndex].spec;
                texture.ApplyParameters(preset); // the colours stay yours
                texture.Label = "";
                Dispatcher.BeginInvoke(new Action(() => { BuildDetails(); RaiseChanged(); }));
            };

            var save = ToolButton(TextureText("uxui_texture_preset_save"), TextureText("uxui_texture_preset_save_tip"));
            save.Click += (s, e) =>
            {
                string name = PromptWindow.Ask(Owner, TextureText("uxui_texture_preset_save"), TextureText("uxui_texture_preset_name"), texture.Label);
                if (name == null) return;
                try
                {
                    TexturePresets.Store(texture, name);
                    BuildDetails();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Report(status, ex.Message);
                }
            };
            var delete = ToolButton(TextureText("uxui_texture_preset_delete"));
            delete.IsEnabled = combo.SelectedIndex >= builtIn.Count;
            combo.SelectionChanged += (s, e) => delete.IsEnabled = combo.SelectedIndex >= builtIn.Count;
            delete.Click += (s, e) =>
            {
                int index = combo.SelectedIndex - builtIn.Count;
                if (index < 0 || index >= user.Count) return;
                try
                {
                    TexturePresets.Delete(user[index].Label);
                    BuildDetails();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Report(status, ex.Message);
                }
            };
            var import = ToolButton(TextureText("uxui_texture_preset_import"), TextureText("uxui_texture_preset_import_tip"));
            import.Click += (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog { Filter = TextureText("uxui_texture_preset_filter"), DefaultExt = ".json" };
                if (dialog.ShowDialog(Owner) != true) return;
                try
                {
                    int count = TexturePresets.Import(dialog.FileName);
                    BuildDetails();
                    Report(status, string.Format(TextureText("uxui_texture_preset_imported"), count));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
                {
                    Report(status, TextureText("uxui_texture_preset_import_failed") + " " + ex.Message);
                }
            };
            var export = ToolButton(TextureText("uxui_texture_preset_export"), TextureText("uxui_texture_preset_export_tip"));
            export.Click += (s, e) =>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog { Filter = TextureText("uxui_texture_preset_filter"), DefaultExt = ".json", FileName = "texture-presets.json" };
                if (dialog.ShowDialog(Owner) != true) return;
                try
                {
                    var mine = TexturePresets.LoadUser();
                    if (mine.Count == 0)
                    {
                        TextureSpec current = texture.Clone();
                        if (string.IsNullOrWhiteSpace(current.Label)) current.Label = current.Name;
                        mine.Add(current); // nothing saved yet: export the texture being edited
                    }
                    TexturePresets.Export(dialog.FileName, mine);
                    Report(status, string.Format(TextureText("uxui_texture_preset_exported"), mine.Count));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Report(status, ex.Message);
                }
            };
            row.Children.Add(save);
            row.Children.Add(delete);
            row.Children.Add(import);
            row.Children.Add(export);
            return row;
        }

        // ---- copy / paste config ------------------------------------------------------------------
        private UIElement ConfigRow(TextureSpec texture, Action<string> status)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            var box = new TextBox
            {
                Text = TextureConfig.ToText(texture), FontFamily = new FontFamily("Consolas"), FontSize = 11,
                TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, MinHeight = 38, MaxHeight = 70, MaxWidth = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalAlignment = HorizontalAlignment.Stretch,
                ToolTip = TextureText("uxui_texture_config_tip"),
            };
            _configBox = box;
            Action apply = () =>
            {
                try
                {
                    TextureSpec parsed = TextureConfig.FromText(box.Text, texture);
                    parsed.Label = "";
                    _value.Texture = parsed;
                    Dispatcher.BeginInvoke(new Action(() => { BuildDetails(); RaiseChanged(); }));
                }
                catch (FormatException ex)
                {
                    Report(status, TextureText("uxui_texture_config_failed") + " " + ex.Message);
                }
            };
            box.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Return) { apply(); e.Handled = true; }
            };

            var buttons = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            var label = new Label { Content = TextureText("uxui_texture_config"), Padding = new Thickness(2, 2, 6, 2), Target = box };
            var copy = ToolButton(TextureText("uxui_texture_config_copy"));
            copy.Click += (s, e) =>
            {
                try { Clipboard.SetText(TextureConfig.ToText(texture)); Report(status, TextureText("uxui_texture_config_copied")); }
                catch (System.Runtime.InteropServices.COMException) { Report(status, TextureText("uxui_texture_config_clipboard")); }
            };
            var paste = ToolButton(TextureText("uxui_texture_config_paste"), TextureText("uxui_texture_config_paste_tip"));
            paste.Click += (s, e) =>
            {
                try
                {
                    if (!Clipboard.ContainsText()) { Report(status, TextureText("uxui_texture_config_clipboard")); return; }
                    box.Text = Clipboard.GetText();
                    apply();
                }
                catch (System.Runtime.InteropServices.COMException) { Report(status, TextureText("uxui_texture_config_clipboard")); }
            };
            var use = ToolButton(TextureText("uxui_texture_config_apply"), TextureText("uxui_texture_config_apply_tip"));
            use.Click += (s, e) => apply();
            buttons.Children.Add(label);
            buttons.Children.Add(copy);
            buttons.Children.Add(paste);
            buttons.Children.Add(use);
            panel.Children.Add(buttons);
            panel.Children.Add(box);
            return panel;
        }

        // ---- the style's texture palette --------------------------------------------------------------
        private UIElement PaletteRow(TextureSpec texture, Action<string> status)
        {
            var row = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
            var slot = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
            Action fill = () =>
            {
                int keep = Math.Max(0, slot.SelectedIndex);
                TextureSpec[] slots = _texturePalette.Slots();
                slot.ItemsSource = Enumerable.Range(0, slots.Length).Select(i => $"{i + 1}" + (slots[i] != null ? " ●" : "")).ToList();
                slot.SelectedIndex = keep;
            };
            fill();
            var label = new Label { Content = TextureText("uxui_texture_slot"), Target = slot, Padding = new Thickness(2, 2, 6, 2) };
            var use = ToolButton(TextureText("uxui_texture_slot_use"), TextureText("uxui_texture_slot_use_tip"));
            use.Click += (s, e) =>
            {
                TextureSpec saved = _texturePalette.Slots()[Math.Max(0, slot.SelectedIndex)];
                if (saved == null) { Report(status, TextureText("uxui_texture_slot_empty")); return; }
                _value.Texture = saved.Clone();
                _value.Texture.Label = "";
                Dispatcher.BeginInvoke(new Action(() => { BuildDetails(); RaiseChanged(); }));
            };
            var store = ToolButton(TextureText("uxui_texture_slot_store"), TextureText("uxui_texture_slot_store_tip"));
            store.Click += (s, e) =>
            {
                int index = Math.Max(0, slot.SelectedIndex);
                TextureSpec copy = texture.Clone();
                copy.Label = "";
                _texturePalette.Slots()[index] = copy;
                _texturePalette.Changed?.Invoke();
                fill();
                Report(status, string.Format(TextureText("uxui_texture_slot_stored"), index + 1));
            };
            row.Children.Add(label);
            row.Children.Add(slot);
            row.Children.Add(use);
            row.Children.Add(store);
            return row;
        }
    }
}
