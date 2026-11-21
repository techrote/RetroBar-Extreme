using RetroBar.Utilities;
using RetroBar.Converters;
using ManagedShell.AppBar;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Markup;
using System.Windows.Media;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ManagedShell.WindowsTasks;
using ManagedShell.Interop;
using System.Reflection;
using RetroBar.Controls;

internal static partial class Program
{
    private static void TaskBundleContracts()
    {
        var entry = new TaskBundleEntry { Key = "bundle:firefox.exe", CanBundle = true, Expansion = TaskBundleExpansion.Inline };
        Check(entry.WindowCount == 0 && !entry.IsBundle, "Empty groups must not present a bundle.");
        entry.Windows.Add(null);
        Check(entry.WindowCount == 1 && !entry.IsBundle, "Single window stays an individual button.");
        entry.Windows.Add(null);
        Check(entry.WindowCount == 2 && entry.IsBundle, "Two windows enter Bundle presentation.");
        Check(entry.Expansion == TaskBundleExpansion.Inline, "Expansion policy is retained.");
        entry.CanBundle = false;
        Check(entry.WindowCount == 2 && !entry.IsBundle, "Separate presentation never combines windows.");
        entry.Windows.Clear();
        Check(entry.Representative == null && entry.SingleWindow == null && entry.DisplayName == "", "Empty representatives are safe.");
        var profile = new DisplayTaskbarProfile { ArrangementOverride = TaskArrangementMode.Hybrid };
        profile.ApplicationOverrides.Add(new TaskApplicationPolicy {
            Apps = "firefox.exe", Presentation = TaskApplicationPresentation.Bundle, Expansion = TaskBundleExpansion.Flyout });
        profile.ApplicationOverrides.Add(new TaskApplicationPolicy {
            Apps = "explorer.exe", Presentation = TaskApplicationPresentation.Separate, Expansion = TaskBundleExpansion.Inline });
        Check(TaskAffinityRules.PresentationFor("firefox.exe", profile) == TaskApplicationPresentation.Bundle, "Firefox bundles.");
        Check(TaskAffinityRules.ExpansionFor("firefox.exe", profile) == TaskBundleExpansion.Flyout, "Firefox uses flyout.");
        Check(TaskAffinityRules.PresentationFor("explorer.exe", profile) == TaskApplicationPresentation.Separate, "Explorer stays separate.");
        Check(TaskAffinityRules.ExpansionFor("explorer.exe", profile) == TaskBundleExpansion.Inline, "Dormant expansion settings persist.");
        Check(TaskAffinityRules.PresentationFor("pwsh.exe", profile) == TaskApplicationPresentation.Affinity, "Other apps retain affinity.");
        var outer = new TaskBundleEntryWidthConverter();
        var vertical = outer.Convert(new object[] { Orientation.Vertical, 172.0 }, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture);
        var horizontal = outer.Convert(new object[] { Orientation.Horizontal, 172.0 }, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture);
        Check(vertical is double vw && vw == 172.0, "Vertical bundle width follows the taskbar.");
        Check(horizontal is double hw && double.IsNaN(hw), "Horizontal bundle width grows with inline children.");
        var inner = new TaskBundleButtonWidthConverter();
        var fallback = inner.Convert(new object[] { new TaskBundleEntry(), 172.0, 0, 1, null, 0.0, 24.0, 172.0, AppBarEdge.Right },
            typeof(double), null, System.Globalization.CultureInfo.InvariantCulture);
        Check(fallback is double bw && bw == 172.0, "Bundle width adapter uses normal taskbar width when no representative is available.");
        BundleHorizontalNavigationContracts();
        BundleProductionVisualTreeContracts();
    }

    // Construct harmless ApplicationWindow identities without launching a
    // shell process, exactly as the existing task-row contract fixtures do.
    private static ApplicationWindow BundleFixtureApp(string name)
    {
        var app = new ApplicationWindow(null, IntPtr.Zero);
        var file = typeof(ApplicationWindow).GetField("_winFileName", BindingFlags.NonPublic | BindingFlags.Instance);
        var aumid = typeof(ApplicationWindow).GetField("_appUserModelId", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(file != null && aumid != null, "ManagedShell identity fixture fields are available.");
        file.SetValue(app, @"C:\Fixture\" + name + ".exe");
        aumid.SetValue(app, "fixture." + name);
        return app;
    }

    private static IEnumerable<T> BundleDescendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (T next in BundleDescendants<T>(child)) yield return next;
        }
    }

