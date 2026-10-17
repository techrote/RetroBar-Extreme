using System;
using System.Windows;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-015): "Change icon..." from a quick launch item's right-click menu.
    public partial class Toolbar
    {
        private void ChooseIcon(string itemPath)
        {
            // After the shell menu has closed.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string icon = IconPicker.Choose(Window.GetWindow(this));
                if (icon != null)
                {
                    CustomIcons.SetQuickLaunchIcon(itemPath, icon);
                }
            }));
        }
    }
}
