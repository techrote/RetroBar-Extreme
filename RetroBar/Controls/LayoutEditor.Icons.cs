using System.Windows;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-015): the Start button's custom PNG icon, on the Layout tab.
    public partial class LayoutEditor
    {
        private void ShowStartIcon()
        {
            string icon = Settings.Instance.StartButtonIcon;
            txtStartIcon.Text = string.IsNullOrEmpty(icon) ? Text("uxui_icon_theme_current") : icon;
        }

        private void StartIconChoose_OnClick(object sender, RoutedEventArgs e)
        {
            string icon = IconPicker.Choose(Window.GetWindow(this));
            if (icon != null)
            {
                Settings.Instance.StartButtonIcon = icon;
                ShowStartIcon();
            }
        }

        private void StartIconReset_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Instance.StartButtonIcon = "";
            ShowStartIcon();
        }
    }
}