    private static void BundleHorizontalNavigationContracts()
    {
        // Load the actual embedded ScrollViewer template rather than a parallel
        // test-only layout or mock navigation buttons.
        using Stream stream = typeof(TaskList).Assembly.GetManifestResourceStream("RetroBar.Themes.System.xaml");
        Check(stream != null, "Bundled task navigation can load the production System theme.");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var doc = XDocument.Load(stream);
        string[] keys = { "TaskListScrollButton", "TaskListScrollButtonText", "TaskListScrollButtonGrid", "TaskListScrollViewer" };
        var resources = Application.Current.Resources;
        var previous = keys.ToDictionary(key => key,
            key => (Present: resources.Contains(key), Value: resources.Contains(key) ? resources[key] : null));
        try
        {
            foreach (string key in keys)
            {
                var node = doc.Descendants(p + "Style").Single(el => (string)el.Attribute(x + "Key") == key);
                resources[key] = XamlReader.Parse(node.ToString());
            }

            var expandedChildren = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < 20; i++)
                expandedChildren.Children.Add(new Button { Width = 96, Height = 22, Content = "window " + i });
            var entry = new StackPanel { Orientation = Orientation.Horizontal };
            entry.Children.Add(new Button { Width = 96, Height = 22, Content = "bundle" });
            entry.Children.Add(expandedChildren);
            var viewer = new ScrollViewer
            {
                Content = entry,
                Style = (Style)resources["TaskListScrollViewer"],
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var method = typeof(TaskList).GetMethod("ConfigureHorizontalBundleAccess",
                BindingFlags.Static | BindingFlags.NonPublic);
            Check(method != null, "Production bundle horizontal-mode helper exists.");
            void Apply(Orientation orientation, bool expanded) => method.Invoke(null, new object[] { viewer, orientation, expanded });
            void Layout(double width)
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    viewer.InvalidateMeasure();
                    viewer.Measure(new Size(width, 48));
                    viewer.Arrange(new Rect(0, 0, width, 48));
                    viewer.UpdateLayout();
                }
            }

