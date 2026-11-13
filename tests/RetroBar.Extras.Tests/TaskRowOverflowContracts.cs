using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using ManagedShell.AppBar;
using ManagedShell.WindowsTasks;
using RetroBar.Controls;
using RetroBar.Converters;
using RetroBar.Utilities;

internal static partial class Program
{
    // Never instantiate a taskbar or start ManagedShell on the CI machine. These are
    // genuine WPF controls measured/arranged on STA; window objects only carry app keys.
    private static ApplicationWindow FixtureApp(string name)
    {
        var app = new ApplicationWindow(null, IntPtr.Zero);
        var filename = typeof(ApplicationWindow).GetField("_winFileName", BindingFlags.Instance | BindingFlags.NonPublic);
        var aumid = typeof(ApplicationWindow).GetField("_appUserModelId", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(filename != null && aumid != null, "ManagedShell test fixture can initialise harmless app identity");
        filename.SetValue(app, @"C:\Fixture\" + name + ".exe");
        aumid.SetValue(app, "fixture." + name);
        return app;
    }

    private static (ScrollViewer viewer, TaskRowPanel panel, FrameworkElement[] items) RowFixture(
        int rows, double viewport, params (string app, double width)[] windows)
    {
        // Settings is deliberately internal to the application. The existing Extras
        // contract scope suppresses saves; assign through its same reflection seam.
        Type type = typeof(HotkeyManager).Assembly.GetType("RetroBar.Utilities.Settings", true);
        object settings = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        type.GetProperty("RowCount").SetValue(settings, rows);
        var panel = new TaskRowPanel { Orientation = Orientation.Horizontal, ViewportWidth = viewport };
        FrameworkElement[] items = windows.Select(w => (FrameworkElement)new Button
        {
            Width = w.width, Height = 18, DataContext = FixtureApp(w.app), Focusable = true,
            Padding = new Thickness(0), Margin = new Thickness(0),
        }).ToArray();
        foreach (FrameworkElement item in items)
            panel.Children.Add(item);
        var viewer = new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };
        MeasureRow(viewer, panel, viewport, rows);
        return (viewer, panel, items);
    }

    private static void MeasureRow(ScrollViewer viewer, TaskRowPanel panel, double viewport, int rows)
    {
        panel.ViewportWidth = viewport;
        viewer.InvalidateMeasure();
        viewer.Measure(new Size(viewport, Math.Max(60, rows * 20 + 25)));
        viewer.Arrange(new Rect(0, 0, viewport, Math.Max(60, rows * 20 + 25)));
        viewer.UpdateLayout();
    }

    private static double X(FrameworkElement item) => VisualTreeHelper.GetOffset(item).X;
    private static double Y(FrameworkElement item) => VisualTreeHelper.GetOffset(item).Y;

    private static void ConstrainedRowRouting()
    {
        using var scope = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow", "RowCount");
        scope.Set("TaskRowRules", new List<TaskRowRule>
        {
            new TaskRowRule { Row = 1, Apps = "alpha", OnlyListed = true },
            new TaskRowRule { Row = -1, Apps = "beta" },
        });
        scope.Set("TaskEndApps", "gamma");
        scope.Set("TaskEndAppsBottomRow", true);
        var (viewer, panel, items) = RowFixture(2, 160, 
            ("alpha", 102), ("other", 97), ("beta", 84),
            ("alpha", 110), ("gamma", 90), ("other2", 108), ("gamma", 95));

        Check(Y(items[0]) == 0 && Y(items[3]) == 0, "explicit row 1 owns both alpha buttons");
        foreach (int index in new[] { 1, 2, 4, 5, 6 })
            Check(Math.Abs(Y(items[index]) - 18) < 0.5, "relative, default and end apps occupy bottom row");
        Check(X(items[3]) >= X(items[0]) + 102 - 0.5, "first row preserves alpha order while overflowing");
        Check(X(items[2]) >= X(items[1]) + 97 - 0.5, "relative-row beta follows ordinary first item");
        Check(X(items[5]) >= X(items[2]) + 84 - 0.5, "ordinary row order is stable");
        Check(X(items[4]) >= X(items[5]) + 108 - 0.5 && X(items[6]) > X(items[4]),
            "end apps remain last in their original relative order");
        Check(viewer.ScrollableWidth > 300, "multiple constrained rows produce shared horizontal extent");
        foreach (FrameworkElement item in items)
            Check(double.IsFinite(X(item)) && X(item) >= 0 && double.IsFinite(Y(item)) && Y(item) >= 0,
                "no overflowing button has negative or non-finite coordinates");
        viewer.ScrollToRightEnd();
        viewer.UpdateLayout();
        Check(viewer.HorizontalOffset > 0, "scroll viewer can navigate end-app buttons into view");

        // Relabelling an exclusive row does not reassign unrelated applications.
        scope.Set("TaskRowRules", new List<TaskRowRule>
        {
            new TaskRowRule { Row = 1, Apps = "beta", OnlyListed = true },
        });
        panel.InvalidateMeasure();
        MeasureRow(viewer, panel, 160, 2);
        Check(Y(items[2]) == 0, "changed rule moves beta to its new explicit row");
        Check(Y(items[0]) > 0 && Y(items[3]) > 0, "alpha buttons leave the now-exclusive beta row");
    }

    private static void ConstrainedRowResize()
    {
        using var scope = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow", "RowCount");
        scope.Set("TaskRowRules", new List<TaskRowRule>
        {
            new TaskRowRule { Row = -1, Align = TaskButtonAlignOption.Right },
        });
        scope.Set("TaskEndApps", "");
        scope.Set("TaskEndAppsBottomRow", false);
        var (viewer, panel, items) = RowFixture(1, 330,
            ("first", 105), ("second", 125), ("third", 100), ("fourth", 95));
        Check(items.All(item => Math.Abs(Y(item)) < 0.5), "one-row mode never creates overflow rows");
        Check(viewer.ScrollableWidth > 0, "one-row constrained mode permits horizontal navigation");
        Check(X(items[0]) >= 0 && X(items[3]) > X(items[0]), "right alignment never shifts overflow negative");

        MeasureRow(viewer, panel, 42, 1);
        Check(viewer.ScrollableWidth > 250, "narrow viewport preserves scroll extent");
        Check(items.All(item => X(item) >= 0 && double.IsFinite(X(item))), "narrow viewport has no negative coordinates");
        viewer.ScrollToRightEnd();
        viewer.UpdateLayout();
        Check(viewer.HorizontalOffset <= viewer.ScrollableWidth + 0.5, "offset remains clamped");

        MeasureRow(viewer, panel, 250, 1);
        Check(viewer.ScrollableWidth > 0 && viewer.HorizontalOffset <= viewer.ScrollableWidth + 0.5,
            "resize expands viewport without stale offset or extent");

        scope.Set("RowCount", 2);
        panel.InvalidateMeasure();
        MeasureRow(viewer, panel, 250, 2);
        Check(items.Any(item => Y(item) > 0), "changing row count repacks instead of retaining stale slots");

        // WPF's layout transform models a non-integer DPI scale while the ScrollViewer
        // still operates in DIPs. Exact physical-monitor DPI is reserved for owner testing.
        panel.LayoutTransform = new ScaleTransform(1.25, 1.25);
        MeasureRow(viewer, panel, 250, 2);
        Check(double.IsFinite(viewer.ExtentWidth) && viewer.ExtentWidth >= 0,
            "scaled WPF panel has finite extent");
    }

    private static void ConstrainedRowFocus()
    {
        using var scope = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow", "RowCount");
        scope.Set("TaskRowRules", new List<TaskRowRule>
        {
            new TaskRowRule { Row = 1, Align = TaskButtonAlignOption.Centre },
        });
        scope.Set("TaskEndApps", "");
        scope.Set("TaskEndAppsBottomRow", false);
        var (viewer, _, items) = RowFixture(1, 180,
            ("a", 95), ("b", 95), ("c", 95), ("d", 95), ("e", 95));
        Check(viewer.HorizontalOffset == 0 && viewer.ScrollableWidth > 0, "fixture starts at leftmost offset");
        var last = (Button)items[^1];
        int clicks = 0;
        last.Click += (_, _) => clicks++;
        // This is the real bubbling WPF keyboard-focus routed event, not physical keystrokes.
        var focus = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, null, last)
        {
            RoutedEvent = Keyboard.GotKeyboardFocusEvent,
        };
        last.RaiseEvent(focus);
        viewer.UpdateLayout();
        Check(viewer.HorizontalOffset > 0, "focus on an offscreen descendant requests WPF scrolling");
        Check(clicks == 0, "bringing a task into view never activates it");
    }

