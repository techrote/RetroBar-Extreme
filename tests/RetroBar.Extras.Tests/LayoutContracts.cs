using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetroBar.Controls;
using RetroBar.Utilities;

internal static partial class Program
{
    // These checks run under the suite's clean-profile guard. Suppress settings persistence as well:
    // they exercise production change notifications, without creating or replacing settings.json.
    private sealed class LayoutSettingsScope : IDisposable
    {
        private readonly Type type = typeof(HotkeyManager).Assembly.GetType("RetroBar.Utilities.Settings", true);
        private readonly FieldInfo initializing;
        private readonly bool wasInitializing;
        private readonly Dictionary<string, object> original = new();
        internal object Instance { get; }
        internal Type Type => type;

        internal LayoutSettingsScope(params string[] properties)
        {
            initializing = type.GetField("_isInitializing", BindingFlags.NonPublic | BindingFlags.Static);
            Instance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            wasInitializing = (bool)initializing.GetValue(null);
            initializing.SetValue(null, true);
            foreach (string name in properties)
            {
                original.Add(name, type.GetProperty(name).GetValue(Instance));
            }
        }

        internal void Set(string name, object value) => type.GetProperty(name).SetValue(Instance, value);

        public void Dispose()
        {
            try
            {
                foreach (var pair in original)
                {
                    Set(pair.Key, pair.Value);
                }
            }
            finally
            {
                initializing.SetValue(null, wasInitializing);
            }
        }
    }

    private static void HiddenQuickLaunchHotkeys()
    {
        using var settings = new LayoutSettingsScope("WinNumHotkeysAction");
        settings.Set("WinNumHotkeysAction", WinNumHotkeysOption.InvokeQuickLaunch);
        // ShowDesktopButton.xaml inside the toolbar uses this App.xaml static resource. Supply
        // only that converter: constructing the real RetroBar.App would start shell services.
        const string converterKey = "menuTextRenderingModeConverter";
        ResourceDictionary resources = Application.Current.Resources;
        bool addedConverter = !resources.Contains(converterKey);
        if (addedConverter)
        {
            resources.Add(converterKey, new RetroBar.Converters.BoolToTextRenderingModeConverter());
        }
        try
        {
            var toolbar = new RetroBar.Controls.Toolbar { Visibility = Visibility.Collapsed };
            MethodInfo handler = typeof(RetroBar.Controls.Toolbar).GetMethod("Toolbar_TaskbarHotkeyPressed", BindingFlags.Instance | BindingFlags.NonPublic);
            Check(handler != null, "quick launch hotkey handler exists");

            // A hidden toolbar has no live folder. Dispatch the real handlers without constructing a
            // taskbar, starting the shell services, opening a folder, or registering a global hotkey.
            foreach (int index in new[] { 0, 9, -1, 10 })
            {
                handler.Invoke(toolbar, new object[] { null, new HotkeyManager.TaskbarHotkeyEventArgs { index = index } });
                Check(true, $"hidden/uninitialized quick launch safely ignores hotkey index {index}");
            }
        }
        finally
        {
            if (addedConverter) resources.Remove(converterKey);
        }
    }

