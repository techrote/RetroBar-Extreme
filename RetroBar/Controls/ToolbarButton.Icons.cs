using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using ManagedShell.ShellFolders;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-015): a user PNG in place of a quick launch item's own icon. Set from
    // the item's right-click menu (Change icon... / Reset icon); remembered per shortcut.
    public partial class ToolbarButton
    {
        private bool _customIcon;

        private void HookCustomIcon()
        {
            DataContextChanged += (s, e) => ApplyCustomIcon();
            Loaded += (s, e) =>
            {
                Settings.Instance.PropertyChanged += Icons_PropertyChanged;
                ApplyCustomIcon();
            };
            Unloaded += (s, e) => Settings.Instance.PropertyChanged -= Icons_PropertyChanged;
        }

        private void Icons_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.QuickLaunchIcons))
            {
                ApplyCustomIcon();
            }
        }

        private void ApplyCustomIcon()
        {
            BitmapSource icon = DataContext is ShellItem item ? CustomIcons.Load(CustomIcons.QuickLaunchIcon(item.Path), CustomIcons.DevicePixels(ToolbarIcon)) : null;

            if (icon != null)
            {
                ToolbarIcon.Source = icon; // replaces the shortcut-icon binding
                _customIcon = true;
            }
            else if (_customIcon)
            {
                _customIcon = false;
                setIconBinding(); // back to the shortcut's own icon
            }
        }
    }
}
