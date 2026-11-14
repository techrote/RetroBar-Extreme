using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using RetroBar.Controls;
using RetroBar.Utilities;

internal static partial class Program
{
    // Load the exact Styles from the embedded production System.xaml. Instantiating these
    // controls exercises the real ScrollViewer ControlTemplate, RepeatButton commands
    // and ScrollContentPresenter, rather than performing XAML string inspections.
    private sealed class NavigationThemeScope : IDisposable
    {
        private readonly Dictionary<string, object> previous = new();
        private readonly HashSet<string> introduced = new();
        internal NavigationThemeScope()
        {
            using Stream stream = typeof(TaskRowPanel).Assembly.GetManifestResourceStream("RetroBar.Themes.System.xaml");
            Check(stream != null, "embedded production ScrollViewer theme is available");
            XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var doc = XDocument.Load(stream);
            foreach (string key in new[] { "TaskListScrollButton", "TaskListScrollButtonText",
                                           "TaskListScrollButtonGrid", "TaskListScrollViewer",
                                           "TaskListWrapPanel" })
            {
                XElement node = doc.Descendants(p + "Style").Single(e => (string)e.Attribute(x + "Key") == key);
                object style = XamlReader.Parse(node.ToString());
                ResourceDictionary resources = Application.Current.Resources;
                if (resources.Contains(key)) previous[key] = resources[key];
                else introduced.Add(key);
                resources[key] = style;
            }
        }

        public void Dispose()
        {
            foreach (string key in introduced) Application.Current.Resources.Remove(key);
            foreach (var item in previous) Application.Current.Resources[item.Key] = item.Value;
        }
    }

    private static IEnumerable<T> DescendantsOfType<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) yield return found;
            foreach (T descendant in DescendantsOfType<T>(child)) yield return descendant;
        }
    }

    private static ScrollViewer NavigationViewer(double width)
    {
        var row = new TaskRowPanel { Orientation = Orientation.Horizontal };
        BindingOperations.SetBinding(row, TaskRowPanel.ViewportWidthProperty,
            new Binding("ViewportWidth") { RelativeSource =
                new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ScrollViewer), 1) });
        for (int i = 0; i < 7; i++)
            row.Children.Add(new Border { Width = 88, Height = 19 });
        var viewer = new ScrollViewer
        {
            Content = row,
            Style = (Style)Application.Current.Resources["TaskListScrollViewer"],
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        ResizeViewer(viewer, width);
        return viewer;
    }

    private static void ResizeViewer(ScrollViewer viewer, double width)
    {
        viewer.InvalidateMeasure();
        viewer.Measure(new Size(width, 52));
        viewer.Arrange(new Rect(0, 0, width, 52));
        viewer.UpdateLayout();
    }

    private static void ExecutableNavigationTemplate()
    {
        using var settings = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow", "RowCount");
        using var theme = new NavigationThemeScope();
        settings.Set("RowCount", 1);
        settings.Set("TaskEndApps", "");
        settings.Set("TaskEndAppsBottomRow", false);
        settings.Set("TaskRowRules", new List<TaskRowRule> { new TaskRowRule { Row = 1, Align = TaskButtonAlignOption.Right } });

        ScrollViewer viewer = NavigationViewer(180);
        var buttons = DescendantsOfType<RepeatButton>(viewer).ToArray();
        Check(buttons.Length >= 2, "real production template creates RepeatButton controls");
        RepeatButton left = buttons.Single(b => b.Command == ScrollBar.PageLeftCommand);
        RepeatButton right = buttons.Single(b => b.Command == ScrollBar.PageRightCommand);
        Check(left.Visibility == Visibility.Visible && right.Visibility == Visibility.Visible,
            "actual overflow makes left/right navigation visible");
        Check(viewer.ScrollableWidth > 100, "production template exposes a finite scrollable extent");

        double initial = viewer.HorizontalOffset;
        var peer = UIElementAutomationPeer.CreatePeerForElement(right) ?? new RepeatButtonAutomationPeer(right);
        Check(peer is IInvokeProvider, "right navigation exposes executable button action");
        ((IInvokeProvider)peer).Invoke();
        viewer.UpdateLayout();
        Check(viewer.HorizontalOffset > initial, "actual right navigation click scrolls right");
        double moved = viewer.HorizontalOffset;
        peer = UIElementAutomationPeer.CreatePeerForElement(left) ?? new RepeatButtonAutomationPeer(left);
        ((IInvokeProvider)peer).Invoke();
        viewer.UpdateLayout();
        Check(viewer.HorizontalOffset < moved, "actual left navigation click scrolls left");

        foreach (double width in new[] { 16.0, 24.0, 32.0, 42.0 })
        {
            ResizeViewer(viewer, width);
            Check(double.IsFinite(viewer.ScrollableWidth) && viewer.ScrollableWidth >= 0,
                $"narrow {width}px template has finite, nonnegative extent");
            Check(double.IsFinite(viewer.ViewportWidth) && viewer.ViewportWidth >= 0,
                $"narrow {width}px template has finite, nonnegative viewport");
            Check(double.IsFinite(viewer.HorizontalOffset) && viewer.HorizontalOffset >= 0,
                $"narrow {width}px template keeps valid offset");
        }
        ResizeViewer(viewer, 900);
        Check(viewer.ScrollableWidth < 0.5, "expanding viewport removes overflow");
        Check(left.Visibility != Visibility.Visible && right.Visibility != Visibility.Visible,
            "real navigation controls hide automatically when overflow disappears");
    }
}