    private static void HotkeyDisposal()
    {
        using var settings = new LayoutSettingsScope("WinNumHotkeysAction");
        settings.Set("WinNumHotkeysAction", WinNumHotkeysOption.WindowsDefault);
        var manager = new HotkeyManager();
        FieldInfo changed = settings.Type.GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo handler = typeof(HotkeyManager).GetMethod("Settings_PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo listenerField = typeof(HotkeyManager).GetField("_listenerWindow", BindingFlags.Instance | BindingFlags.NonPublic);
        var listener = (System.Windows.Forms.NativeWindow)listenerField.GetValue(manager);
        PropertyInfo registered = listener.GetType().GetProperty("IsRegistered", BindingFlags.Instance | BindingFlags.NonPublic);
        bool IsSubscribed() => ((Delegate)changed.GetValue(settings.Instance))?.GetInvocationList().Any(d => ReferenceEquals(d.Target, manager)) == true;

        try
        {
            // WindowsDefault creates only the private message window; it must not touch Explorer
            // hotkeys or register any global key combination on the test machine.
            Check(listener.Handle != IntPtr.Zero, "hotkey listener owns its message window");
            Check(!(bool)registered.GetValue(listener), "WindowsDefault registers no global hotkeys");
            Check(IsSubscribed(), "live manager observes settings changes");
            manager.Dispose();
            Check(listener.Handle == IntPtr.Zero, "Dispose destroys the message window");
            Check(!IsSubscribed(), "Dispose releases the singleton settings subscription");
            manager.Dispose();
            Check(listener.Handle == IntPtr.Zero, "repeated Dispose remains safe");
        }
        finally
        {
            manager.Dispose();
            // Keep the fixture safe even when run against the pre-fix implementation.
            var callback = (PropertyChangedEventHandler)Delegate.CreateDelegate(typeof(PropertyChangedEventHandler), manager, handler);
            settings.Type.GetEvent("PropertyChanged").RemoveEventHandler(settings.Instance, callback);
        }
    }

    private static void OverlappingRowAlignment()
    {
        using var settings = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow");
        settings.Set("TaskEndApps", "");
        settings.Set("TaskEndAppsBottomRow", false);

        foreach (var (alignment, expectedX) in new[]
        {
            (TaskButtonAlignOption.Right, 400.0),
            (TaskButtonAlignOption.Centre, 200.0),
            (TaskButtonAlignOption.Left, 0.0),
        })
        {
            settings.Set("TaskRowRules", new List<TaskRowRule>
            {
                new TaskRowRule { Row = 1, Align = TaskButtonAlignOption.Right },
                new TaskRowRule { Row = -1, Align = alignment },
            });
            var panel = new TaskRowPanel { Orientation = Orientation.Horizontal };
            var button = new Border { Width = 100, Height = 20 };
            panel.Children.Add(button);
            panel.Measure(new Size(500, 20));
            panel.Arrange(new Rect(0, 0, 500, 20));
            Vector offset = VisualTreeHelper.GetOffset(button);
            Check(Math.Abs(offset.X - expectedX) < 0.01, $"explicit/lowest row overlap uses final {alignment} alignment once: {offset.X}");
            Check(offset.X >= 0 && offset.X + button.ActualWidth <= 500, "aligned button stays within its row");
        }

        // The same collision occurs when an explicit second row becomes the lowest row.
        settings.Set("TaskRowRules", new List<TaskRowRule>
        {
            new TaskRowRule { Row = 1, Apps = "reserved-app", OnlyListed = true },
            new TaskRowRule { Row = 2, Align = TaskButtonAlignOption.Right },
            new TaskRowRule { Row = -1, Align = TaskButtonAlignOption.Centre },
        });
        var twoRows = new TaskRowPanel { Orientation = Orientation.Horizontal };
        var bottomButton = new Border { Width = 100, Height = 20 };
        twoRows.Children.Add(bottomButton);
        twoRows.Measure(new Size(500, 40));
        twoRows.Arrange(new Rect(0, 0, 500, 40));
        Vector bottomOffset = VisualTreeHelper.GetOffset(bottomButton);
        Check(Math.Abs(bottomOffset.X - 200) < 0.01 && Math.Abs(bottomOffset.Y - 20) < 0.01,
            "row assignment is retained while overlapping alignment is resolved once");
    }

    // The owner can now select sixteen physical taskbar rows. Check the public
    // selection range, legacy saved RowLimit=5 and row-16/lowest placement against
    // real STA WPF Measure/Arrange without constructing a shell taskbar.
    private static void SixteenRowCapacity()
    {
        using var settings = new LayoutSettingsScope("RowCount", "TaskRowRules",
            "TaskEndApps", "TaskEndAppsBottomRow");
        var rowLimit = settings.Type.GetProperty("RowLimit");
        var rowCount = settings.Type.GetProperty("RowCount");
        Check((int)rowLimit.GetValue(settings.Instance) == 16,
            "taskbar row count selector and drag-resize limit reach 16");

        // Prior settings.json snapshots normally contain RowLimit=5. They must
        // not retain the old cap after upgrading, and saved row counts survive.
        object oldProfile = JsonSerializer.Deserialize(
            """{"RowLimit":5,"RowCount":5}""", settings.Type);
        Check((int)rowLimit.GetValue(oldProfile) == 16 &&
              (int)rowCount.GetValue(oldProfile) == 5,
            "old five-row settings load with the new sixteen-row limit");
        rowCount.SetValue(oldProfile, 16);
        Check((int)rowCount.GetValue(oldProfile) == 16,
            "row 16 is accepted by the settings model");
        rowCount.SetValue(oldProfile, 17);
        Check((int)rowCount.GetValue(oldProfile) == 16,
            "row 17 is rejected instead of growing the taskbar beyond the limit");
        rowCount.SetValue(oldProfile, 0);
        Check((int)rowCount.GetValue(oldProfile) == 1,
            "row count stays at least one");

        var strings = (ResourceDictionary)Application.LoadComponent(
            new Uri("/RetroBar;component/Controls/LayoutStrings.xaml", UriKind.Relative));
        var choices = (string[])strings["uxui_rows_values"];
        Check(choices.Length == 17 && choices[0].Contains("1") &&
              choices[15].Contains("16") && choices[16] == "Lowest row",
            "Layout editor exposes rows 1..16 plus the relative lowest-row rule");
        var lastIndex = typeof(LayoutEditor).GetField(
            "LastRowChoice", BindingFlags.NonPublic | BindingFlags.Static);
        Check(lastIndex != null && (int)lastIndex.GetRawConstantValue() == 16,
            "relative lowest-row picker maps index 16 to row -1");

        settings.Set("RowCount", 16);
        settings.Set("TaskEndApps", "");
        settings.Set("TaskEndAppsBottomRow", false);
        settings.Set("TaskRowRules", new List<TaskRowRule>
        {
            new TaskRowRule { Row = 1, Apps = "first", OnlyListed = true },
            new TaskRowRule { Row = 16, Apps = "last", OnlyListed = true },
            new TaskRowRule { Row = -1, Align = TaskButtonAlignOption.Right },
        });
        var (viewer, panel, items) = RowFixture(16, 180,
            ("first", 92), ("ordinary", 82),
            ("last", 90), ("last", 90), ("last", 90));
        Check(Math.Abs(Y(items[0])) < 0.5,
            "explicit top-row rule remains on row 1");
        Check(Math.Abs(Y(items[1]) - 18) < 0.5,
            "unmatched task avoids the exclusive first and sixteenth rows");
        foreach (int index in new[] { 2, 3, 4 })
            Check(Math.Abs(Y(items[index]) - 15 * 18) < 0.5,
                "explicit row 16 matches relative lowest physical row");
        Check(panel.DesiredSize.Height >= 16 * 18,
            "the content extent includes all sixteen physical rows");
        Check(viewer.ScrollableWidth > 0,
            "overflow on row 16 exposes horizontal navigation");
        viewer.ScrollToRightEnd();
        viewer.UpdateLayout();
        Check(viewer.HorizontalOffset > 0,
            "the last task on row 16 remains reachable");
    }

    // Regression for #37: ScrollContentPresenter measures the content with infinite width.
    // A rule that reserves row 1 must still keep every unmatched button in row 2;
    // the second row may extend horizontally, but may not silently reassign tasks to row 1.
    private static void ConstrainedRowOverflowRed()
    {
        using var settings = new LayoutSettingsScope("TaskRowRules", "TaskEndApps", "TaskEndAppsBottomRow", "RowCount");
        settings.Set("TaskEndApps", "");
        settings.Set("TaskEndAppsBottomRow", false);
        settings.Set("RowCount", 2);
        settings.Set("TaskRowRules", new List<TaskRowRule>
        {
            new TaskRowRule { Row = 1, Apps = "reserved-app", OnlyListed = true },
        });

        var panel = new TaskRowPanel { Orientation = Orientation.Horizontal };
        var buttons = Enumerable.Range(0, 9).Select(_ => new Border { Width = 92, Height = 18 }).ToArray();
        foreach (var button in buttons)
            panel.Children.Add(button);

        var viewer = new ScrollViewer
        {
            Content = panel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        viewer.Measure(new Size(184, 48));
        viewer.Arrange(new Rect(0, 0, 184, 48));
        viewer.UpdateLayout();

        foreach (var button in buttons)
            Check(Math.Abs(VisualTreeHelper.GetOffset(button).Y - 18) < 0.5,
                "under infinite-width ScrollViewer measure, an exclusive rule keeps unmatched tasks in row 2");
        Check(viewer.ScrollableWidth >= 500, "overflowed constrained row contributes real horizontal extent");
        viewer.ScrollToRightEnd();
        viewer.UpdateLayout();
        Check(viewer.HorizontalOffset > 0, "WPF can scroll to the off-screen task buttons");
    }
}
