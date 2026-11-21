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
}
