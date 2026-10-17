using System.ComponentModel;
using System.Windows;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: an optional box around each quick launch icon, drawn with the task buttons' own
    // style (the theme's, or the style's fill and borders for button.*): the same fill, borders and
    // hover look as the taskbar buttons next to it.
    public partial class ToolbarButton
    {
        private void HookFill()
        {
            Loaded += (s, e) =>
            {
                Settings.Instance.PropertyChanged += Fill_SettingChanged;
                StyleLayer.Changed += Fill_StyleChanged;
                ApplyFill();
            };
            Unloaded += (s, e) =>
            {
                Settings.Instance.PropertyChanged -= Fill_SettingChanged;
                StyleLayer.Changed -= Fill_StyleChanged;
            };
        }

        private void Fill_SettingChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.QuickLaunchButtonFill) || e.PropertyName == nameof(Settings.Theme))
            {
                Dispatcher.BeginInvoke(new System.Action(ApplyFill));
            }
        }

        private void Fill_StyleChanged(object sender, System.EventArgs e)
        {
            Dispatcher.BeginInvoke(new System.Action(ApplyFill));
        }

        private void ApplyFill()
        {
            if (Settings.Instance.QuickLaunchButtonFill && TryFindResource("TaskButton") is Style taskButton)
            {
                IconButton.Style = taskButton;
                IconButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                IconButton.VerticalContentAlignment = VerticalAlignment.Center;
                // A small box: the icon plus a few pixels each side. The task button template pads its
                // content for text, so pull the icon back out by negative margins.
                ToolbarIcon.Margin = new Thickness(-4, -1, -4, -1);
            }
            else
            {
                IconButton.SetResourceReference(FrameworkElement.StyleProperty, "ToolbarButton");
                IconButton.ClearValue(System.Windows.Controls.Control.HorizontalContentAlignmentProperty);
                IconButton.ClearValue(System.Windows.Controls.Control.VerticalContentAlignmentProperty);
                ToolbarIcon.ClearValue(FrameworkElement.MarginProperty);
            }
        }
    }
}
