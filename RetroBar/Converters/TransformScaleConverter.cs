using System;
using System.Globalization;
using System.Windows.Data;
using RetroBar.Utilities;

namespace RetroBar.Converters
{
    // UXUI-Extreme (UX-006): bound to Settings.TaskbarScale (so it re-evaluates when the scale
    // changes) but returns the scale the taskbar should actually be stretched by: 1 in native
    // sizing mode, TaskbarScale in stretch mode. Mode changes reopen the taskbars, which rebinds.
    [ValueConversion(typeof(double), typeof(double))]
    public class TransformScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ScaleModel.TransformScale;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
