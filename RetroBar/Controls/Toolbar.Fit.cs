using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RetroBar.Controls
{
    // UXUI-Extreme: on multi-row taskbars the quick launch icons stack in columns, and a column
    // holds as many icons as fit the taskbar's height. Locking the taskbar takes off its few
    // pixels of resize margin, which could push the last icon into a second column (the
    // toolbar suddenly doubles in width). Before adding a column, let the icon cells give up a
    // little of their padding (down to 75% of their natural height) so the column count stays
    // put; only when that isn't enough does another column appear.
    public partial class Toolbar
    {
        private const double MinCellShare = 0.75;
        private WrapPanel _iconPanel;
        private bool _fitting;

        private void FitIconColumns()
        {
            if (_fitting)
            {
                return;
            }

            _fitting = true;
            try
            {
                _iconPanel = FindChild<WrapPanel>(ToolbarItems); // re-found each time: the items panel is rebuilt on theme changes
                if (_iconPanel == null)
                {
                    return;
                }

                // Never narrower (or shorter) than one icon cell, so a shortened quick launch area doesn't
                // cut off the icons or their boxes.
                bool alongBar = Host == null || Host.Orientation == Orientation.Horizontal;
                double cell = CellLength(alongBar);
                if (cell > 0)
                {
                    if (alongBar) { MinWidth = cell; ClearValue(MinHeightProperty); }
                    else { MinHeight = cell; ClearValue(MinWidthProperty); }
                }

                // The Show desktop icon (when it lives in this area) is one of the cells.
                int count = ToolbarItems.Items.Count + (QuickExtra != null ? 1 : 0);
                UIElement first = QuickExtra ?? ToolbarItems.ItemContainerGenerator.ContainerFromIndex(0) as UIElement;
                if (_iconPanel.Orientation != Orientation.Vertical || count == 0 || first == null)
                {
                    _iconPanel.ClearValue(WrapPanel.ItemHeightProperty);
                    return;
                }

                first.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double natural = first.DesiredSize.Height;
                Thickness margin = ToolbarItems.Margin;
                double available = ActualHeight - margin.Top - margin.Bottom;
                if (natural <= 0 || available <= 0)
                {
                    return;
                }

                int perColumn = Math.Max(1, (int)Math.Floor(available / natural));
                int columns = (int)Math.Ceiling((double)count / perColumn);
                double itemHeight = double.NaN;

                // Fewest columns whose (slightly squeezed) cells still fit.
                for (int c = 1; c < columns; c++)
                {
                    int rows = (int)Math.Ceiling((double)count / c);
                    double height = Math.Floor(available / rows);
                    if (height >= natural * MinCellShare)
                    {
                        itemHeight = height;
                        break;
                    }
                }

                if (double.IsNaN(itemHeight))
                {
                    _iconPanel.ClearValue(WrapPanel.ItemHeightProperty);
                }
                else if (_iconPanel.ItemHeight != itemHeight)
                {
                    _iconPanel.ItemHeight = itemHeight;
                }
            }
            finally
            {
                _fitting = false;
            }
        }

        // The room one icon cell needs along the taskbar (with the items' margin), so a shortened quick
        // launch area never clips the icons or their boxes.
        public double CellLength(bool horizontal)
        {
            UIElement first = QuickExtra ?? ToolbarItems.ItemContainerGenerator.ContainerFromIndex(0) as UIElement;
            if (first == null)
            {
                return 0;
            }

            var unlimited = new Size(double.PositiveInfinity, double.PositiveInfinity);
            first.Measure(unlimited);
            Thickness margin = ToolbarItems.Margin;
            double cell = horizontal ? first.DesiredSize.Width + margin.Left + margin.Right : first.DesiredSize.Height + margin.Top + margin.Bottom;

            // Plus everything else in the area that takes room: the grip's strip and the Show desktop cell.
            if (Content is Panel root)
            {
                foreach (UIElement other in root.Children)
                {
                    if (!ReferenceEquals(other, ToolbarFlow) && other.Visibility != Visibility.Collapsed)
                    {
                        other.Measure(unlimited);
                        cell += horizontal ? other.DesiredSize.Width : other.DesiredSize.Height;
                    }
                }
            }

            if (DesktopCell.Visibility != Visibility.Collapsed)
            {
                DesktopCell.Measure(unlimited);
                cell += horizontal ? DesktopCell.DesiredSize.Width : DesktopCell.DesiredSize.Height;
            }

            return cell + 1; // a pixel of slack, so rounding never shaves the last column
        }

        private bool _fitHooked;

        private void HookIconFit()
        {
            if (_fitHooked)
            {
                return;
            }

            _fitHooked = true;
            void schedule() => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(FitIconColumns));
            SizeChanged += (s, e) => { if (Math.Abs(e.NewSize.Height - e.PreviousSize.Height) > 0.5) schedule(); };
            ((System.Collections.Specialized.INotifyCollectionChanged)ToolbarItems.Items).CollectionChanged += (s, e) => schedule();
            ToolbarItems.ItemContainerGenerator.StatusChanged += (s, e) => schedule();
            schedule();
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                T found = FindChild<T>(child);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
