using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace RetroBar.Converters
{
    // A vertical taskbar fixes group entry width; a horizontal inline group must expand
    // its own width to accommodate additional windows.
    public sealed class TaskBundleEntryWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[1] is not double width) return Binding.DoNothing;
            return values[0] is Orientation orientation && orientation == Orientation.Horizontal
                ? double.NaN : width;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
