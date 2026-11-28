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

            // Preserve the old DockPanel constraints exactly for horizontal and opt-out
            // layouts. Only probe unbounded natural widths when side packing is eligible.
            if (Orientation == Orientation.Horizontal ||
                !PackBesideStart || double.IsInfinity(availableSize.Width) ||
                Start.Visibility != Visibility.Visible || Quick.Visibility != Visibility.Visible)
            {
                return MeasureSequential(availableSize);
            }

            Start.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Quick.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size startNatural = Start.DesiredSize;
            Size quickNatural = Quick.DesiredSize;

            // A visible StartArea can contain only collapsed controls (e.g. on a secondary
            // monitor). Do not reserve a phantom gutter for a zero-sized section.
            if (startNatural.Width > 0 && startNatural.Height > 0 &&
                quickNatural.Width > 0 && quickNatural.Height > 0 &&
                startNatural.Width + quickNatural.Width + PackedGap + FitReserve <= availableSize.Width &&
                Math.Max(startNatural.Height, quickNatural.Height) <= availableSize.Height)
            {
                Start.Measure(new Size(startNatural.Width, availableSize.Height));
                double remaining = Math.Max(0, availableSize.Width - Start.DesiredSize.Width - PackedGap);
                Quick.Measure(new Size(remaining, availableSize.Height));
                _packed = true;
                return new Size(Start.DesiredSize.Width + PackedGap + Quick.DesiredSize.Width,
                    Math.Max(Start.DesiredSize.Height, Quick.DesiredSize.Height));
            }

            return MeasureSequential(availableSize);
        }

        // Match the legacy sequential Dock.Left/Dock.Top measure pass. The first
        // section sees the available rectangle, and the second sees its remainder.
        // In particular, a side toolbar still has the full width for icon wrapping.
        private Size MeasureSequential(Size availableSize)
        {
            First.Measure(availableSize);
            if (Orientation == Orientation.Horizontal)
            {
                Second.Measure(new Size(Math.Max(0, availableSize.Width - First.DesiredSize.Width),
                    availableSize.Height));
                return new Size(First.DesiredSize.Width + Second.DesiredSize.Width,
                    Math.Max(First.DesiredSize.Height, Second.DesiredSize.Height));
            }

            Second.Measure(new Size(availableSize.Width,
                Math.Max(0, availableSize.Height - First.DesiredSize.Height)));
            return new Size(Math.Max(First.DesiredSize.Width, Second.DesiredSize.Width),
                First.DesiredSize.Height + Second.DesiredSize.Height);
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
