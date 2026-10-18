using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the quick launch icons' wrap panel, with room for one extra cell that is not an item
    // of the list: the Show desktop button when it is set to live inside the quick launch area. The
    // extra cell is laid out first, exactly like a program's icon: same cell size, same rows, same
    // columns. (It is a visual child of its own, so the items' own indices are untouched.)
    public class QuickLaunchPanel : WrapPanel
    {
        public static readonly DependencyProperty ExtraProperty = DependencyProperty.Register(
            nameof(Extra), typeof(UIElement), typeof(QuickLaunchPanel), new PropertyMetadata(null, OnExtraChanged));

        public UIElement Extra
        {
            get => (UIElement)GetValue(ExtraProperty);
            set => SetValue(ExtraProperty, value);
        }

        private UIElement _extra;

        private static void OnExtraChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (QuickLaunchPanel)d;
            if (panel._extra != null)
            {
                panel.RemoveVisualChild(panel._extra);
                panel.RemoveLogicalChild(panel._extra);
                panel._extra = null;
            }

            if (e.NewValue is UIElement extra)
            {
                // A rebuilt items panel (new theme) takes the cell over from the one it replaces.
                if (VisualTreeHelper.GetParent(extra) is QuickLaunchPanel previous && !ReferenceEquals(previous, panel))
                {
                    previous.Extra = null;
                }

                panel._extra = extra;
                panel.AddLogicalChild(extra);
                panel.AddVisualChild(extra);
            }

            panel.InvalidateMeasure();
        }

        protected override int VisualChildrenCount => base.VisualChildrenCount + (_extra != null ? 1 : 0);

        protected override Visual GetVisualChild(int index)
        {
            int own = base.VisualChildrenCount;
            return _extra != null && index == own ? _extra : base.GetVisualChild(index);
        }

        private List<UIElement> Cells()
        {
            var cells = new List<UIElement>();
            if (_extra != null && _extra.Visibility != Visibility.Collapsed)
            {
                cells.Add(_extra);
            }

            foreach (UIElement child in InternalChildren)
            {
                if (child != null && child.Visibility != Visibility.Collapsed)
                {
                    cells.Add(child);
                }
            }

            return cells;
        }

        // Length along the line and thickness across it, whichever way the panel runs.
        private double Along(Size size) => Orientation == Orientation.Horizontal ? size.Width : size.Height;
        private double Across(Size size) => Orientation == Orientation.Horizontal ? size.Height : size.Width;

        private double CellAlong(UIElement cell) => Orientation == Orientation.Horizontal && !double.IsNaN(ItemWidth) ? ItemWidth
            : Orientation == Orientation.Vertical && !double.IsNaN(ItemHeight) ? ItemHeight : Along(cell.DesiredSize);

        private double CellAcross(UIElement cell) => Orientation == Orientation.Horizontal && !double.IsNaN(ItemHeight) ? ItemHeight
            : Orientation == Orientation.Vertical && !double.IsNaN(ItemWidth) ? ItemWidth : Across(cell.DesiredSize);

        protected override Size MeasureOverride(Size constraint)
        {
            var cells = Cells();
            var childConstraint = new Size(double.IsNaN(ItemWidth) ? constraint.Width : ItemWidth, double.IsNaN(ItemHeight) ? constraint.Height : ItemHeight);
            double limit = Along(constraint);
            double used = 0, thickness = 0, longest = 0, total = 0;

            foreach (UIElement cell in cells)
            {
                cell.Measure(childConstraint);
                double length = CellAlong(cell), across = CellAcross(cell);
                if (used > 0 && used + length > limit)
                {
                    longest = System.Math.Max(longest, used);
                    total += thickness;
                    used = 0;
                    thickness = 0;
                }

                used += length;
                thickness = System.Math.Max(thickness, across);
            }

            longest = System.Math.Max(longest, used);
            total += thickness;
            return Orientation == Orientation.Horizontal ? new Size(longest, total) : new Size(total, longest);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var cells = Cells();
            double limit = Along(finalSize);
            double position = 0, offset = 0;
            var line = new List<UIElement>();

            void flush(double thickness)
            {
                double at = 0;
                foreach (UIElement cell in line)
                {
                    double length = CellAlong(cell);
                    cell.Arrange(Orientation == Orientation.Horizontal
                        ? new Rect(at, offset, length, thickness)
                        : new Rect(offset, at, thickness, length));
                    at += length;
                }

                offset += thickness;
                line.Clear();
            }

            double lineThickness = 0;
            foreach (UIElement cell in cells)
            {
                double length = CellAlong(cell), across = CellAcross(cell);
                if (position > 0 && position + length > limit)
                {
                    flush(lineThickness);
                    position = 0;
                    lineThickness = 0;
                }

                line.Add(cell);
                position += length;
                lineThickness = System.Math.Max(lineThickness, across);
            }

            if (line.Count > 0)
            {
                flush(lineThickness);
            }

            return finalSize;
        }
    }
}
