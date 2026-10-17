using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RetroBar.Converters
{
    // UXUI-Extreme: lets a toolbar's grip (the dotted divider shown while the taskbar is unlocked) sit
    // in a thin cell of fixed size, so locking the taskbar changes nothing about how everything else
    // is arranged.
    // parameter: "width" / "height" (the cell's size), "h" / "v" (the grip's alignment).
    public class ThumbOverlayConverter : IValueConverter
    {
        // The grip is a thin strip, always the same size, whether the taskbar is locked (the grip is hidden
        // but its strip stays) or unlocked, so locking never moves anything.
        public const double GripThickness = 3;

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool horizontal = !(value is Orientation orientation) || orientation == Orientation.Horizontal;

            switch (parameter as string)
            {
                case "width": return horizontal ? GripThickness : double.NaN;
                case "height": return horizontal ? double.NaN : GripThickness;
                case "h": return HorizontalAlignment.Stretch;
                case "v": return VerticalAlignment.Stretch;
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
