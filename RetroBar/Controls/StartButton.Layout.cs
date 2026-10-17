#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-012): Start button layout. Optionally turned to run vertically on a
    // horizontal taskbar, made a given length (automatic = fill the taskbar's height) and
    // thickness, and with its icon and/or "start" text hidden. The theme's own button is kept:
    // - Keep ends (default): the button is made longer itself. Image buttons (XP family) get a
    //   copy of their image with a middle column repeated (SlicedImage), so the rounded tip
    //   isn't stretched; drawn buttons (classic) just widen.
    // - Scale: the whole button is scaled.
    public partial class StartButton
    {
        private const string ImageKey = "StartButtonImage";
        private const double EmptyButtonLength = 48; // when neither icon nor text is shown

        // The button's look when the picture is the user's: the image (or its normal / hover /
        // pressed thirds) behind the same icon and text.
        private static ControlTemplate CustomTemplate(int states)
        {
            string height = states == 3 ? "0.3333333" : "1";
            string hover = states == 3 ? "0,0.3333333,1,0.3333333" : "0,0,1,1";
            string pressed = states == 3 ? "0,0.6666667,1,0.3333333" : "0,0,1,1";
            string xaml = $@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'>
  <Border x:Name='StartButtonBorder' HorizontalAlignment='Left'>
    <Border.Background>
      <ImageBrush ImageSource='{{DynamicResource StartButtonImage}}' ViewboxUnits='RelativeToBoundingBox' Viewbox='0,0,1,{height}' />
    </Border.Background>
    <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' />
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property='IsMouseOver' Value='True'>
      <Setter TargetName='StartButtonBorder' Property='Background'>
        <Setter.Value><ImageBrush ImageSource='{{DynamicResource StartButtonImage}}' ViewboxUnits='RelativeToBoundingBox' Viewbox='{hover}' /></Setter.Value>
      </Setter>
    </Trigger>
    <Trigger Property='IsChecked' Value='True'>
      <Setter TargetName='StartButtonBorder' Property='Background'>
        <Setter.Value><ImageBrush ImageSource='{{DynamicResource StartButtonImage}}' ViewboxUnits='RelativeToBoundingBox' Viewbox='{pressed}' /></Setter.Value>
      </Setter>
    </Trigger>
    <Trigger Property='IsPressed' Value='True'>
      <Setter TargetName='StartButtonBorder' Property='Background'>
        <Setter.Value><ImageBrush ImageSource='{{DynamicResource StartButtonImage}}' ViewboxUnits='RelativeToBoundingBox' Viewbox='{pressed}' /></Setter.Value>
      </Setter>
    </Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>";
            return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        // availableLength: room along the taskbar's height for the button (the taskbar works
        // this out, e.g. less when Show desktop sits under the Start button).
        public void ApplyLayout(double availableLength)
        {
            var s = Settings.Instance;

            // UX-015: a user PNG in place of the theme's Start icon.
            if (CustomIcons.Load(s.StartButtonIcon, CustomIcons.DevicePixels(StartIconImage)) is BitmapSource customIcon)
            {
                StartIconImage.Source = customIcon;
            }
            else
            {
                StartIconImage.ClearValue(Image.SourceProperty);
            }
            StartIconImage.Visibility = s.StartButtonShowIcon ? Visibility.Visible : Visibility.Collapsed;
            StartLabelText.Visibility = s.StartButtonShowText ? Visibility.Visible : Visibility.Collapsed;

            // Icon (and label) position on the button.
            var star = new GridLength(1, GridUnitType.Star);
            switch (s.StartButtonIconAlign)
            {
                case TaskButtonAlignOption.Centre:
                    StartLeadColumn.Width = star; StartLabelColumn.Width = GridLength.Auto; StartTrailColumn.Width = star;
                    break;
                case TaskButtonAlignOption.Right:
                    StartLeadColumn.Width = star; StartLabelColumn.Width = GridLength.Auto; StartTrailColumn.Width = new GridLength(0);
                    break;
                default:
                    StartLeadColumn.Width = new GridLength(0); StartLabelColumn.Width = star; StartTrailColumn.Width = new GridLength(0);
                    break;
            }
            if (s.StartButtonIconAlign != TaskButtonAlignOption.Left && !s.StartButtonShowText)
            {
                StartIconImage.Margin = new Thickness(0); // the theme's gap after the icon would push it off centre
            }
            else
            {
                StartIconImage.ClearValue(FrameworkElement.MarginProperty);
            }
            bool empty = !s.StartButtonShowIcon && !s.StartButtonShowText;

            bool horizontalBar = Host == null || Host.Orientation == Orientation.Horizontal;
            bool rotated = horizontalBar && s.StartButtonOrientation != ElementOrientationOption.Horizontal;
            bool customLength = s.StartButtonLengthMode == SizeModeOption.Custom;
            bool customThickness = s.StartButtonThicknessMode == SizeModeOption.Custom;
            bool keepEnds = s.StartButtonStretchMode == StretchModeOption.KeepEnds;

            // Back to the theme's button, then measure it as it would be with no changes.
            Start.ClearValue(LayoutTransformProperty);
            StartContentGrid.ClearValue(MinWidthProperty);
            Start.ClearValue(MinWidthProperty);
            Start.ClearValue(WidthProperty);
            Start.ClearValue(HeightProperty);
            Start.ClearValue(TemplateProperty);
            Start.Resources.Remove(ImageKey);
            StartIconImage.ClearValue(LayoutTransformProperty);

            // The Vista orb is a round graphic drawn in its own floating window: turning or
            // stretching it only distorts it, so it keeps its own shape (Position still applies).
            bool floatingOrb = Application.Current.TryFindResource("UseFloatingStartButton") as bool? ?? false;

            // A user's PNG in place of the theme's button picture (not for the floating orb).
            BitmapSource? customImage = floatingOrb ? null : CustomIcons.Load(s.StartButtonImage);
            if (customImage != null)
            {
                bool stacked = s.StartButtonImageStates == StartImageStatesOption.Three && customImage.PixelHeight >= 3;
                int states = stacked ? 3 : 1;
                double scaleUnit = ScaleModel.IsNative ? s.TaskbarScale : 1.0;
                Start.Template = CustomTemplate(states);
                Start.Resources[ImageKey] = customImage;
                Start.Height = Math.Max(8, customImage.PixelHeight / states * scaleUnit);
                Start.MinWidth = Math.Max(8, customImage.PixelWidth * scaleUnit);
            }

            if (floatingOrb || (customImage == null && !rotated && !customLength && !customThickness && !empty))
            {
                return;
            }

            BitmapSource? image = customImage ?? Application.Current.TryFindResource(ImageKey) as BitmapSource; // incl. UX-010 recolouring

            Thickness margin = Start.Margin;
            var unlimited = new Size(double.PositiveInfinity, double.PositiveInfinity);

            // The theme's button as it is: measured first, before anything below changes it.
            Start.Measure(unlimited);
            double naturalLength = Start.DesiredSize.Width - margin.Left - margin.Right;
            double naturalThickness = Start.DesiredSize.Height - margin.Top - margin.Bottom;

            // Padding and borders around the content: measured with the content forced wide and
            // the theme's minimum width off (XP's minimum doesn't widen its image, which is only
            // as wide as the content). Undone straight after; the real layout pass re-measures.
            const double probe = 400;
            Start.MinWidth = 0;
            StartContentGrid.MinWidth = probe;
            StartContentGrid.InvalidateMeasure();
            Start.InvalidateMeasure();
            Start.Measure(unlimited);
            double chrome = Math.Max(0, Start.DesiredSize.Width - margin.Left - margin.Right - probe);
            StartContentGrid.ClearValue(MinWidthProperty);
            Start.ClearValue(MinWidthProperty);
            StartContentGrid.InvalidateMeasure();
            Start.InvalidateMeasure();

            // With nothing inside, the button would shrink to its padding (XP: to nothing), so
            // start from the image's own width, or a small drawn button.
            if (empty)
            {
                naturalLength = image != null ? image.Width : chrome + EmptyButtonLength;
            }

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

            double lengthScale, thicknessScale;
            if (keepEnds)
            {
                // Make the button itself that long; nothing is scaled along its length.
                Start.MinWidth = 0; // themes set a minimum (XP: 99px) that would stop it getting shorter
                // Some themes fix the width (Longhorn: 78px); the content can't make those longer.
                double themeWidth = (double)Start.GetValue(WidthProperty);
                if (!double.IsNaN(themeWidth))
                {
                    Start.Width = length;
                }
                StartContentGrid.MinWidth = Math.Max(0, length - chrome);
                if (image != null && image.PixelWidth > 2 && Math.Abs(length - naturalLength) > 0.5)
                {
                    int pixels = (int)Math.Round(image.PixelWidth * length / naturalLength);
                    BitmapSource? sliced = SlicedImage.StretchWidth(image, Math.Max(3, pixels));
                    if (sliced != null)
                    {
                        Start.Resources[ImageKey] = sliced;
                    }
                }
                lengthScale = 1;
                thicknessScale = customThickness ? Math.Max(8, s.StartButtonThicknessPx * unit) / naturalThickness : 1;
            }
            else
            {
                if (empty)
                {
                    StartContentGrid.MinWidth = Math.Max(0, naturalLength - chrome);
                }
                lengthScale = length / naturalLength;
                double thickness = customThickness ? Math.Max(8, s.StartButtonThicknessPx * unit) : naturalThickness * lengthScale;
                thicknessScale = thickness / naturalThickness;
            }

            if (!rotated && Math.Abs(lengthScale - 1) < 0.001 && Math.Abs(thicknessScale - 1) < 0.001)
            {
                return;
            }

            // The symbol keeps its own size: undo the stretch on it (in its own, unturned directions).
            if (Math.Abs(lengthScale - 1) > 0.001 || Math.Abs(thicknessScale - 1) > 0.001)
            {
                StartIconImage.LayoutTransform = new ScaleTransform(1 / lengthScale, 1 / thicknessScale);
            }

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
