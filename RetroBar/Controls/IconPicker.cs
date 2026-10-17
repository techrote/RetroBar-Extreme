using System;
using System.IO;
using System.Windows;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-015): pick a PNG and copy it into the icons folder. Returns the stored
    // name, or null if cancelled or unusable (the user is told why).
    public static class IconPicker
    {
        public static string Choose(Window owner)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Application.Current.TryFindResource("uxui_icon_filter") as string ?? "PNG images (*.png)|*.png",
                DefaultExt = ".png",
            };
            if (dialog.ShowDialog(owner) != true)
            {
                return null;
            }

            try
            {
                return CustomIcons.Import(dialog.FileName);
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                string format = Application.Current.TryFindResource("uxui_icon_failed") as string ?? "This icon can't be used: {0}";
                MessageBox.Show(owner, string.Format(format, e.Message), "RetroBar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }
    }
}
