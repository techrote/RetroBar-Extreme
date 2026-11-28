using ManagedShell.AppBar;
using ManagedShell.WindowsTasks;
using ManagedShell.Common.Helpers;
using RetroBar.Utilities;
using System;
using System.ComponentModel;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RetroBar.Controls
{
    /// <summary>
    /// Interaction logic for TaskList.xaml
    /// </summary>
    public partial class TaskList : UserControl
    {
        private bool isLoaded;
        private bool isVerticalScrollable;
        private double DefaultButtonWidth;
        private double MinButtonWidth;
        private double TaskButtonLeftMargin;
        private double TaskButtonRightMargin;
        private ICollectionView taskbarItems;
        private readonly TaskWindowOrderTracker _windowOrder = new();
        private Dictionary<IntPtr, long> _originalOrder = new();
        private bool _sorting;
        private readonly ObservableCollection<TaskBundleEntry> _bundleEntries = new();
        private readonly HashSet<string> _inlineExpanded = new(StringComparer.OrdinalIgnoreCase);
        private bool _bundlesVisible;

        private sealed class RectangleTarget
        {
            internal ApplicationWindow Window;
            internal FrameworkElement Element;
            internal bool IsBundleHeader;
        }
        private readonly List<RectangleTarget> _rectangleTargets = new();

        internal void RegisterRectangleTarget(ApplicationWindow window, FrameworkElement element, bool header)
        {
            if (window == null || element == null ||
                _rectangleTargets.Any(t => ReferenceEquals(t.Window, window) &&
                    ReferenceEquals(t.Element, element) && t.IsBundleHeader == header))
                return;
            _rectangleTargets.Add(new RectangleTarget { Window = window, Element = element, IsBundleHeader = header });
        }

        internal void UnregisterRectangleTarget(ApplicationWindow window, FrameworkElement element, bool header)
        {
            _rectangleTargets.RemoveAll(t => ReferenceEquals(t.Window, window) &&
                ReferenceEquals(t.Element, element) && t.IsBundleHeader == header);
        }

        // Only live WPF button/header representations count as candidates. Merely
        // including an app in a display profile is not enough: a filtered-out or
        // collapsed item must never win the election.
        internal bool HasVisibleRectangleTarget(ApplicationWindow window)
        {
            if (window == null || !IsLoaded || !IsVisible || taskbarItems == null ||
                !taskbarItems.Contains(window) || !Tasks_Filter(window))
                return false;
            bool bundled = IsBundledWindow(window);
            return _rectangleTargets.Any(t => ReferenceEquals(t.Window, window) &&
                MatchesRectRepresentation(t.Element.IsVisible, bundled, t.IsBundleHeader));
        }

        public bool BundleExpanded(string key) => _inlineExpanded.Contains(key ?? "");

        internal bool IsBundledWindow(ApplicationWindow window) =>
            window != null && _bundlesVisible && _bundleEntries.Any(entry =>
                entry.IsBundle && entry.Windows.Any(member => ReferenceEquals(member, window)));

        internal static bool MatchesRectRepresentation(bool visible, bool bundled, bool header) =>
            visible && bundled == header;

        internal bool CanOwnButtonRect(ApplicationWindow window, FrameworkElement target, bool isBundleHeader)
        {
            if (window == null || target == null || Host == null ||
                !MatchesRectRepresentation(target.IsVisible, IsBundledWindow(window), isBundleHeader) ||
                !_rectangleTargets.Any(t => ReferenceEquals(t.Window, window) &&
                    ReferenceEquals(t.Element, target) && t.IsBundleHeader == isBundleHeader))
                return false;
            return Host.windowManager?.IsWindowRectangleOwner(Host, window) == true;
        }
        public void SetBundleExpanded(string key, bool expanded)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (expanded) _inlineExpanded.Add(key);
            else _inlineExpanded.Remove(key);
            // Horizontal overflow belongs to the horizontal axis, not the
            // vertical task-list scrollbar. Collapse restores ordinary wrapping.
            RefreshHorizontalBundleAccess();
            SetTaskButtonWidth();
        }

        // One horizontal-access policy for both native tasks and bundle projections.
        // Preserve the old three-argument contract seam for Inline-only tests.
        internal static void ConfigureHorizontalTaskAccess(ScrollViewer viewer, Orientation orientation,
            bool ruleConstrained, bool expandedInline)
        {
            viewer.HorizontalScrollBarVisibility = orientation == Orientation.Horizontal &&
                (ruleConstrained || expandedInline) ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        }

        internal static void ConfigureHorizontalBundleAccess(ScrollViewer viewer, Orientation orientation, bool expandedInline) =>
            ConfigureHorizontalTaskAccess(viewer, orientation, false, expandedInline);

        private bool HasExpandedInline => _bundlesVisible && _bundleEntries.Any(entry =>
            entry.IsBundle && entry.Expansion == TaskBundleExpansion.Inline &&
            _inlineExpanded.Contains(entry.Key));

        private void RefreshHorizontalBundleAccess()
        {
            Orientation orientation = Host?.Orientation ??
                ((Settings.Instance.Edge == AppBarEdge.Left || Settings.Instance.Edge == AppBarEdge.Right)
                    ? Orientation.Vertical : Orientation.Horizontal);
            ConfigureHorizontalTaskAccess(TasksScrollViewer, orientation, TaskRowPanel.HasRules, HasExpandedInline);
        }

        public static DependencyProperty ButtonWidthProperty = DependencyProperty.Register(nameof(ButtonWidth), typeof(double), typeof(TaskList), new PropertyMetadata(new double()));

        public double ButtonWidth
        {
            get { return (double)GetValue(ButtonWidthProperty); }
            set { SetValue(ButtonWidthProperty, value); }
        }

        public static DependencyProperty ButtonsPerRowProperty = DependencyProperty.Register(nameof(ButtonsPerRow), typeof(int), typeof(TaskList), new PropertyMetadata(0));

        public int ButtonsPerRow
        {
            get { return (int)GetValue(ButtonsPerRowProperty); }
            set { SetValue(ButtonsPerRowProperty, value); }
        }

        // Number of columns in a full row that get one extra pixel, so the row fills the
        // taskbar exactly instead of leaving the floored remainder empty.
        // UXUI-Extreme (UX-014): per-app task button widths
        public static DependencyProperty ButtonMarginTotalProperty = DependencyProperty.Register(nameof(ButtonMarginTotal), typeof(double), typeof(TaskList), new PropertyMetadata(0.0));
        public double ButtonMarginTotal
        {
            get { return (double)GetValue(ButtonMarginTotalProperty); }
            set { SetValue(ButtonMarginTotalProperty, value); }
        }

        public static DependencyProperty IconOnlyWidthProperty = DependencyProperty.Register(nameof(IconOnlyWidth), typeof(double), typeof(TaskList), new PropertyMetadata(0.0));
        public double IconOnlyWidth
        {
            get { return (double)GetValue(IconOnlyWidthProperty); }
            set { SetValue(IconOnlyWidthProperty, value); }
        }

        public static DependencyProperty DefaultWidthWithMarginProperty = DependencyProperty.Register(nameof(DefaultWidthWithMargin), typeof(double), typeof(TaskList), new PropertyMetadata(0.0));
        public double DefaultWidthWithMargin
        {
            get { return (double)GetValue(DefaultWidthWithMarginProperty); }
            set { SetValue(DefaultWidthWithMarginProperty, value); }
        }

        public static DependencyProperty ExtraWidthCountProperty = DependencyProperty.Register(nameof(ExtraWidthCount), typeof(int), typeof(TaskList), new PropertyMetadata(0));

        public int ExtraWidthCount
        {
            get { return (int)GetValue(ExtraWidthCountProperty); }
            set { SetValue(ExtraWidthCountProperty, value); }
        }

        public static DependencyProperty TasksProperty = DependencyProperty.Register(nameof(Tasks), typeof(Tasks), typeof(TaskList), new PropertyMetadata(TasksChangedCallback));

        public Tasks Tasks
        {
            get { return (Tasks)GetValue(TasksProperty); }
            set { SetValue(TasksProperty, value); }
        }

        // UXUI-Extreme: the grip left of the task buttons resizes the quick launch area.
        private void QuickLaunchGrip_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.Thumb grip && Host != null)
            {
                grip.Cursor = Host.Orientation == Orientation.Horizontal ? System.Windows.Input.Cursors.SizeWE : System.Windows.Input.Cursors.SizeNS;
            }
        }

        private void QuickLaunchGrip_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            Host?.ResizeQuickLaunch(Host.Orientation == Orientation.Horizontal ? e.HorizontalChange : e.VerticalChange);
        }

        private void QuickLaunchGrip_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Settings.Instance.QuickLaunchLength = 0;
        }

        private void QuickLaunchGrip_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Host?.FinishQuickLaunchResize();
        }

        public static DependencyProperty HostProperty = DependencyProperty.Register(nameof(Host), typeof(Taskbar), typeof(TaskList), new PropertyMetadata(TasksChangedCallback));

        public Taskbar Host
        {
            get { return (Taskbar)GetValue(HostProperty); }
            set { SetValue(HostProperty, value); }
        }

        public TaskList()
        {
            InitializeComponent();
        }

        private void SetStyles()
        {
            DefaultButtonWidth = Application.Current.FindResource("TaskButtonWidth") as double? ?? 0;
            MinButtonWidth = Application.Current.FindResource("TaskButtonMinWidth") as double? ?? 0;

            // UXUI-Extreme (UX-014): all-apps width (px at 100%) instead of the theme's.
            if (Settings.Instance.TaskButtonWidthPx > 0)
            {
                DefaultButtonWidth = Settings.Instance.TaskButtonWidthPx * (ScaleModel.IsNative ? Settings.Instance.TaskbarScale : 1.0);
            }
            Thickness buttonMargin;

            if ((Host?.AppBarEdge ?? Settings.Instance.Edge) == AppBarEdge.Left || (Host?.AppBarEdge ?? Settings.Instance.Edge) == AppBarEdge.Right)
            {
                buttonMargin = Application.Current.FindResource("TaskButtonVerticalMargin") as Thickness? ?? new Thickness();
            }
            else
            {
                buttonMargin = Application.Current.FindResource("TaskButtonMargin") as Thickness? ?? new Thickness();
            }

            TaskButtonLeftMargin = buttonMargin.Left;
            TaskButtonRightMargin = buttonMargin.Right;

            // UXUI-Extreme (UX-014): for per-app widths (TaskButtonWidthConverter)
            ButtonMarginTotal = buttonMargin.Left + buttonMargin.Right;
            IconOnlyWidth = MinButtonWidth + ButtonMarginTotal;
            DefaultWidthWithMargin = DefaultButtonWidth + ButtonMarginTotal;
        }

        public void ApplyProfileRefresh()
        {
            if (!isLoaded) return;
            SetStyles();
            taskbarItems?.Refresh();
            ApplyAffinityOrder();
            SetTaskButtonWidth();
        }

        private void TaskList_OnLoaded(object sender, RoutedEventArgs e)
        {
            SetStyles();
            SetTasksCollection();
        }

        private void SetTasksCollection()
        {
            if (!isLoaded && Tasks != null && Host != null)
            {
                taskbarItems = Tasks.CreateGroupedWindowsCollection();
                if (taskbarItems != null)
                {
                    taskbarItems.CollectionChanged += GroupedWindows_CollectionChanged;
                    taskbarItems.Filter = Tasks_Filter;
                }

                TasksList.ItemsSource = taskbarItems;
                BundledTasksList.ItemsSource = _bundleEntries;
                ApplyAffinityOrder();

                Settings.Instance.PropertyChanged += Settings_PropertyChanged;
                Host.hotkeyManager.TaskbarHotkeyPressed += TaskList_TaskbarHotkeyPressed;

                isLoaded = true;
            }
        }

        private static void TasksChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TaskList taskList && e.OldValue == null && e.NewValue != null)
            {
                taskList.SetTasksCollection();
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.TaskArrangement) ||
                e.PropertyName == nameof(Settings.TaskAffinityGroups) ||
                e.PropertyName == nameof(Settings.TaskApplicationPolicies))
            {
                ApplyAffinityOrder();
                return;
            }
            if (e.PropertyName == nameof(Settings.TaskButtonWidthPx) ||
                e.PropertyName == nameof(Settings.TaskbarScale) ||
                e.PropertyName == nameof(Settings.ScalingMode) ||
                e.PropertyName == nameof(Settings.Theme) ||
                e.PropertyName == nameof(Settings.Edge))
            {
                SetStyles();
                SetTaskButtonWidth();
                return;
            }
            if (e.PropertyName == nameof(Settings.TaskRowRules) ||
                e.PropertyName == nameof(Settings.TaskEndApps) ||
                e.PropertyName == nameof(Settings.TaskEndAppsBottomRow) ||
                e.PropertyName == nameof(Settings.TaskButtonAppRules) ||
                e.PropertyName == nameof(Settings.RowCount))
            {
                SetTaskButtonWidth();
                return;
            }

            if (e.PropertyName == nameof(Settings.MultiMonMode))
            {
                taskbarItems?.Refresh();
            }
            else if (e.PropertyName == nameof(Settings.ShowMultiMon))
            {
                if (Settings.Instance.MultiMonMode != MultiMonOption.AllTaskbars)
                {
                    taskbarItems?.Refresh();
                }
            }
        }
        private void TaskList_TaskbarHotkeyPressed(object sender, HotkeyManager.TaskbarHotkeyEventArgs e)
        {
            if (Settings.Instance.WinNumHotkeysAction == WinNumHotkeysOption.SwitchTasks && Host.IsShellHost)
            {
                try
                {
                    ApplicationWindow window = null;
                    if (_bundlesVisible)
                    {
                        if (e.index >= 0 && e.index < _bundleEntries.Count)
                            window = _bundleEntries[e.index].Representative;
                    }
                    else if (taskbarItems.MoveCurrentToPosition(e.index))
                    {
                        window = taskbarItems.CurrentItem as ApplicationWindow;
                    }

                    if (window != null)
                    {

                        if (e.isShiftPressed)
                        {
                            // Open new instance when Shift is pressed
                            ShellHelper.StartProcess(window.IsUWP ? "appx:" + window.AppUserModelID : window.WinFileName);
                        }
                        else
                        {
                            // Normal behavior - switch to existing window
                            if (window.State == ApplicationWindow.WindowState.Active && window.CanMinimize)
                            {
                                window.Minimize();
                            }
                            else
                            {
                                window.BringToFront();
                            }
                        }
                    }

                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        private bool Tasks_Filter(object obj)
        {
            if (obj is ApplicationWindow window)
            {
                if (!window.ShowInTaskbar)
                {
                    return false;
                }

                if (Host.DisplayProfile != null)
                {
                    var profile = Host.DisplayProfile;
                    if (!DisplayTaskbarProfiles.AllowsApplication(TaskButtonRules.AppKey(window), profile))
                        return false;
                    if (profile.WindowSource == DisplayWindowSource.AllMonitors)
                        return true;
                    return window.HMonitor == Host.Screen.HMonitor ||
                        (Host.IsShellHost && !Host.windowManager.IsValidHMonitor(window.HMonitor));
                }

                if (!Settings.Instance.ShowMultiMon || Settings.Instance.MultiMonMode == MultiMonOption.AllTaskbars)
                {
                    return true;
                }

                if (Settings.Instance.MultiMonMode == MultiMonOption.SameAsWindowAndPrimary && Host.Screen.Primary)
                {
                    return true;
                }

                IntPtr hMonitor = window.HMonitor;
                if (Host.Screen.Primary && !Host.windowManager.IsValidHMonitor(hMonitor))
                {
                    return true;
                }

                if (hMonitor != Host.Screen.HMonitor)
                {
                    return false;
                }
            }

            return true;
        }

        private void TaskList_OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (taskbarItems != null)
            {
                taskbarItems.CollectionChanged -= GroupedWindows_CollectionChanged;
                taskbarItems.Filter = null;
            }

            if (Host != null)
            {
                Host.hotkeyManager.TaskbarHotkeyPressed -= TaskList_TaskbarHotkeyPressed;
            }

            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;

            _rectangleTargets.Clear();
            isLoaded = false;
        }

        private void GroupedWindows_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // A source reset may mean closed HWNDs were removed. Reconcile
            // identity history and the projected bundle list for every change.
            // Sort-induced resets are guarded by _sorting.
            if (!_sorting)
                ApplyAffinityOrder();
            SetTaskButtonWidth();
        }

        private void RememberWindowOrder()
        {
            if (taskbarItems == null) return;
            _originalOrder = _windowOrder.Reconcile(taskbarItems.OfType<ApplicationWindow>()
                .Select(window => (window.Handle, (object)window)));
        }

        private void ApplyAffinityOrder()
        {
            if (taskbarItems is not ListCollectionView view || _sorting) return;
            _sorting = true;
            try
            {
                var mode = TaskAffinityRules.ModeFor(Host?.DisplayProfile);
                if (mode == TaskArrangementMode.Separate)
                {
                    if (view.CustomSort != null) view.CustomSort = null;
                }
                else
                {
                    RememberWindowOrder();
                    view.CustomSort = new TaskAffinityComparer(Settings.Instance.TaskAffinityGroups,
                        _originalOrder, taskbarItems.OfType<ApplicationWindow>().ToList(), Host?.DisplayProfile);
                }
            }
            finally { _sorting = false; }
            RebuildBundleEntries();
        }

        private void RebuildBundleEntries()
        {
            if (taskbarItems == null || Host == null) return;
            var mode = TaskAffinityRules.ModeFor(Host.DisplayProfile);
            _bundlesVisible = mode == TaskArrangementMode.Bundle || mode == TaskArrangementMode.Hybrid;
            TasksList.Visibility = _bundlesVisible ? Visibility.Collapsed : Visibility.Visible;
            BundledTasksList.Visibility = _bundlesVisible ? Visibility.Visible : Visibility.Collapsed;
            if (!_bundlesVisible)
            {
                _bundleEntries.Clear();
                RefreshHorizontalBundleAccess();
                return;
            }

            var buckets = new Dictionary<string, TaskBundleEntry>(StringComparer.OrdinalIgnoreCase);
            var ordered = new List<TaskBundleEntry>();
            foreach (var window in taskbarItems.OfType<ApplicationWindow>())
            {
                string app = TaskButtonRules.AppKey(window);
                // Missing executable identities must never collapse unrelated windows.
                if (string.IsNullOrWhiteSpace(app)) app = "unknown:" + window.Handle.ToInt64();
                bool combine = TaskAffinityRules.PresentationFor(app, Host.DisplayProfile) ==
                               TaskApplicationPresentation.Bundle;
                string bucket = "bundle:" + app.ToLowerInvariant();
                if (!combine || !buckets.TryGetValue(bucket, out var item))
                {
                    item = new TaskBundleEntry
                    {
                        Key = combine ? bucket : "single:" + window.Handle.ToInt64(),
                        CanBundle = combine,
                        Expansion = TaskAffinityRules.ExpansionFor(app, Host.DisplayProfile)
                    };
                    if (combine) buckets[bucket] = item;
                    ordered.Add(item);
                }
                item.Windows.Add(window);
            }

            _bundleEntries.Clear();
            foreach (var item in ordered) _bundleEntries.Add(item);
            RefreshHorizontalBundleAccess();
            SetTaskButtonWidth();
        }

        private void TaskList_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshHorizontalBundleAccess();
            SetTaskButtonWidth();
        }

        private void BundledTasksList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_bundlesVisible)
                SetTaskButtonWidth();
        }

        private void TasksScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // The ScrollViewer, not ItemsControl's expanding extent, owns the finite width.
            if (e.WidthChanged)
            {
                SetTaskButtonWidth();
            }
        }

        private void SetTaskButtonWidth()
        {
            if (Host is null)
                return; // The state is trashed, but presumably it's just a transition

            ConfigureButtonLayout(Host.Orientation, Host.Rows);
        }

        // A single production sizing routine also permits a WPF layout fixture to supply
        // rows/orientation without creating a shell-managed AppBarWindow or taskbar instance.
        private void ConfigureButtonLayout(Orientation orientation, int rowCount)
        {
            bool ruleConstrained = orientation == Orientation.Horizontal && TaskRowPanel.HasRules;
            bool expandedInline = orientation == Orientation.Horizontal && HasExpandedInline;
            ConfigureHorizontalTaskAccess(TasksScrollViewer, orientation, ruleConstrained, expandedInline);

            if (orientation == Orientation.Vertical)
            {
                ExtraWidthCount = 0;
                ButtonWidth = ActualWidth;
                SetScrollable(true); // while technically not always scrollable, we don't run into DPI-specific issues with it enabled while vertical
                return;
            }

            int rows = Math.Max(1, rowCount);

            int taskCount = _bundlesVisible ? BundledTasksList.Items.Count : TasksList.Items.Count;
            TasksList.AlternationCount = taskCount; // keep in sync for correct AlternationIndex
            BundledTasksList.AlternationCount = taskCount;

            // Use the finite outer scroll host, never the growing ItemsControl
            // extent or the arrow-reduced content viewport. The panel separately
            // preflights its no-arrow row layout before deciding whether the
            // navigation chrome is really necessary. Fixed widths are essential
            // for that comparison to remain deterministic across layout passes.
            double viewport = TasksScrollViewer.ActualWidth;
            if (!double.IsFinite(viewport) || viewport <= 0)
            {
                viewport = ActualWidth;
            }
            if (!double.IsFinite(viewport) || viewport <= 0)
            {
                return; // A later size-change supplies the real viewport.
            }

            double margin = TaskButtonLeftMargin + TaskButtonRightMargin;
            ButtonsPerRow = Math.Max(1, (int)Math.Ceiling((double)taskCount / rows));
            double maxWidth = viewport / ButtonsPerRow;
            double defaultWidth = DefaultButtonWidth + margin;
            double minWidth = MinButtonWidth + margin;

            if (ruleConstrained || expandedInline)
            {
                // Row ownership may concentrate every task in a single physical row.
                // Averaging the task count over all configured rows silently squeezes
                // requested button widths before the horizontal access policy can work.
                // Inline members likewise retain their requested widths. Keep the
                // theme/user width (and per-app overrides); let TaskRowPanel
                // extend the scroll extent when the permitted row has insufficient room.
                ExtraWidthCount = 0;
                ButtonWidth = Math.Max(minWidth, defaultWidth);
                SetScrollable(true);
                return;
            }

            if (maxWidth > defaultWidth)
            {
                // Room to spare: keep the default width and leave the trailing gap.
                ExtraWidthCount = 0;
                ButtonWidth = defaultWidth;
                SetScrollable(false);
            }
            else if (maxWidth < minWidth)
            {
                ExtraWidthCount = 0;
                ButtonWidth = Math.Ceiling(defaultWidth / 2);
                SetScrollable(true);
            }
            else
            {
                // Buttons are shrunk to fit a full row: spread the pixels lost to flooring
                // across the columns so the row fills the taskbar.
                double baseWidth = Math.Floor(maxWidth);
                ButtonWidth = baseWidth;
                ExtraWidthCount = Math.Max(0, (int)Math.Floor(viewport) - (int)baseWidth * ButtonsPerRow);
                SetScrollable(false);
            }
        }

        private void SetScrollable(bool canScroll)
        {
            if (canScroll == isVerticalScrollable) return;

            TasksScrollViewer.VerticalScrollBarVisibility = canScroll
                ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            isVerticalScrollable = canScroll;
        }

        private void TasksScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!isVerticalScrollable && Settings.Instance.TaskWheelAction == TaskWheelActionOption.DoNothing)
            {
                e.Handled = true;
            }
        }
    }
}