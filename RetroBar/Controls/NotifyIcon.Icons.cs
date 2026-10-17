using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using RetroBar.Extensions;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: a user PNG in place of one notification area icon, chosen on the Icons tab
    // and remembered by the icon's identifier. Cleared, the icon's own image comes back.
    public partial class NotifyIcon
    {
        private bool _customTrayIcon;

        private void ApplyCustomIcon()
        {
            if (TrayIcon == null)
            {
                return;
            }

            BitmapSource icon = CustomIcons.Load(CustomIcons.TrayIcon(TrayIcon.Identifier), CustomIcons.DevicePixels(NotifyIconImage));
            if (icon != null)
            {
                NotifyIconImage.Source = icon; // replaces the icon binding
                _customTrayIcon = true;
            }
            else if (_customTrayIcon)
            {
                _customTrayIcon = false;
                NotifyIconImage.SetBinding(Image.SourceProperty, new Binding("Icon") { Mode = BindingMode.OneWay });
            }

            applyEffects();
        }
    }
}
