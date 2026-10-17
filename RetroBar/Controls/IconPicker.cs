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
        private static string _lastFolder;

        // UX-017: the icon pack shipped beside RetroBar (Extras\Icons), where the dialog first opens.
        public static string PackFolder(string kind)
        {
            string folder = Path.Combine(Path.GetDirectoryName(ExePath.GetExecutablePath()) ?? "", "Extras", "Icons", kind);
            return Directory.Exists(folder) ? folder : null;
        }

        // kind: the icon pack folder to start in, such as @"start\16px" or @"toggle\100%".
        public static string Choose(Window owner, string kind = null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Application.Current.TryFindResource("uxui_icon_filter") as string ?? "PNG images (*.png)|*.png",
                DefaultExt = ".png",
                InitialDirectory = _lastFolder ?? (kind != null ? PackFolder(kind) : null) ?? "",
            };
            if (dialog.ShowDialog(owner) != true)
            {
                return null;
            }

            try
            {
                _lastFolder = Path.GetDirectoryName(dialog.FileName);
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
