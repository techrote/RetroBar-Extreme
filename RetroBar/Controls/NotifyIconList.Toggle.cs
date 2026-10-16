using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-016): the notification area's expand/collapse toggle. Optionally a user
    // PNG in place of the theme's drawing (mirrored while expanded, so one arrow image works
    // both ways), and custom margins: the space kept clear around it.
    public partial class NotifyIconList
    {
        private static bool IsToggleSetting(string name) =>
            name == nameof(Settings.TrayToggleIcon) ||
            name == nameof(Settings.TrayToggleFlip) ||
            name == nameof(Settings.TrayToggleCustomMargin) ||
            name == nameof(Settings.TrayToggleMargin) ||
            name == nameof(Settings.TaskbarScale) ||
            name == nameof(Settings.ScalingMode);

        private void ApplyToggleOverrides()
        {
            var s = Settings.Instance;
            double unit = ScaleModel.IsNative ? s.TaskbarScale : 1.0;

            if (CustomIcons.Load(s.TrayToggleIcon) is BitmapSource image)
            {
                NotifyIconToggleButton.Template = ImageTemplate(image, unit, s.TrayToggleFlip);
            }
            else
            {
                NotifyIconToggleButton.ClearValue(TemplateProperty);
            }

            if (s.TrayToggleCustomMargin && TryParseMargin(s.TrayToggleMargin, out Thickness margin))
            {
                NotifyIconToggleButton.Margin = new Thickness(margin.Left * unit, margin.Top * unit, margin.Right * unit, margin.Bottom * unit);
            }
            else
            {
                NotifyIconToggleButton.ClearValue(MarginProperty);
            }
        }

        // The image at its own size (1 px = 1 DIP at 100%), centred, mirrored when checked.
        private static ControlTemplate ImageTemplate(BitmapSource image, double unit, bool flip)
        {
            var template = new ControlTemplate(typeof(ToggleButton));

            var root = new FrameworkElementFactory(typeof(Border));
            root.SetValue(Border.BackgroundProperty, Brushes.Transparent); // whole area clickable

            var picture = new FrameworkElementFactory(typeof(Image));
            picture.Name = "ToggleImage";
            picture.SetValue(Image.SourceProperty, image);
            picture.SetValue(FrameworkElement.WidthProperty, Math.Round(image.PixelWidth * unit));
            picture.SetValue(FrameworkElement.HeightProperty, Math.Round(image.PixelHeight * unit));
            picture.SetValue(Image.StretchProperty, Stretch.Uniform);
            picture.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            picture.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            picture.SetValue(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
            // Crisp at whole-number scales (pixel art stays pixel art), smooth otherwise.
            picture.SetValue(RenderOptions.BitmapScalingModeProperty, unit == Math.Round(unit) ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            root.AppendChild(picture);
            template.VisualTree = root;

            if (flip)
            {
                var expanded = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
                expanded.Setters.Add(new Setter(UIElement.RenderTransformProperty, new ScaleTransform(-1, 1), "ToggleImage"));
                template.Triggers.Add(expanded);
            }

            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.7, "ToggleImage"));
            template.Triggers.Add(pressed);

            template.Seal();
            return template;
        }

        // "2,0,2,0" (left, top, right, bottom), "2,0" (left/right, top/bottom) or "2".
        public static bool TryParseMargin(string text, out Thickness margin)
        {
            margin = new Thickness();
            double[] parts = (text ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v >= -50 && v <= 200 ? v : double.NaN)
                .ToArray();
            if (parts.Length == 0 || parts.Any(double.IsNaN))
            {
                return false;
            }

            margin = parts.Length switch
            {
                1 => new Thickness(parts[0]),
                2 => new Thickness(parts[0], parts[1], parts[0], parts[1]),
                4 => new Thickness(parts[0], parts[1], parts[2], parts[3]),
                _ => new Thickness(double.NaN),
            };
            return !double.IsNaN(margin.Left);
        }
    }
}
