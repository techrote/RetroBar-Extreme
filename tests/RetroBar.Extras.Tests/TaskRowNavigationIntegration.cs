using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        // A ScrollViewer can remeasure after its computed scrollbar state changes.
        // Drive two synchronous STA layout passes, checking the settled result.
        for (int pass = 0; pass < 2; pass++)
        {
            viewer.InvalidateMeasure();
            viewer.Measure(new Size(width, 52));
            viewer.Arrange(new Rect(0, 0, width, 52));
            viewer.UpdateLayout();
        }
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
        Check(((UIElement)VisualTreeHelper.GetParent(left)).Visibility == Visibility.Visible &&
              ((UIElement)VisualTreeHelper.GetParent(right)).Visibility == Visibility.Visible,
            "actual overflow makes both navigation containers visible");
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
        Check(((UIElement)VisualTreeHelper.GetParent(left)).Visibility == Visibility.Collapsed &&
              ((UIElement)VisualTreeHelper.GetParent(right)).Visibility == Visibility.Collapsed,
            $"real navigation containers collapse on no-overflow: computed={viewer.ComputedHorizontalScrollBarVisibility}, extent={viewer.ExtentWidth}, viewport={viewer.ViewportWidth}, scrollable={viewer.ScrollableWidth}");
    }
    private static void ProductionTaskListViewportBinding()
    {
        using var settings = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow",
            "RowCount", "TaskButtonWidthPx", "TaskButtonAppRules", "Edge");
        using var theme = new NavigationThemeScope();
        settings.Set("TaskRowRules", new List<TaskRowRule>
        {
            new TaskRowRule { Row = 1, Align = TaskButtonAlignOption.Right },
        });
        settings.Set("TaskEndApps", "");
        settings.Set("TaskEndAppsBottomRow", false);
        settings.Set("RowCount", 1);
        settings.Set("TaskButtonWidthPx", 80.0);
        settings.Set("Edge", ManagedShell.AppBar.AppBarEdge.Bottom);

        // Supply only those global style resources normally loaded by App. TaskList,
        // TaskButton, the ItemsControl ItemTemplate and ItemsPanelTemplate remain the
        // unmodified production XAML. No taskbar/shell HWND or owner profile is created.
        var values = new Dictionary<string, object>
        {
            ["TaskButtonWidth"] = 160.0,
            ["TaskButtonMinWidth"] = 45.0,
            ["TaskButtonMargin"] = new Thickness(0, 0, 3, 0),
            ["TaskButtonVerticalMargin"] = new Thickness(),
            ["TaskButton"] = new Style(typeof(Button)),
            ["TaskButtonActive"] = new Style(typeof(Button)),
            ["TaskButtonFlashing"] = new Style(typeof(Button)),
            ["menuTextRenderingModeConverter"] = new RetroBar.Converters.BoolToTextRenderingModeConverter(),
        };
        var resources = Application.Current.Resources;
        var originals = values.ToDictionary(kv => kv.Key,
            kv => (exists: resources.Contains(kv.Key), value: resources.Contains(kv.Key) ? resources[kv.Key] : null));
        foreach (var pair in values) resources[pair.Key] = pair.Value;
        try
        {
            var control = new TaskList();
            // Normal production initialization happens from Loaded. This fixture is deliberately
            // HWND-free, so initialise the same theme-dependent width fields explicitly.
            typeof(TaskList).GetMethod("SetStyles", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(control, null);
            var items = (ItemsControl)control.FindName("TasksList");
            var viewer = (ScrollViewer)control.FindName("TasksScrollViewer");
            Check(items != null && viewer != null, "production TaskList exposes its real task items and scroll host");
            items.ItemsSource = Enumerable.Range(0, 9).Select(i => FixtureApp("integration" + i)).ToArray();
            control.ButtonMarginTotal = 3;
            control.IconOnlyWidth = 48;
            control.DefaultWidthWithMargin = 163;

            var sizing = typeof(TaskList).GetMethod("ConfigureButtonLayout",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Check(sizing != null, "production layout policy has a single host-independent sizing core");
            void SetLayout(int rows) => sizing.Invoke(control, new object[] { Orientation.Horizontal, rows });
            void Measure(double width, int rows = 1)
            {
                SetLayout(rows);
                control.Measure(new Size(width, 52));
                control.Arrange(new Rect(0, 0, width, 52));
                control.UpdateLayout();
                SetLayout(rows);
                control.InvalidateMeasure();
                control.Measure(new Size(width, 52));
                control.Arrange(new Rect(0, 0, width, 52));
                control.UpdateLayout();
            }

            Measure(210);
            var panel = DescendantsOfType<TaskRowPanel>(control).Single();
            var realButtons = DescendantsOfType<TaskButton>(control).ToArray();
            Check(realButtons.Length == 9, "production ItemTemplate creates nine real task buttons");
            Check(panel.GetBindingExpression(TaskRowPanel.ViewportWidthProperty) != null,
                "production ItemsPanelTemplate binds ViewportWidth rather than injecting a test-only value");
            Check(panel.ViewportWidth > 0 && Math.Abs(panel.ViewportWidth - viewer.ViewportWidth) < 1,
                "production viewport ancestor binding resolves to the real ScrollViewer");
            Check(viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && viewer.ScrollableWidth > 0,
                $"production TaskList sizing enables overflow: mode={viewer.HorizontalScrollBarVisibility}, scrollable={viewer.ScrollableWidth}, buttonWidth={control.ButtonWidth}, count={items.Items.Count}, boundViewport={panel.ViewportWidth}");

            double widthAt210 = control.ButtonWidth;
            Measure(165);
            Check(Math.Abs(control.ButtonWidth - widthAt210) < 40,
                "production button width remains bounded by host viewport rather than growing content extent");
            Check(viewer.ScrollableWidth > 0 && double.IsFinite(viewer.ScrollableWidth),
                "resize keeps a stable, finite extent without oscillation");
            Check(panel.ViewportWidth >= 0 && double.IsFinite(panel.ViewportWidth),
                "actual viewport binding remains finite during resize");

            settings.Set("TaskRowRules", new List<TaskRowRule>());
            SetLayout(1);
            panel.InvalidateMeasure();
            Measure(165);
            Check(viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled,
                "production sizing turns horizontal scrolling off when rules are removed");
            Check(viewer.ScrollableWidth < 1, "no-rule TaskList uses the original wrapping path");

            // Regression probe: scroll arrows take width away from the content viewport.
            // A four-button/two-row layout fits without arrows, but may no longer fit
            // inside the smaller viewport while arrows are displayed. Include widths
            // close to the per-button minimum, where the arrow chrome is a large fraction
            // of the available area. Changing a row rule
            // should recover the same no-overflow state as a fresh unrestrained layout.
            // No shell HWND, task activation or real application installation is involved.
            items.ItemsSource = Enumerable.Range(0, 4).Select(i => FixtureApp("integration" + i)).ToArray();
            settings.Set("RowCount", 2);
            var relaxed = new List<TaskRowRule>
            {
                new TaskRowRule { Row = 1, Align = TaskButtonAlignOption.Centre },
            };
            var constrained = new List<TaskRowRule>
            {
                new TaskRowRule { Row = 1, Apps = "integration", OnlyListed = true },
            };
            foreach (double width in new[] { 105.0, 110.0, 120.0, 140.0, 180.0, 185.0, 200.0, 210.0 })
            {
                settings.Set("TaskRowRules", relaxed);
                panel.InvalidateMeasure();
                Measure(width, 2);
                Measure(width, 2);
                // Configured button widths are no longer compressed under row rules.
                // Below 180 DIPs even two 83-DIP buttons per row legitimately need
                // horizontal access; at 180+ the same relaxed rule fits without arrows.
                bool relaxedNeedsScroll = width < 180;
                Visibility expectedRelaxedArrows = relaxedNeedsScroll ? Visibility.Visible : Visibility.Collapsed;
                Check(viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto &&
                      viewer.ComputedHorizontalScrollBarVisibility == expectedRelaxedArrows &&
                      (relaxedNeedsScroll ? viewer.ScrollableWidth > 1 : viewer.ScrollableWidth < 0.5),
                    $"fresh relaxed rows must match actual fixed-width occupancy at width {width}: expected={expectedRelaxedArrows}, computed={viewer.ComputedHorizontalScrollBarVisibility}, scrollable={viewer.ScrollableWidth}, viewport={viewer.ViewportWidth}, outer={viewer.ActualWidth}, button={control.ButtonWidth}");

                settings.Set("TaskRowRules", constrained);
                panel.InvalidateMeasure();
                Measure(width, 2);
                Measure(width, 2);
                Check(viewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible &&
                      viewer.ScrollableWidth > 1,
                    $"constraining all four buttons into row 1 should overflow at width {width}: computed={viewer.ComputedHorizontalScrollBarVisibility}, scrollable={viewer.ScrollableWidth}, viewport={viewer.ViewportWidth}, outer={viewer.ActualWidth}, button={control.ButtonWidth}");

                settings.Set("TaskRowRules", relaxed);
                panel.InvalidateMeasure();
                Measure(width, 2);
                Measure(width, 2);
                Check(viewer.ComputedHorizontalScrollBarVisibility == expectedRelaxedArrows &&
                      (relaxedNeedsScroll ? viewer.ScrollableWidth > 1 : viewer.ScrollableWidth < 0.5),
                    $"P2 arrow hysteresis: relaxed rows must return to their fresh fixed-width state at width {width}: expected={expectedRelaxedArrows}, computed={viewer.ComputedHorizontalScrollBarVisibility}, scrollable={viewer.ScrollableWidth}, viewport={viewer.ViewportWidth}, outer={viewer.ActualWidth}, button={control.ButtonWidth}");
                Check(DescendantsOfType<TaskButton>(control).Count() == 4,
                    "arrow-state transition retains all four production task buttons");
            }

            // Owner physical failure: on a full taskbar, configured task-button widths
            // were compressed to a row-count average, truncating their visible faces
            // without generating horizontal navigation. A constrained row must keep
            // its configured width; the finite viewer scrolls instead.
            settings.Set("TaskButtonWidthPx", 240.0);
            typeof(TaskList).GetMethod("SetStyles", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(control, null);
            settings.Set("TaskRowRules", constrained);
            panel.InvalidateMeasure();
            Measure(200, 2);
            Measure(200, 2);
            Check(control.ButtonWidth >= 240,
                $"full-taskbar width regression: requested 240-DIP button compressed to {control.ButtonWidth}, viewer={viewer.ActualWidth}, viewport={viewer.ViewportWidth}, scrollable={viewer.ScrollableWidth}");
            var fullButtons = DescendantsOfType<TaskButton>(control).ToArray();
            Check(fullButtons.Length == 4 && fullButtons.All(button => button.ActualWidth >= 239),
                "all four production task buttons retain the configured full width");
            settings.Set("TaskButtonAppRules", new List<TaskButtonAppRule>
            {
                new TaskButtonAppRule { App = "integration0.exe", Width = 320 },
            });
            panel.InvalidateMeasure();
            Measure(200, 2);
            var wideButton = DescendantsOfType<TaskButton>(control).Single(button =>
                ((ManagedShell.WindowsTasks.ApplicationWindow)button.DataContext).WinFileName.EndsWith("integration0.exe"));
            Check(wideButton.ActualWidth >= 319,
                $"per-app wide-button override must not be clipped by row compression: actual={wideButton.ActualWidth}");
            Check(viewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible && viewer.ScrollableWidth > 0,
                "per-app wider face remains horizontally reachable");

            Check(viewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible &&
                  viewer.ScrollableWidth >= 2 * 239,
                $"full-width task buttons must overflow horizontally rather than be clipped without navigation: computed={viewer.ComputedHorizontalScrollBarVisibility}, extent={viewer.ExtentWidth}, viewport={viewer.ViewportWidth}");
            viewer.ScrollToRightEnd();
            viewer.UpdateLayout();
            Check(viewer.HorizontalOffset > 2 * 239 - 1,
                "last full-width task button remains horizontally reachable on a full taskbar");

            // No-rule layouts must retain native WrapPanel behaviour rather than
            // suddenly activating a horizontal scroll policy.
            settings.Set("TaskRowRules", new List<TaskRowRule>());
            panel.InvalidateMeasure();
            Measure(200, 2);
            Check(viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled,
                "legacy no-rule WrapPanel does not inherit new full-width scrolling");

            // Reproduce the user's full taskbar geometry: TaskList is the last fill
            // child in a DockPanel, with a fixed-width notification/tray area docked
            // to the right. An isolated TaskList fixture cannot catch host-allocation
            // regressions at precisely this edge of the screen.
            settings.Set("TaskRowRules", constrained);
            panel.InvalidateMeasure();
            var barDock = new DockPanel();
            var tray = new Border { Width = 120, Height = 52 };
            DockPanel.SetDock(tray, Dock.Right);
            barDock.Children.Add(tray);
            barDock.Children.Add(control);
            for (int pass = 0; pass < 3; pass++)
            {
                SetLayout(2);
                barDock.InvalidateMeasure();
                barDock.Measure(new Size(320, 52));
                barDock.Arrange(new Rect(0, 0, 320, 52));
                barDock.UpdateLayout();
            }
            Check(Math.Abs(control.ActualWidth - 200) < 1,
                $"host DockPanel reserves the trailing tray: taskArea={control.ActualWidth}, tray={tray.ActualWidth}");
            Check(control.ButtonWidth >= 240 &&
                  DescendantsOfType<TaskButton>(control).Any(button => button.ActualWidth >= 319),
                "docked production task buttons keep default and wide per-app widths");
            Check(viewer.ComputedHorizontalScrollBarVisibility == Visibility.Visible &&
                  viewer.ScrollableWidth > 2 * 239,
                $"task area beside a tray uses horizontal navigation instead of button clipping: outer={viewer.ActualWidth}, extent={viewer.ExtentWidth}, scrollable={viewer.ScrollableWidth}");
            double viewerRight = viewer.TranslatePoint(new Point(viewer.ActualWidth, 0), barDock).X;
            double trayLeft = tray.TranslatePoint(new Point(0, 0), barDock).X;
            Check(viewerRight <= trayLeft + 0.5,
                $"scroll host remains entirely to the left of the notification tray: viewerRight={viewerRight}, trayLeft={trayLeft}");
            viewer.ScrollToRightEnd();
            viewer.UpdateLayout();
            Check(viewer.HorizontalOffset > 2 * 239,
                "the last full-width button stays reachable with the real trailing tray layout");
        }
        finally
        {
            foreach (var pair in originals)
            {
                if (pair.Value.exists) resources[pair.Key] = pair.Value.value;
                else resources.Remove(pair.Key);
            }
        }
    }

}
