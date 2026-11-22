using System;
using System.Windows;
using System.Windows.Controls;

namespace RetroBar.Controls
{
    /// <summary>
    /// Keeps Start and Quick Launch in the same leading taskbar section. On side taskbars,
    /// place both in one row only when the entire natural-width Quick Launch fits;
    /// otherwise preserve the existing full-width stacked layout and icon wrapping.
    /// Children are never reparented when the layout or order changes.
    /// </summary>
    public sealed class AdaptiveStartQuickPanel : Panel
    {
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            nameof(Orientation), typeof(Orientation), typeof(AdaptiveStartQuickPanel),
            new FrameworkPropertyMetadata(Orientation.Horizontal,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty PackBesideStartProperty = DependencyProperty.Register(
            nameof(PackBesideStart), typeof(bool), typeof(AdaptiveStartQuickPanel),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty QuickLaunchFirstProperty = DependencyProperty.Register(
            nameof(QuickLaunchFirst), typeof(bool), typeof(AdaptiveStartQuickPanel),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        // WPF layout units (DIP); the small reserve prevents rounding from obscuring the final icon.
        private const double PackedGap = 4;
        private const double FitReserve = 2;
        private bool _packed;

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public bool PackBesideStart
        {
            get => (bool)GetValue(PackBesideStartProperty);
            set => SetValue(PackBesideStartProperty, value);
        }

        public bool QuickLaunchFirst
        {
            get => (bool)GetValue(QuickLaunchFirstProperty);
            set => SetValue(QuickLaunchFirstProperty, value);
        }

        private UIElement Start => InternalChildren[0];
        private UIElement Quick => InternalChildren[1];
        private UIElement First => QuickLaunchFirst ? Quick : Start;
        private UIElement Second => QuickLaunchFirst ? Start : Quick;

        protected override Size MeasureOverride(Size availableSize)
        {
            _packed = false;
            if (InternalChildren.Count != 2)
            {
                foreach (UIElement child in InternalChildren)
                {
                    child.Measure(availableSize);
                }
                return Size.Empty;
            }

            if (Orientation == Orientation.Horizontal)
            {
                // Matches two separate Dock.Left sections. The first gets its natural width;
                // the following section sees the remaining space. Keep the taskbar's full height.
                First.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                double remaining = double.IsInfinity(availableSize.Width)
                    ? double.PositiveInfinity
                    : Math.Max(0, availableSize.Width - First.DesiredSize.Width);
                Second.Measure(new Size(remaining, availableSize.Height));
                return new Size(First.DesiredSize.Width + Second.DesiredSize.Width,
                    Math.Max(First.DesiredSize.Height, Second.DesiredSize.Height));
            }

            // First measure at natural width, rather than letting the toolbar wrap prematurely:
            // it must qualify as a complete group before being placed beside Start.
            Start.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Quick.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double startWidth = Start.DesiredSize.Width;
            double quickWidth = Quick.DesiredSize.Width;
            _packed = PackBesideStart &&
                Start.Visibility != Visibility.Collapsed &&
                Quick.Visibility != Visibility.Collapsed &&
                !double.IsInfinity(availableSize.Width) &&
                startWidth + quickWidth + PackedGap + FitReserve <= availableSize.Width;

            if (_packed)
            {
                // Give Quick a finite width even in packed mode. This keeps its own internal
                // layout constraints intact and preserves explicit toolbar size settings.
                double remaining = Math.Max(0, availableSize.Width - startWidth - PackedGap);
                Quick.Measure(new Size(remaining, double.PositiveInfinity));
                return new Size(startWidth + PackedGap + Quick.DesiredSize.Width,
                    Math.Max(Start.DesiredSize.Height, Quick.DesiredSize.Height));
            }

            // A finite *full-sidebar* width is essential here: QuickLaunchPanel can then
            // wrap shortcuts across rows instead of spilling past the side taskbar boundary.
            Start.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            Quick.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            return new Size(Math.Max(Start.DesiredSize.Width, Quick.DesiredSize.Width),
                Start.DesiredSize.Height + Quick.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (InternalChildren.Count != 2)
            {
                foreach (UIElement child in InternalChildren)
                {
                    child.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
                }
                return finalSize;
            }

            if (Orientation == Orientation.Horizontal)
            {
                First.Arrange(new Rect(0, 0, First.DesiredSize.Width, finalSize.Height));
                Second.Arrange(new Rect(First.DesiredSize.Width, 0,
                    Second.DesiredSize.Width, finalSize.Height));
            }
            else if (_packed)
            {
                First.Arrange(new Rect(0, 0, First.DesiredSize.Width, First.DesiredSize.Height));
                Second.Arrange(new Rect(First.DesiredSize.Width + PackedGap, 0,
                    Second.DesiredSize.Width, Second.DesiredSize.Height));
            }
            else
            {
                First.Arrange(new Rect(0, 0, finalSize.Width, First.DesiredSize.Height));
                Second.Arrange(new Rect(0, First.DesiredSize.Height,
                    finalSize.Width, Second.DesiredSize.Height));
            }

            return finalSize;
        }
    }
}
