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
            txtToggleMargin.Text = Settings.Instance.TrayToggleMargin;
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