            Apply(Orientation.Horizontal, true);
            Layout(210);
            Check(viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto &&
                  viewer.ScrollableWidth > 1000, "Twenty expanded horizontal windows expose real scroll extent.");
            var buttons = BundleDescendants<RepeatButton>(viewer).ToArray();
            var right = buttons.Single(b => b.Command == ScrollBar.PageRightCommand);
            var left = buttons.Single(b => b.Command == ScrollBar.PageLeftCommand);
            Check(((UIElement)VisualTreeHelper.GetParent(right)).Visibility == Visibility.Visible,
                "Production right page arrow appears during horizontal bundle overflow.");
            var peer = UIElementAutomationPeer.CreatePeerForElement(right) ?? new RepeatButtonAutomationPeer(right);
            Check(peer is IInvokeProvider, "Right arrow exposes an executable WPF automation action.");
            ((IInvokeProvider)peer).Invoke();
            viewer.UpdateLayout();
            Check(viewer.HorizontalOffset > 0, "Right navigation reaches later inline windows.");
            viewer.ScrollToRightEnd();
            viewer.UpdateLayout();
            Check(viewer.HorizontalOffset >= viewer.ScrollableWidth - 1,
                "Horizontal extent reaches the final expanded window.");
            expandedChildren.Visibility = Visibility.Collapsed;
            Apply(Orientation.Horizontal, false);
            Layout(210);
            Check(viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled &&
                  viewer.ScrollableWidth < 1, "Collapsing Inline restores the legacy non-horizontal-scroll layout.");
            Check(((UIElement)VisualTreeHelper.GetParent(right)).Visibility == Visibility.Collapsed,
                "Horizontal page arrows collapse with the bundle.");
            expandedChildren.Visibility = Visibility.Visible;
            Apply(Orientation.Vertical, true);
            Layout(210);
            Check(viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled,
                "Vertical taskbars never activate horizontal bundle navigation.");
        }
        finally
        {
            foreach (string key in keys)
            {
                if (previous[key].Present) resources[key] = previous[key].Value;
                else resources.Remove(key);
            }
        }
    }

    private static void BundleProductionVisualTreeContracts()
    {
        using var settings = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow",
            "RowCount", "Edge", "TaskButtonAppRules", "TaskButtonWidthPx");
        settings.Set("RowCount", 2);
        settings.Set("Edge", AppBarEdge.Bottom);
        settings.Set("TaskEndApps", "");
        settings.Set("TaskEndAppsBottomRow", false);
        settings.Set("TaskButtonWidthPx", 96.0);
        settings.Set("TaskRowRules", new List<TaskRowRule> {
            new TaskRowRule { Row = 2, Apps = "firefox", OnlyListed = true }
        });

        using Stream stream = typeof(TaskList).Assembly.GetManifestResourceStream("RetroBar.Themes.System.xaml");
        Check(stream != null, "Production TaskList theme is embedded in the application.");
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var document = XDocument.Load(stream);
        string[] themeKeys = { "TaskListScrollButton", "TaskListScrollButtonText",
            "TaskListScrollButtonGrid", "TaskListScrollViewer", "TaskListWrapPanel" };
        var supplied = new Dictionary<string, object> {
            ["TaskButton"] = new Style(typeof(Button)),
            ["TaskButtonActive"] = new Style(typeof(Button)),
            ["TaskButtonFlashing"] = new Style(typeof(Button)),
            ["TaskIcon"] = new Style(typeof(System.Windows.Controls.Image)),
            ["TaskLabel"] = new Style(typeof(System.Windows.Controls.TextBlock)),
            ["TaskOverlayIcon"] = new Style(typeof(System.Windows.Controls.Image)),
            ["TaskButtonMargin"] = new Thickness(0, 0, 3, 0),
            ["TaskButtonVerticalMargin"] = new Thickness(),
            ["TaskButtonWidth"] = 96.0,
            ["TaskButtonMinWidth"] = 44.0,
            ["menuTextRenderingModeConverter"] = new RetroBar.Converters.BoolToTextRenderingModeConverter()
        };
        var resources = Application.Current.Resources;
        var keys = themeKeys.Concat(supplied.Keys).Distinct().ToArray();
        var previous = keys.ToDictionary(key => key,
            key => (Present: resources.Contains(key), Value: resources.Contains(key) ? resources[key] : null));
        foreach (var kv in supplied) resources[kv.Key] = kv.Value;
        try
        {
            foreach (string key in themeKeys)
            {
                var node = document.Descendants(p + "Style").Single(el => (string)el.Attribute(x + "Key") == key);
                resources[key] = XamlReader.Parse(node.ToString());
            }
            var list = new TaskList {
                ButtonWidth = 96,
                ButtonsPerRow = 2,
                ButtonMarginTotal = 3,
                IconOnlyWidth = 47,
                DefaultWidthWithMargin = 99
            };
            var bundled = (ItemsControl)list.FindName("BundledTasksList");
            var legacy = (ItemsControl)list.FindName("TasksList");
            var scroll = (ScrollViewer)list.FindName("TasksScrollViewer");
            Check(bundled != null && legacy != null && scroll != null,
                "Production TaskList XAML exposes both actual renderers and scroll host.");
            var entries = (ObservableCollection<TaskBundleEntry>)typeof(TaskList)
                .GetField("_bundleEntries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(list);
            var firefox = new TaskBundleEntry { Key = "bundle:firefox.exe", CanBundle = true, Expansion = TaskBundleExpansion.Inline };
            for (int i = 0; i < 20; i++) firefox.Windows.Add(BundleFixtureApp("firefox"));
            var explorer = new TaskBundleEntry { Key = "single:explorer", CanBundle = false };
            explorer.Windows.Add(BundleFixtureApp("explorer"));
            entries.Add(explorer);
            entries.Add(firefox);
            bundled.ItemsSource = entries;
            legacy.Visibility = Visibility.Collapsed;
            bundled.Visibility = Visibility.Visible;
            typeof(TaskList).GetField("_bundlesVisible", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(list, true);
            list.SetBundleExpanded(firefox.Key, true);
            void Layout(double width, double height = 75)
            {
                for (int pass = 0; pass < 4; pass++)
                {
                    list.InvalidateMeasure();
                    list.Measure(new Size(width, height));
                    list.Arrange(new Rect(0, 0, width, height));
                    list.UpdateLayout();
                }
            }
            Layout(230);
            var rowPanel = BundleDescendants<TaskRowPanel>(bundled).Single();
            var views = BundleDescendants<TaskBundleEntryView>(bundled).ToArray();
            Check(views.Length == 2, "Actual bundled ItemTemplate renders two production entry views.");
            var firefoxView = views.Single(v => ReferenceEquals(v.DataContext, firefox));
            var explorerView = views.Single(v => ReferenceEquals(v.DataContext, explorer));
            var children = (ItemsControl)firefoxView.FindName("ExpandedWindows");
            var header = (Button)firefoxView.FindName("GroupButton");
            var visibleChildren = BundleDescendants<TaskButton>(children).ToArray();
            Check(header.Visibility == Visibility.Visible && children.Visibility == Visibility.Visible &&
                  visibleChildren.Length == 20, "Real bundle header and 20 native TaskButtons render inline.");
            Check(rowPanel.GetBindingExpression(TaskRowPanel.ViewportWidthProperty) != null &&
                  rowPanel.ViewportWidth > 0 && scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto,
                "Production bundled row panel receives a finite viewport from its real ScrollViewer.");
            Check(scroll.ScrollableWidth > 800, "Actual 20-window bundle creates horizontal overflow.");
            double firefoxTop = firefoxView.TransformToAncestor(rowPanel).Transform(new Point(0, 0)).Y;
            double explorerTop = explorerView.TransformToAncestor(rowPanel).Transform(new Point(0, 0)).Y;
            Check(firefoxTop > explorerTop + 8,
                "Explicit/exclusive firefox row rule survives horizontal Inline expansion.");
            var right = BundleDescendants<RepeatButton>(scroll)
                .Single(b => b.Command == ScrollBar.PageRightCommand);
            var peer = UIElementAutomationPeer.CreatePeerForElement(right) ?? new RepeatButtonAutomationPeer(right);
            Check(peer is IInvokeProvider, "Production bundle right-arrow supports WPF automation.");
            ((IInvokeProvider)peer).Invoke();
            scroll.UpdateLayout();
            Check(scroll.HorizontalOffset > 0, "Real bundled TaskList responds to right-page navigation.");
            scroll.ScrollToRightEnd();
            scroll.UpdateLayout();
            Check(scroll.HorizontalOffset >= scroll.ScrollableWidth - 1,
                "Production bundled TaskList can reach the final inline member.");

            // Verify the actual visible header is the single rectangle owner for
            // all 20 member HWND identities, independent of hidden legacy items.
            firefoxView.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            var members = (List<ApplicationWindow>)typeof(TaskBundleEntryView)
                .GetField("_rectMembers", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(firefoxView);
            Check(members.Count == 20 && members.All(w => firefox.Windows.Contains(w)),
                "Visible group header subscribes to each member window rectangle request.");
            var policy = typeof(TaskList).GetMethod("MatchesRectRepresentation",
                BindingFlags.Static | BindingFlags.NonPublic);
            Check((bool)policy.Invoke(null, new object[] { true, true, true }) &&
                  !(bool)policy.Invoke(null, new object[] { true, true, false }) &&
                  !(bool)policy.Invoke(null, new object[] { false, true, true }),
                "Only the visible bundle header can own bundle-member restore rectangles.");
            var headerBounds = header.TransformToAncestor(firefoxView)
                .TransformBounds(new Rect(0, 0, header.ActualWidth, header.ActualHeight));
            Check(headerBounds.Width > 0 && headerBounds.Height > 0 &&
                  headerBounds.Left >= -1 && headerBounds.Top >= -1,
                "The production visible bundle header has nonzero visual bounds.");
            firefoxView.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Check(members.Count == 0, "Unloaded bundle header releases all member-rectangle callbacks.");

            header.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Layout(230);
            Check(children.Visibility == Visibility.Collapsed &&
                  scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled,
                "Real bundle-header click collapses children and restores nonhorizontal scroll mode.");

            // Side-docked Inline must subtract the 8-DIP indentation even when a
            // per-app override requests full taskbar width.
            settings.Set("Edge", AppBarEdge.Left);
            var verticalHost = new TaskList {
                ButtonWidth = 172, ButtonsPerRow = 1, ButtonMarginTotal = 0,
                IconOnlyWidth = 24, DefaultWidthWithMargin = 172
            };
            var verticalEntries = (ObservableCollection<TaskBundleEntry>)typeof(TaskList)
                .GetField("_bundleEntries", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(verticalHost);
            verticalEntries.Add(firefox);
            typeof(TaskList).GetField("_bundlesVisible", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(verticalHost, true);
            verticalHost.SetBundleExpanded(firefox.Key, true);
            var sideView = new TaskBundleEntryView {
                Host = verticalHost, DataContext = firefox, Width = 172
            };
            ((StackPanel)sideView.FindName("EntryStack")).Orientation = Orientation.Vertical;
            sideView.Measure(new Size(172, 700));
            sideView.Arrange(new Rect(0, 0, 172, 700));
            sideView.UpdateLayout();
            var sideChildren = (ItemsControl)sideView.FindName("ExpandedWindows");
            foreach (var stack in BundleDescendants<StackPanel>(sideChildren))
                stack.Orientation = Orientation.Vertical;
            sideView.InvalidateMeasure();
            sideView.Measure(new Size(172, 700));
            sideView.Arrange(new Rect(0, 0, 172, 700));
            sideView.UpdateLayout();
            var sideButtons = BundleDescendants<TaskButton>(sideChildren).ToArray();
            Check(sideButtons.Length == 20 && sideChildren.Visibility == Visibility.Visible,
                "Production vertical Inline template renders 20 expanded windows.");
            Check(sideButtons.All(b => double.IsFinite(b.ActualWidth) && b.ActualWidth <= 164.5) &&
                  sideChildren.Margin.Left == 8 && sideView.ActualWidth <= 172.5,
                "Vertical Inline buttons fit the 172-DIP host after 8-DIP indentation.");
            Check(verticalHost.FindName("TasksScrollViewer") is ScrollViewer sideScroll &&
                  sideScroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled,
                "Vertical Inline never enables horizontal scroll access.");
        }
        finally
        {
            foreach (string key in keys)
            {
                if (previous[key].Present) resources[key] = previous[key].Value;
                else resources.Remove(key);
            }
        }
    }
}