    private static void LegacyRowModes()
    {
        using var scope = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow", "RowCount");
        scope.Set("TaskRowRules", new List<TaskRowRule>());
        scope.Set("TaskEndApps", "");
        scope.Set("TaskEndAppsBottomRow", false);
        scope.Set("RowCount", 1);
        var row = new TaskRowPanel { Orientation = Orientation.Horizontal };
        var children = Enumerable.Range(0, 5).Select(_ => new Border { Width = 80, Height = 19 }).ToArray();
        foreach (var child in children) row.Children.Add(child);
        var viewer = new ScrollViewer
        {
            Content = row,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        viewer.Measure(new Size(170, 70));
        viewer.Arrange(new Rect(0, 0, 170, 70));
        viewer.UpdateLayout();
        Check(Y(children[2]) >= 19, "no-rule horizontal WrapPanel wraps naturally instead of growing extent");
        Check(viewer.ScrollableWidth == 0, "no-rule horizontal mode does not gain new scrolling");

        row.Orientation = Orientation.Vertical;
        viewer.Measure(new Size(100, 45));
        viewer.Arrange(new Rect(0, 0, 100, 45));
        viewer.UpdateLayout();
        Check(viewer.ScrollableHeight > 0, "vertical taskbar retains vertical navigation");
        Check(viewer.ScrollableWidth == 0, "vertical taskbar does not gain horizontal navigation");
        viewer.ScrollToBottom();
        viewer.UpdateLayout();
        Check(viewer.VerticalOffset > 0, "vertical scrolling reaches later tasks");
    }

    private static void PerAppRowPresentation()
    {
        using var scope = new LayoutSettingsScope("TaskButtonAppRules", "TaskButtonShowIcons", "TaskButtonShowLabels",
            "TaskbarScale", "Edge");
        scope.Set("TaskbarScale", 1.0);
        scope.Set("Edge", AppBarEdge.Bottom);
        scope.Set("TaskButtonShowIcons", true);
        scope.Set("TaskButtonShowLabels", true);
        var iconRule = new TaskButtonAppRule { App = "icon.exe", Width = TaskButtonRules.IconOnly };
        var wideRule = new TaskButtonAppRule { App = "wide.exe", Width = TaskButtonRules.WideWidth };
        scope.Set("TaskButtonAppRules", new List<TaskButtonAppRule> { iconRule, wideRule });
        var converter = new TaskButtonWidthConverter();
        object[] Values(string name, double width) => new object[]
        {
            0, width, 0, 2, FixtureApp(name), new List<TaskButtonAppRule> { iconRule, wideRule }, 3.0, 48.0, 160.0
        };
        Check((double)converter.Convert(Values("icon", 110), typeof(double), null, null) == 48,
            "icon-only app remains compact even when a row overflows");
        Check((double)converter.Convert(Values("wide", 85), typeof(double), null, null) == 85,
            "wide per-app setting honours available compressed width");
        Check((double)converter.Convert(Values("wide", 175), typeof(double), null, null) > 175,
            "uncompressed row still honours an explicit wide per-app button");
        Check(!TaskButtonRules.ShowLabel(iconRule) && TaskButtonRules.ShowIcon(iconRule),
            "icon-only label is suppressed while the icon remains");
        wideRule.Label = AppOverrideOption.Hide;
        wideRule.Icon = AppOverrideOption.Hide;
        Check(!TaskButtonRules.ShowLabel(wideRule) && !TaskButtonRules.ShowIcon(wideRule),
            "explicit per-app icon/label choices remain independent of row navigation");
    }

    private static void TaskNavigationTemplate()
    {
        using Stream stream = typeof(TaskRowPanel).Assembly.GetManifestResourceStream("RetroBar.Themes.System.xaml");
        Check(stream != null, "the production embedded theme is available for structural inspection");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var doc = XDocument.Load(stream);
        var style = doc.Descendants(p + "Style").Single(e => (string)e.Attribute(x + "Key") == "TaskListScrollViewer");
        var templates = style.Descendants(p + "ControlTemplate").ToArray();
        Check(templates.Length == 2, "horizontal and legacy vertical ScrollViewer templates both exist");
        string horizontal = templates[0].ToString();
        string vertical = templates[1].ToString();
        Check(horizontal.Contains("ScrollBar.PageLeftCommand") && horizontal.Contains("ScrollBar.PageRightCommand"),
            "production horizontal template has both navigation commands");
        Check(horizontal.Contains("ComputedHorizontalScrollBarVisibility"),
            "horizontal arrows are displayed only when scroll overflow is computed");
        Check(horizontal.Contains("ComputedVerticalScrollBarVisibility"),
            "horizontal template retains legacy vertical overflow controls");
        Check(!vertical.Contains("PageLeftCommand") && vertical.Contains("PageUpCommand"),
            "vertical template remains the original top/bottom navigation");
    }
}
