using System;
using System.Windows.Data;
using ManagedShell.AppBar;
using ManagedShell.WindowsTasks;
using RetroBar.Utilities;

namespace RetroBar.Converters
{
    // Computes an individual task button width so a full row fills the taskbar exactly.
    // Every button gets the base (floored) width; the pixels lost to flooring are spread
    // evenly across the row's columns, each affected column getting one extra pixel.
    //
    // Distribution is keyed on the column (index % buttonsPerRow) so widths line up in
    // columns across rows, and a partial last row keeps the same per-column widths.
    //
    // values[0] = item index (ItemsControl.AlternationIndex)
    // values[1] = base button width (double)
    // values[2] = number of columns that get an extra pixel (int)
    // values[3] = buttons per row (int)
    //
    // If the index isn't available the base width is returned, so the layout simply falls
    // back to uniform widths rather than breaking.
    public class TaskButtonWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length < 4 || !(values[1] is double baseWidth))
            {
                return Binding.DoNothing;
            }

            if (values[0] is int index
                && values[2] is int extraCount
                && values[3] is int buttonsPerRow
                && extraCount > 0
                && buttonsPerRow > 0)
            {
                int column = index % buttonsPerRow;

                // Evenly spaced "+1"s: 1 when this column falls on a distribution boundary.
                int extraForColumn = (column + 1) * extraCount / buttonsPerRow - column * extraCount / buttonsPerRow;

                return AppWidth(values, baseWidth + extraForColumn, baseWidth);
            }

            return AppWidth(values, baseWidth, baseWidth);
        }

        // UXUI-Extreme (UX-014): a remembered per-app width.
        // values[4] = the button's ApplicationWindow, values[5] = the rules (so changes re-run this),
        // values[6] = button margins, values[7] = icon-only width, values[8] = default width (both incl. margins).
        // Icon only is always narrow. Other widths apply as set when the row has room; when buttons
        // are being shrunk to fit, a wider app button shrinks with them rather than overflowing.
        private static object AppWidth(object[] values, double width, double baseWidth)
        {
            if (values.Length < 9 || !(values[4] is ApplicationWindow window) ||
                !(values[6] is double margin) || !(values[7] is double iconOnly) || !(values[8] is double defaultWidth) ||
                Settings.Instance.Edge == AppBarEdge.Left || Settings.Instance.Edge == AppBarEdge.Right)
            {
                return width;
            }

            TaskButtonAppRule rule = TaskButtonRules.Find(window);
            if (rule == null || rule.Width == 0)
            {
                return width;
            }

            if (rule.Width == TaskButtonRules.IconOnly)
            {
                return Math.Min(width, iconOnly);
            }

            double unit = ScaleModel.IsNative ? Settings.Instance.TaskbarScale : 1.0;
            double wanted = Math.Round(rule.Width * unit) + margin;
            bool shrunk = baseWidth < defaultWidth - 0.5;
            return wanted <= width || !shrunk ? wanted : width;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
