using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-016): the notification area toggle's custom image and margins.
    public partial class LayoutEditor
    {
        private void ShowToggleSettings()
        {
            string icon = Settings.Instance.TrayToggleIcon;
            txtToggleIcon.Text = string.IsNullOrEmpty(icon) ? Text("uxui_icon_theme_current") : icon;
            txtToggleMargin.Text = Settings.Instance.TrayToggleMargin;
        }

        private void ToggleIconChoose_OnClick(object sender, RoutedEventArgs e)
        {
            string icon = IconPicker.Choose(Window.GetWindow(this), @"toggle\100%");
            if (icon != null)
            {
                Settings.Instance.TrayToggleIcon = icon;
                ShowToggleSettings();
            }
        }

        private void ToggleIconReset_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Instance.TrayToggleIcon = "";
            ShowToggleSettings();
        }

        private void TxtToggleMargin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading)
            {
                return;
            }

            if (NotifyIconList.TryParseMargin(txtToggleMargin.Text, out _))
            {
                txtToggleMargin.ClearValue(Control.BorderBrushProperty);
                Settings.Instance.TrayToggleMargin = txtToggleMargin.Text.Trim();
            }
            else
            {
                txtToggleMargin.BorderBrush = Brushes.Red;
            }
        }
    }
}
