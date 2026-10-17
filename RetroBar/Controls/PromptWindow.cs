using System.Windows;
using System.Windows.Controls;

namespace RetroBar.Controls
{
    // UXUI-Extreme: a one-line text prompt (naming a texture preset), without a XAML file.
    public static class PromptWindow
    {
        // The entered text, or null if cancelled.
        public static string Ask(Window owner, string title, string label, string initial = "")
        {
            string result = null;
            var box = new TextBox { Text = initial ?? "", MinWidth = 260, Margin = new Thickness(0, 4, 0, 8) };
            var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 70, Margin = new Thickness(0, 0, 6, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 70 };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            var panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock { Text = label });
            panel.Children.Add(box);
            panel.Children.Add(buttons);

            var window = new Window
            {
                Title = title,
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Owner = owner,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            };
            ok.Click += (s, e) =>
            {
                result = box.Text;
                window.DialogResult = true;
            };
            window.Loaded += (s, e) =>
            {
                box.Focus();
                box.SelectAll();
            };
            return window.ShowDialog() == true && !string.IsNullOrWhiteSpace(result) ? result.Trim() : null;
        }
    }
}
