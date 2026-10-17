using ManagedShell.AppBar;
using ManagedShell.WindowsTasks;
using ManagedShell.Common.Helpers;
using RetroBar.Utilities;
using System;
using System.ComponentModel;
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
        private bool isScrollable;
        private double DefaultButtonWidth;
        private double MinButtonWidth;
        private double TaskButtonLeftMargin;
        private double TaskButtonRightMargin;
        private ICollectionView taskbarItems;

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

            if (Settings.Instance.Edge == AppBarEdge.Left || Settings.Instance.Edge == AppBarEdge.Right)
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
            if (e.PropertyName == nameof(Settings.TaskButtonWidthPx)) // UXUI-Extreme (UX-014)
            {
                SetStyles();
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
            if (Settings.Instance.WinNumHotkeysAction == WinNumHotkeysOption.SwitchTasks && Host.Screen.Primary)
            {
                try
                {
                    bool exists = taskbarItems.MoveCurrentToPosition(e.index);

                    if (exists)
                    {
                        ApplicationWindow window = taskbarItems.CurrentItem as ApplicationWindow;

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

            isLoaded = false;
        }

        private void GroupedWindows_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SetTaskButtonWidth();
        }

        private void TaskList_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetTaskButtonWidth();
        }

        private void SetTaskButtonWidth()
        {
            if (Host is null)
                return; // The state is trashed, but presumably it's just a transition

            if (Settings.Instance.Edge == AppBarEdge.Left || Settings.Instance.Edge == AppBarEdge.Right)
            {
                ExtraWidthCount = 0;
                ButtonWidth = ActualWidth;
                SetScrollable(true); // while technically not always scrollable, we don't run into DPI-specific issues with it enabled while vertical
                return;
            }

            double height = ActualHeight;
            int rows = Host.Rows;

            int taskCount = TasksList.Items.Count;
            TasksList.AlternationCount = taskCount; // keep in sync for correct AlternationIndex

            double margin = TaskButtonLeftMargin + TaskButtonRightMargin;
            ButtonsPerRow = Math.Max(1, (int)Math.Ceiling((double)taskCount / rows));
            double maxWidth = TasksList.ActualWidth / ButtonsPerRow;
            double defaultWidth = DefaultButtonWidth + margin;
            double minWidth = MinButtonWidth + margin;

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
                ExtraWidthCount = (int)Math.Floor(TasksList.ActualWidth) - (int)baseWidth * ButtonsPerRow;
                SetScrollable(false);
            }
        }

        private void SetScrollable(bool canScroll)
        {
            if (canScroll == isScrollable) return;

            if (canScroll)
            {
                TasksScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                TasksScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }

            isScrollable = canScroll;
        }

        private void TasksScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (!isScrollable && Settings.Instance.TaskWheelAction == TaskWheelActionOption.DoNothing)
            {
                e.Handled = true;
            }
        }
    }
}