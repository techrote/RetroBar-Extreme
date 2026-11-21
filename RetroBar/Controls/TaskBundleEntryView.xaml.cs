using ManagedShell.WindowsTasks;
using RetroBar.Utilities;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace RetroBar.Controls
{
    // Flyout displays one activation choice per HWND. Inline exposes the original TaskButton
    // for each window, preserving its normal context menu and click behaviour.
    public partial class TaskBundleEntryView : UserControl
    {
        public static readonly DependencyProperty HostProperty =
            DependencyProperty.Register(nameof(Host), typeof(TaskList), typeof(TaskBundleEntryView),
                new PropertyMetadata(null, (d, e) => ((TaskBundleEntryView)d).RefreshEntry()));
        public TaskList Host
        {
            get => (TaskList)GetValue(HostProperty);
            set => SetValue(HostProperty, value);
        }

        private TaskBundleEntry Entry => DataContext as TaskBundleEntry;

        public TaskBundleEntryView()
        {
            InitializeComponent();
        }

        private void Entry_Loaded(object sender, RoutedEventArgs e) => RefreshEntry();
        private void Entry_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => RefreshEntry();

        private void RefreshEntry()
        {
            if (SingleButton == null || GroupButton == null || ExpandedWindows == null) return;
            var entry = Entry;
            bool bundled = entry?.IsBundle == true;
            GroupButton.Visibility = bundled ? Visibility.Visible : Visibility.Collapsed;
            SingleButton.Visibility = bundled ? Visibility.Collapsed : Visibility.Visible;
            bool horizontal = Host?.Host?.Orientation == Orientation.Horizontal;
            EntryStack.Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
            // The outer entry must be free to grow when expanded horizontally; apply
            // fixed button widths to header/individual buttons, never to the whole entry.
            Width = horizontal ? double.NaN : (Host?.ButtonWidth ?? double.NaN);
            // Child widths are bound to the same per-app converter as legacy TaskButton.
            ExpandedWindows.Visibility = bundled && entry.Expansion == TaskBundleExpansion.Inline &&
                Host?.BundleExpanded(entry.Key) == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void GroupButton_Click(object sender, RoutedEventArgs e)
        {
            var entry = Entry;
            if (entry == null || !entry.IsBundle) return;
            if (entry.Expansion == TaskBundleExpansion.Inline)
            {
                bool expanded = Host?.BundleExpanded(entry.Key) != true;
                Host?.SetBundleExpanded(entry.Key, expanded);
                ExpandedWindows.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            var menu = new ContextMenu { PlacementTarget = GroupButton, StaysOpen = false };
            var edge = Host?.Host?.AppBarEdge;
            menu.Placement = edge == ManagedShell.AppBar.AppBarEdge.Right ? PlacementMode.Left :
                             edge == ManagedShell.AppBar.AppBarEdge.Left ? PlacementMode.Right :
                             edge == ManagedShell.AppBar.AppBarEdge.Bottom ? PlacementMode.Top : PlacementMode.Bottom;
            foreach (ApplicationWindow window in entry.Windows)
            {
                var item = new MenuItem
                {
                    Header = string.IsNullOrWhiteSpace(window.Title) ? entry.DisplayName : window.Title,
                    Tag = window
                };
                item.Click += (s, args) =>
                {
                    try
                    {
                        if (window.State == ApplicationWindow.WindowState.Active && window.CanMinimize) window.Minimize();
                        else window.BringToFront();
                    }
                    catch (InvalidOperationException) { /* Window exited after menu opened. */ }
                };
                menu.Items.Add(item);
            }
            menu.Opened += (s, args) => Host?.Host?.AddOpenMenu();
            menu.Closed += (s, args) => Host?.Host?.RemoveOpenMenu();
            menu.IsOpen = true;
        }
    }
}
