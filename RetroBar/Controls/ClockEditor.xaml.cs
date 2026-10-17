using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the Clock tab (moved here from the Layout tab). Every control writes straight
    // to Settings; the taskbar's text styler and layout pick the changes up live.
    public partial class ClockEditor : UserControl
    {
        private const double SpacingSamePosition = 0.5; // slider's leftmost = same as taskbar
        private bool _loading;

        public ClockEditor()
        {
            InitializeComponent();
        }

        private string Text(string key) => TryFindResource(key) as string ?? Application.Current.TryFindResource(key) as string ?? key;

        private void ClockEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                Load();
            }
        }

        private void Load()
        {
            _loading = true;
            var s = Settings.Instance;

            cboClockOrientation.SelectedIndex = (int)s.ClockOrientation;
            cboClockItalic.SelectedIndex = (int)s.ClockFontItalic;
            cboClockMargin.SelectedIndex = (int)s.ClockMarginMode;
            txtClockMargin.Text = s.ClockMargin;

            // Fonts: "Same as taskbar" first, then the built-in fonts (UX-017), then installed fonts.
            FontChoices.Fill(cboClockFont, Text("uxui_layout_same_as_taskbar"), s.ClockFontFamily);
            FontChoices.Fill(cboClockMono, Text("uxui_layout_clock_mono_default"), s.ClockMonoFamily);

            // Weights: the Style tab's list, with "Same as taskbar" in place of "Theme default".
            cboClockWeight.Items.Clear();
            cboClockWeight.Items.Add(Text("uxui_layout_same_as_taskbar"));
            if (FindResource("uxui_font_weight_values") is Array weights)
            {
                foreach (object weight in weights.Cast<object>().Skip(1))
                {
                    cboClockWeight.Items.Add(weight);
                }
            }
            int weightIndex = s.ClockFontWeight / 100;
            cboClockWeight.SelectedIndex = weightIndex >= 0 && weightIndex < cboClockWeight.Items.Count ? weightIndex : 0;

            sldClockSpacing.Value = s.ClockLineSpacing > 0 ? s.ClockLineSpacing : SpacingSamePosition;

            _loading = false;
            UpdateTexts();
        }

        private void Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(sender is ComboBox combo))
            {
                return;
            }

            if (combo.SelectedIndex < 0)
            {
                // Item list was reloaded (language change); put the selection back.
                if (!_loading && IsLoaded)
                {
                    Dispatcher.BeginInvoke(new Action(Load));
                }
                return;
            }

            if (_loading)
            {
                return;
            }

            var s = Settings.Instance;
            int i = combo.SelectedIndex;

            if (combo == cboClockOrientation) s.ClockOrientation = (ElementOrientationOption)i;
            else if (combo == cboClockItalic) s.ClockFontItalic = (ClockItalicOption)i;
            else if (combo == cboClockMargin) s.ClockMarginMode = (ClockMarginOption)i;
            else if (combo == cboClockFont) s.ClockFontFamily = FontChoices.Selected(combo) ?? s.ClockFontFamily;
            else if (combo == cboClockMono) s.ClockMonoFamily = FontChoices.Selected(combo) ?? s.ClockMonoFamily;
            else if (combo == cboClockWeight) s.ClockFontWeight = i * 100;

            UpdateTexts();
        }

        private void TxtClockMargin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            if (NotifyIconList.TryParseMargin(txtClockMargin.Text, out _))
            {
                txtClockMargin.ClearValue(Control.BorderBrushProperty);
                Settings.Instance.ClockMargin = txtClockMargin.Text.Trim();
            }
            else
            {
                txtClockMargin.BorderBrush = System.Windows.Media.Brushes.Red;
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTexts();
        }

        private void SldClockSpacing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || sldClockSpacing == null)
            {
                return;
            }

            double value = sldClockSpacing.Value;
            Settings.Instance.ClockLineSpacing = value < SpacingSamePosition + 0.025 ? 0 : Math.Round(value, 2);
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            if (_loading || txtClockSpacing == null)
            {
                return;
            }

            var s = Settings.Instance;
            string same = Text("uxui_layout_same_as_taskbar");
            txtClockSize.Text = s.ClockFontSize > 0 ? $"{Math.Round(s.ClockFontSize)} px" : same;
            txtClockSpacing.Text = s.ClockLineSpacing > 0 ? "× " + s.ClockLineSpacing.ToString("0.00", CultureInfo.CurrentCulture) : same;
        }
    }
}
