using ManagedShell.AppBar;
using ManagedShell.WindowsTasks;
using RetroBar.Utilities;
using System;
using System.Globalization;
using System.Windows.Data;

namespace RetroBar.Converters
{
    // Adapter around the established TaskButtonWidthConverter, not a second sizing formula.
    // The data context may be a single native window or a bundle representative.
    public sealed class TaskBundleButtonWidthConverter : IMultiValueConverter
    {
        private readonly TaskButtonWidthConverter _perWindow = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 9 || values[1] is not double baseWidth) return Binding.DoNothing;
            ApplicationWindow window = values[0] switch
            {
                TaskBundleEntry entry => entry.Representative,
                ApplicationWindow app => app,
                _ => null
            };
            int extra = values[2] is int ex ? ex : 0;
            int columns = values[3] is int n ? n : 0;
            AppBarEdge edge = values[8] is AppBarEdge dockingEdge ? dockingEdge : Settings.Instance.Edge;
            // index=0 is a deterministic fallback; each child is still independently sized.
            object requested = _perWindow.Convert(new object[] {
                0, baseWidth, extra, columns, window, values[4], values[5], values[6], values[7], edge
            }, targetType, null, culture);
            // Inline child indentation consumes 8 DIPs of a vertical taskbar.
            // Even explicit wide per-app overrides cannot overflow the fixed entry.
            if (parameter is string hint && hint == "InlineChild" &&
                (edge == AppBarEdge.Left || edge == AppBarEdge.Right) && requested is double width)
                return Math.Min(width, Math.Max(0, baseWidth - 8));
            return requested;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
