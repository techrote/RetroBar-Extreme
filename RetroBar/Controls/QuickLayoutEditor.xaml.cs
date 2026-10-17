using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the quick launch area's layout, in the Start/Quick tab.
    public partial class QuickLayoutEditor : UserControl
    {
        private bool _loading;
        private bool _ready;

        public QuickLayoutEditor()
        {
            InitializeComponent();
            _ready = true;
        }

        private void QuickLayoutEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                _loading = true;
                cboQuickH.SelectedIndex = (int)Settings.Instance.QuickLaunchAlignH;
                cboQuickV.SelectedIndex = (int)Settings.Instance.QuickLaunchAlignV;
                if (!txtQuickLength.IsKeyboardFocused)
                {
                    txtQuickLength.Text = Math.Round(Settings.Instance.QuickLaunchLength).ToString(CultureInfo.CurrentCulture);
                }
                _loading = false;
            }
        }

        private void Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !_ready || !(sender is ComboBox combo) || combo.SelectedIndex < 0)
            {
                return;
            }

            if (combo == cboQuickH) Settings.Instance.QuickLaunchAlignH = (TaskButtonAlignOption)combo.SelectedIndex;
            else if (combo == cboQuickV) Settings.Instance.QuickLaunchAlignV = (VerticalPlaceOption)combo.SelectedIndex;
        }

        // 0 = as long as its icons; the grip next to the task buttons sets it by dragging.
        private void TxtQuickLength_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || !_ready || !txtQuickLength.IsKeyboardFocused)
            {
                return;
            }

            if (double.TryParse(txtQuickLength.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) && value >= 0 && value <= 2000)
            {
                txtQuickLength.ClearValue(Control.BorderBrushProperty);
                Settings.Instance.QuickLaunchLength = Math.Round(value);
            }
            else
            {
                txtQuickLength.BorderBrush = System.Windows.Media.Brushes.Red;
            }
        }

        private void QuickLengthAuto_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Instance.QuickLaunchLength = 0;
            txtQuickLength.Text = "0";
        }
    }
}
