using ManagedShell.WindowsTasks;
using ManagedShell.Interop;
using System.Collections.Generic;
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
        private sealed class MemberRectReceiver
        {
            internal readonly ApplicationWindow Member;
            internal readonly TaskList RegisteredHost;
            private readonly TaskBundleEntryView _view;

            internal MemberRectReceiver(TaskBundleEntryView view, ApplicationWindow member, TaskList host)
            {
                _view = view;
                Member = member;
                RegisteredHost = host;
            }

            internal void OnGetButtonRect(ref NativeMethods.ShortRect rect) =>
                _view.Member_GetButtonRect(Member, ref rect);
        }

        private readonly List<ApplicationWindow> _rectMembers = new();
        private readonly List<MemberRectReceiver> _rectReceivers = new();
        private bool _rectLoaded;

        private void DetachRectMembers()
        {
            foreach (var receiver in _rectReceivers)
            {
                receiver.Member.GetButtonRect -= receiver.OnGetButtonRect;
                receiver.RegisteredHost?.UnregisterRectangleTarget(receiver.Member, GroupButton, header: true);
            }
            _rectReceivers.Clear();
            _rectMembers.Clear();
        }

        private void RefreshRectMembers()
        {
            DetachRectMembers();
            if (!_rectLoaded || Entry?.IsBundle != true) return;
            foreach (var member in Entry.Windows)
            {
                if (member == null || _rectMembers.Exists(w => ReferenceEquals(w, member))) continue;
                var receiver = new MemberRectReceiver(this, member, Host);
                receiver.RegisteredHost?.RegisterRectangleTarget(member, GroupButton, header: true);
                member.GetButtonRect += receiver.OnGetButtonRect;
                _rectReceivers.Add(receiver);
                _rectMembers.Add(member);
            }
        }

        private void Member_GetButtonRect(ApplicationWindow member, ref NativeMethods.ShortRect rect)
        {
            // The event has no sender argument: this receiver binds the exact
            // requesting window rather than authorizing with the bundle's
            // first member, which may belong to a different monitor.
            if (!_rectLoaded || Entry?.IsBundle != true ||
                !_rectMembers.Exists(w => ReferenceEquals(w, member)) ||
                !Entry.Windows.Exists(w => ReferenceEquals(w, member)) ||
                Host?.CanOwnButtonRect(member, GroupButton, isBundleHeader: true) != true)
                return;

            Point first = GroupButton.PointToScreen(new Point(0, 0));
            Point last = GroupButton.PointToScreen(new Point(GroupButton.ActualWidth, GroupButton.ActualHeight));
            rect.Top = (short)first.Y;
            rect.Left = (short)first.X;
            rect.Bottom = (short)last.Y;
            rect.Right = (short)last.X;
        }

        public TaskBundleEntryView()
        {
            InitializeComponent();
        }

        private void Entry_Loaded(object sender, RoutedEventArgs e)
        {
            _rectLoaded = true;
            RefreshEntry();
        }
        private void Entry_Unloaded(object sender, RoutedEventArgs e)
        {
            _rectLoaded = false;
            DetachRectMembers();
        }
        private void Entry_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => RefreshEntry();

        private void RefreshEntry()
        {
            if (SingleButton == null || GroupButton == null || ExpandedWindows == null) return;
            var entry = Entry;
            bool bundled = entry?.IsBundle == true;
            GroupButton.Visibility = bundled ? Visibility.Visible : Visibility.Collapsed;
            SingleButton.Visibility = bundled ? Visibility.Collapsed : Visibility.Visible;
            // Both orientation and width now bind reactively; otherwise a taskbar resized
            // after creation can retain a stale zero-width bundle entry.
            ExpandedWindows.Visibility = bundled && entry.Expansion == TaskBundleExpansion.Inline &&
                Host?.BundleExpanded(entry.Key) == true ? Visibility.Visible : Visibility.Collapsed;
            RefreshRectMembers();
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
