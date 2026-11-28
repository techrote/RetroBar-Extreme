using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Controls;

internal static partial class Program
{
    private static void SideQuickLaunchPacking()
    {
        var panel = new AdaptiveStartQuickPanel
        {
            Orientation = Orientation.Vertical,
            PackBesideStart = true,
        };
        // Explicit widths define natural measurement; Left alignment prevents WPF from
        // centring a fixed-width Border within the panel's full-width fallback slot.
        var start = new Border { Width = 76, Height = 28, HorizontalAlignment = HorizontalAlignment.Left };
        var quick = new Border { Width = 90, Height = 24, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(start);
        panel.Children.Add(quick);

        void Layout(double width)
        {
            panel.Measure(new Size(width, 500));
            panel.Arrange(new Rect(0, 0, width, panel.DesiredSize.Height));
        }

        double X(UIElement child) => VisualTreeHelper.GetOffset(child).X;
        double Y(UIElement child) => VisualTreeHelper.GetOffset(child).Y;
        bool Near(double a, double b) => Math.Abs(a - b) < 0.01;

        // Exact threshold = 76 + 90 + 4 DIP gap + 2 DIP rounding reserve.
        Layout(172);
        Check(Near(X(start), 0) && Near(Y(start), 0), "packed: Start stays at top-left");
        Check(Near(X(quick), 80) && Near(Y(quick), 0), "packed: Quick Launch occupies spare space on Start's row");
        Check(Near(panel.DesiredSize.Height, 28), "packed: height is that of one row");

        Layout(171);
        Check(Near(X(quick), 0) && Near(Y(quick), 28), $"one DIP below fit threshold stacks complete toolbar (x={X(quick)}, y={Y(quick)})");
        Check(Near(panel.DesiredSize.Height, 52), "stacked: each section retains its own height");
        Check(ReferenceEquals(VisualTreeHelper.GetParent(quick), panel),
            "switching layout does not reparent or unload shortcut controls");

        Layout(220);
        Check(Near(X(quick), 80) && Near(Y(quick), 0), "widening repacks without restart");

        panel.QuickLaunchFirst = true;
        Layout(220);
        Check(Near(X(quick), 0) && Near(X(start), 94), "Quick Launch first reverses packed order");
        Layout(171);
        Check(Near(Y(quick), 0) && Near(Y(start), 24), "Quick Launch first reverses stacked order");

        panel.QuickLaunchFirst = false;
        panel.PackBesideStart = false;
        Layout(220);
        Check(Near(X(quick), 0) && Near(Y(quick), 28), "opt-out always retains stacked side layout");

        panel.PackBesideStart = true;
        start.Visibility = Visibility.Collapsed;
        Layout(220);
        Check(Near(X(quick), 0) && Near(Y(quick), 0) &&
              Near(panel.DesiredSize.Height, 24), "hidden Start leaves no phantom band or gutter");

        start.Visibility = Visibility.Visible;
        quick.Visibility = Visibility.Collapsed;
        Layout(220);
        Check(Near(X(start), 0) && Near(Y(start), 0) &&
              Near(panel.DesiredSize.Height, 28), "hidden Quick Launch leaves no phantom band or gutter");

        quick.Visibility = Visibility.Visible;
        panel.Orientation = Orientation.Horizontal;
        Layout(220);
        // Dock.Left gives both controls the full taskbar height. A shorter child may
        // legitimately be vertically centred inside its slot by WPF.
        Check(Near(X(quick), 76) && Y(quick) >= -0.01 &&
              Y(quick) + quick.ActualHeight <= panel.ActualHeight + 0.01,
            $"horizontal taskbar preserves consecutive Dock.Left geometry without a gap (x={X(quick)}, y={Y(quick)})");

        // A custom Start image or size change alters the decision without replacing the controls.
        panel.Orientation = Orientation.Vertical;
        start.Width = 130;
        Layout(240);
        Check(Near(Y(quick), 0), "Start image resize still packs if it fits");
        Layout(200);
        Check(Near(Y(quick), 28), "Start image resize triggers a safe full-width fallback");

        // In ordinary (horizontal and disabled) modes, constrain the first section
        // just as the old DockPanel did, then constrain the second to what remains.
        // Also exercise reversed ordering and a failed natural-width packing probe.
        var constraints = new AdaptiveStartQuickPanel();
        var firstProbe = new SideQuickMeasureProbe(80, 30);
        var secondProbe = new SideQuickMeasureProbe(70, 40);
        constraints.Children.Add(firstProbe);
        constraints.Children.Add(secondProbe);
        constraints.Measure(new Size(100, 50));
        Check(firstProbe.LastConstraint == new Size(100, 50) &&
              secondProbe.LastConstraint == new Size(20, 50) &&
              constraints.DesiredSize == new Size(100, 40),
            "horizontal measurement respects remaining Dock.Left width");
        constraints.Orientation = Orientation.Vertical;
        constraints.Measure(new Size(100, 50));
        Check(firstProbe.LastConstraint == new Size(100, 50) &&
              secondProbe.LastConstraint == new Size(100, 20) &&
              constraints.DesiredSize == new Size(80, 50),
            "opt-out respects remaining Dock.Top height");
        constraints.QuickLaunchFirst = true;
        constraints.Measure(new Size(100, 50));
        Check(secondProbe.LastConstraint == new Size(100, 50) &&
              firstProbe.LastConstraint == new Size(100, 10),
            "reversed opt-out constrains the second section in visual order");
        constraints.PackBesideStart = true;
        constraints.Measure(new Size(100, 50));
        Check(secondProbe.LastConstraint == new Size(100, 50) &&
              firstProbe.LastConstraint == new Size(100, 10),
            "failed packing probe restores constrained stacked measurement");
        constraints.QuickLaunchFirst = false;
        constraints.Measure(new Size(200, 25));
        Check(firstProbe.LastConstraint == new Size(200, 25) &&
              secondProbe.LastConstraint == new Size(200, 0),
            "a row that exceeds available height is not admitted as packed");

        // Use the actual wrapping panel and the same nested Start shape as Taskbar.xaml.
        // A secondary taskbar may hide both StartArea children without collapsing StartArea.
        var realWrapHost = new AdaptiveStartQuickPanel
        {
            Orientation = Orientation.Vertical,
            PackBesideStart = true,
        };
        var startGroup = new StackPanel { Orientation = Orientation.Horizontal };
        var startCell = new Border { Width = 30, Height = 28 };
        startGroup.Children.Add(startCell);
        var wrap = new QuickLaunchPanel { Orientation = Orientation.Horizontal, ItemWidth = 30, ItemHeight = 20 };
        for (int i = 0; i < 4; i++) wrap.Children.Add(new Border());
        realWrapHost.Children.Add(startGroup);
        realWrapHost.Children.Add(wrap);

        void LayoutWrap(double width)
        {
            realWrapHost.Measure(new Size(width, 500));
            realWrapHost.Arrange(new Rect(0, 0, width, realWrapHost.DesiredSize.Height));
        }

        LayoutWrap(156);
        Check(Near(X(wrap), 34) && Near(Y(wrap), 0) &&
              Near(realWrapHost.DesiredSize.Height, 28),
            "real QuickLaunchPanel packs all four cells at the exact threshold");
        LayoutWrap(155);
        Check(Near(X(wrap), 0) && Near(Y(wrap), 28),
            "real QuickLaunchPanel returns to a complete full-width row");
        LayoutWrap(65);
        Check(Near(Y(wrap), 28) && Near(realWrapHost.DesiredSize.Height, 68) &&
              Near(Y(wrap.Children[2]), 20) && Near(X(wrap.Children[2]), 0),
            "narrow fallback wraps actual cells across two rows");

        startCell.Visibility = Visibility.Collapsed;
        LayoutWrap(156);
        Check(startGroup.Visibility == Visibility.Visible &&
              Near(X(wrap), 0) && Near(Y(wrap), 0) &&
              Near(realWrapHost.DesiredSize.Height, 20),
            "visible StartArea with collapsed descendants creates no phantom gutter or band");
        realWrapHost.QuickLaunchFirst = true;
        LayoutWrap(156);
        Check(Near(X(wrap), 0) && Near(Y(wrap), 0) &&
              Near(realWrapHost.DesiredSize.Height, 20),
            "empty StartArea creates no phantom gutter in reverse order either");
    }

    private sealed class SideQuickMeasureProbe : FrameworkElement
    {
        private readonly Size _natural;
        public Size LastConstraint { get; private set; }

        public SideQuickMeasureProbe(double width, double height)
        {
            _natural = new Size(width, height);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            LastConstraint = availableSize;
            return new Size(Math.Min(_natural.Width, availableSize.Width),
                Math.Min(_natural.Height, availableSize.Height));
        }
    }
}
