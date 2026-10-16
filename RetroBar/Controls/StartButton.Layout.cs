#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-012): Start button layout. Optionally turned to run vertically on a
    // horizontal taskbar, scaled to a length (automatic = fill the taskbar's height) and
    // thickness (automatic = keep proportions), and with its icon and/or "start" text hidden.
    // The theme's own button is kept and transformed, so every theme (image or drawn) works.
    public partial class StartButton
    {
        // availableLength: room along the taskbar's height for the button (the taskbar works
        // this out, e.g. less when Show desktop sits under the Start button).
        public void ApplyLayout(double availableLength)
        {
            var s = Settings.Instance;

            StartIconImage.Visibility = s.StartButtonShowIcon ? Visibility.Visible : Visibility.Collapsed;
            StartLabelText.Visibility = s.StartButtonShowText ? Visibility.Visible : Visibility.Collapsed;

            bool horizontalBar = Host == null || Host.Orientation == Orientation.Horizontal;
            bool rotated = horizontalBar && s.StartButtonOrientation != ElementOrientationOption.Horizontal;
            bool customLength = s.StartButtonLengthMode == SizeModeOption.Custom;
            bool customThickness = s.StartButtonThicknessMode == SizeModeOption.Custom;

            // Natural size: the theme's button as it would be with no transform.
            Start.ClearValue(LayoutTransformProperty);
            if (!rotated && !customLength && !customThickness)
            {
                return;
            }

            Start.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Thickness margin = Start.Margin;
            double naturalLength = Start.DesiredSize.Width - margin.Left - margin.Right;
            double naturalThickness = Start.DesiredSize.Height - margin.Top - margin.Bottom;
            if (naturalLength <= 0 || naturalThickness <= 0)
            {
                return;
            }

            // Custom sizes are px at 100%; native sizing draws at real size, stretch mode scales
            // the whole taskbar afterwards.
            double unit = ScaleModel.IsNative ? s.TaskbarScale : 1.0;

            double length;
            if (customLength)
            {
                length = Math.Max(8, s.StartButtonLengthPx * unit);
            }
            else if (rotated && availableLength > 0)
            {
                length = Math.Max(8, availableLength - margin.Top - margin.Bottom);
            }
            else
            {
                length = naturalLength;
            }

            double lengthScale = length / naturalLength;
            double thickness = customThickness ? Math.Max(8, s.StartButtonThicknessPx * unit) : naturalThickness * lengthScale;
            double thicknessScale = thickness / naturalThickness;

            var transform = new TransformGroup();
            if (rotated)
            {
                // After turning, the button's length runs down the screen and its thickness across.
                transform.Children.Add(new RotateTransform(s.StartButtonOrientation == ElementOrientationOption.VerticalUp ? -90 : 90));
                transform.Children.Add(new ScaleTransform(thicknessScale, lengthScale));
            }
            else
            {
                transform.Children.Add(new ScaleTransform(lengthScale, thicknessScale));
            }
            transform.Freeze();
            Start.LayoutTransform = transform;
        }
    }
}
