using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-012): the Layout tab in Properties. Every control writes straight to
    // Settings; the taskbar (Taskbar.Layout.cs) and the text styler pick the changes up live.
    public partial class LayoutEditor : UserControl
    {
        private const double SpacingSamePosition = 0.5; // slider's leftmost = same as taskbar
        private bool _loading;

        public LayoutEditor()
        {
            InitializeComponent();
        }

        private void LayoutEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                Load();
            }
        }

        private string Text(string key) => FindResource(key) as string ?? key;

        private void Load()
        {
            _loading = true;
            var s = Settings.Instance;

            cboStartOrientation.SelectedIndex = (int)s.StartButtonOrientation;
            cboStartLength.SelectedIndex = (int)s.StartButtonLengthMode;
            cboStartThickness.SelectedIndex = (int)s.StartButtonThicknessMode;
            cboStartStretch.SelectedIndex = (int)s.StartButtonStretchMode;
            cboStartAnchor.SelectedIndex = (int)s.StartButtonAnchor;
            cboShowDesktopPosition.SelectedIndex = (int)s.ShowDesktopPosition;
            cboClockOrientation.SelectedIndex = (int)s.ClockOrientation;
            cboClockItalic.SelectedIndex = (int)s.ClockFontItalic;

            // Fonts: "Same as taskbar" first, then the built-in fonts (UX-017), then installed fonts.
            FontChoices.Fill(cboClockFont, Text("uxui_layout_same_as_taskbar"), s.ClockFontFamily);

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

            ShowStartIcon(); // UX-015
            ShowToggleSettings(); // UX-016

            // UX-014
            cboTaskWidth.SelectedIndex = s.TaskButtonWidthPx > 0 ? 1 : 0;
            sldTaskWidth.Value = s.TaskButtonWidthPx > 0 ? s.TaskButtonWidthPx : 160;
            LoadApps();

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

            if (combo == cboStartOrientation) s.StartButtonOrientation = (ElementOrientationOption)i;
            else if (combo == cboStartLength) s.StartButtonLengthMode = (SizeModeOption)i;
            else if (combo == cboStartThickness) s.StartButtonThicknessMode = (SizeModeOption)i;
            else if (combo == cboStartStretch) s.StartButtonStretchMode = (StretchModeOption)i;
            else if (combo == cboStartAnchor) s.StartButtonAnchor = (VerticalAnchorOption)i;
            else if (combo == cboShowDesktopPosition) s.ShowDesktopPosition = (ShowDesktopPositionOption)i;
            else if (combo == cboClockOrientation) s.ClockOrientation = (ElementOrientationOption)i;
            else if (combo == cboClockItalic) s.ClockFontItalic = (ClockItalicOption)i;
            else if (combo == cboClockFont) s.ClockFontFamily = FontChoices.Selected(combo) ?? s.ClockFontFamily;
            else if (combo == cboClockWeight) s.ClockFontWeight = i * 100;
            else if (combo == cboTaskWidth) s.TaskButtonWidthPx = i == 0 ? 0 : Math.Round(sldTaskWidth.Value); // UX-014

            UpdateTexts();
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

            txtStartLength.Text = $"{Math.Round(s.StartButtonLengthPx)} px";
            txtStartThickness.Text = $"{Math.Round(s.StartButtonThicknessPx)} px";
            rowStartLengthPx.IsEnabled = s.StartButtonLengthMode == SizeModeOption.Custom;
            rowStartThicknessPx.IsEnabled = s.StartButtonThicknessMode == SizeModeOption.Custom;

            // Position only matters when the button doesn't fill the taskbar's height.
            bool fills = s.StartButtonOrientation != ElementOrientationOption.Horizontal && s.StartButtonLengthMode == SizeModeOption.Automatic;
            cboStartAnchor.IsEnabled = !fills;

            // UX-014
            rowTaskWidthPx.IsEnabled = s.TaskButtonWidthPx > 0;
            txtTaskWidth.Text = $"{Math.Round(sldTaskWidth.Value)} px";
            txtAppWidth.Text = $"{Math.Round(sldAppWidth.Value)} px";
            rowAppWidthPx.IsEnabled = cboAppWidth.SelectedIndex == 4;

            txtClockSize.Text = s.ClockFontSize > 0 ? $"{Math.Round(s.ClockFontSize)} px" : same;
            txtClockSpacing.Text = s.ClockLineSpacing > 0 ? "× " + s.ClockLineSpacing.ToString("0.00", CultureInfo.CurrentCulture) : same;
        }
    }
}
