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

        // UXUI-Extreme: which groups this copy of the editor shows. "All" is everything; the Start/Quick tab
        // and the Layout / Icons tabs each show their own part, from the same settings.
        public static readonly DependencyProperty SectionsProperty = DependencyProperty.Register(
            nameof(Sections), typeof(string), typeof(LayoutEditor), new PropertyMetadata("All", (d, e) => ((LayoutEditor)d).ApplySections()));

        public string Sections
        {
            get => (string)GetValue(SectionsProperty);
            set => SetValue(SectionsProperty, value);
        }

        private static Visibility Show(bool show) => show ? Visibility.Visible : Visibility.Collapsed;

        private void ApplySections()
        {
            bool startQuick = Sections == "StartQuick", main = Sections == "Main", all = !startQuick && !main;
            grpStart.Visibility = Show(all || startQuick);
            grpDesktop.Visibility = Show(all || startQuick);
            noteStart.Visibility = Show(all || startQuick);
            grpToggle.Visibility = Show(all || main);
            grpTaskButtons.Visibility = Show(all || main);
        }
        private bool _ready; // set once every control exists (sliders raise ValueChanged while the XAML loads)

        public LayoutEditor()
        {
            InitializeComponent();
            _ready = true;
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
            cboTogglePlacement.SelectedIndex = (int)s.TrayTogglePlacement;

            ShowToggleSettings(); // UX-016

            // UX-014
            cboTaskWidth.SelectedIndex = s.TaskButtonWidthPx > 0 ? 1 : 0;
            cboTaskAlign.SelectedIndex = (int)s.TaskButtonAlign;
            cboTaskVAlign.SelectedIndex = (int)s.TaskButtonVAlign;
            sldTaskWidth.Value = s.TaskButtonWidthPx > 0 ? s.TaskButtonWidthPx : 160;
            LoadApps();
            LoadRows();
            txtDesktopMargin.Text = s.ShowDesktopMargin;

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
            else if (combo == cboTogglePlacement) s.TrayTogglePlacement = (TrayTogglePlacementOption)i;
            else if (combo == cboTaskAlign) s.TaskButtonAlign = (TaskButtonAlignOption)i;
            else if (combo == cboTaskVAlign) s.TaskButtonVAlign = (VerticalPlaceOption)i;
            else if (combo == cboTaskWidth) s.TaskButtonWidthPx = i == 0 ? 0 : Math.Round(sldTaskWidth.Value); // UX-014

            UpdateTexts();
        }

        private void TxtDesktopMargin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            if (NotifyIconList.TryParseMargin(txtDesktopMargin.Text, out _))
            {
                txtDesktopMargin.ClearValue(Control.BorderBrushProperty);
                Settings.Instance.ShowDesktopMargin = txtDesktopMargin.Text.Trim();
            }
            else
            {
                txtDesktopMargin.BorderBrush = System.Windows.Media.Brushes.Red;
            }
        }

        // The width boxes take exact numbers (the sliders are only coarse); typing one switches to Custom.
        private void TxtTaskWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || !_ready || !txtTaskWidth.IsKeyboardFocused)
            {
                return;
            }

            if (int.TryParse(txtTaskWidth.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value) && value >= 16 && value <= 2000)
            {
                txtTaskWidth.ClearValue(Control.BorderBrushProperty);
                _loading = true;
                cboTaskWidth.SelectedIndex = 1;
                sldTaskWidth.Value = Math.Min(value, sldTaskWidth.Maximum);
                _loading = false;
                Settings.Instance.TaskButtonWidthPx = value;
            }
            else
            {
                txtTaskWidth.BorderBrush = System.Windows.Media.Brushes.Red;
            }
        }

        private void TxtAppWidth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || !_ready || _updatingApp || !txtAppWidth.IsKeyboardFocused || SelectedApp == null)
            {
                return;
            }

            if (int.TryParse(txtAppWidth.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value) && value >= 16 && value <= 2000)
            {
                txtAppWidth.ClearValue(Control.BorderBrushProperty);
                _updatingApp = true;
                cboAppWidth.SelectedIndex = APP_CUSTOM;
                sldAppWidth.Value = Math.Min(value, sldAppWidth.Maximum);
                _updatingApp = false;
                SaveApp(value);
            }
            else
            {
                txtAppWidth.BorderBrush = System.Windows.Media.Brushes.Red;
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTexts();
        }

        private void UpdateTexts()
        {
            if (_loading || !_ready)
            {
                return;
            }

            var s = Settings.Instance;
            string same = Text("uxui_layout_same_as_taskbar");

            txtStartLength.Text = $"{Math.Round(s.StartButtonLengthPx)} px";
            txtStartThickness.Text = $"{Math.Round(s.StartButtonThicknessPx)} px";
            txtStartGap.Text = $"{Math.Round(s.StartButtonEdgeGap)} px";
            rowStartLengthPx.IsEnabled = s.StartButtonLengthMode == SizeModeOption.Custom;
            rowStartThicknessPx.IsEnabled = s.StartButtonThicknessMode == SizeModeOption.Custom;

            // Position only matters when the button doesn't fill the taskbar's height.
            bool fills = s.StartButtonOrientation != ElementOrientationOption.Horizontal && s.StartButtonLengthMode == SizeModeOption.Automatic;
            cboStartAnchor.IsEnabled = !fills;

            // UX-014
            rowTaskWidthPx.IsEnabled = s.TaskButtonWidthPx > 0;
            if (!txtTaskWidth.IsKeyboardFocused) txtTaskWidth.Text = Math.Round(sldTaskWidth.Value).ToString(CultureInfo.CurrentCulture);
            if (!txtAppWidth.IsKeyboardFocused) txtAppWidth.Text = Math.Round(sldAppWidth.Value).ToString(CultureInfo.CurrentCulture);
            rowAppWidthPx.IsEnabled = cboAppWidth.SelectedIndex == 4;

        }
    }
}
