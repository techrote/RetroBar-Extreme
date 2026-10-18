using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the Show desktop button as an icon of the quick launch area itself, drawn and
    // placed like any program's icon (same cell, box, rows and columns), rather than a button beside
    // the icons.
    public partial class Toolbar
    {
        public static readonly DependencyProperty QuickExtraProperty = DependencyProperty.Register(
            nameof(QuickExtra), typeof(UIElement), typeof(Toolbar));

        // The cell the icons' panel lays out ahead of the shortcuts (null when Show desktop lives elsewhere).
        public UIElement QuickExtra
        {
            get => (UIElement)GetValue(QuickExtraProperty);
            private set => SetValue(QuickExtraProperty, value);
        }

        private ToolbarButton _desktopItem;
        private DesktopQuickItem _desktopData;

        public void SetDesktopItem(bool visible)
        {
            if (!visible)
            {
                QuickExtra = null;
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(FitIconColumns));
                return;
            }

            if (_desktopItem == null)
            {
                _desktopData = new DesktopQuickItem();
                _desktopItem = new ToolbarButton { DataContext = _desktopData };
                _desktopItem.PreviewMouseLeftButtonUp += (s, e) =>
                {
                    Mouse.Capture(null);
                    ShowDesktopButton.ToggleDesktopNow();
                    e.Handled = true;
                };
                _desktopItem.MouseEnter += (s, e) => ShowDesktopButton.PeekNow(1);
                _desktopItem.MouseLeave += (s, e) => ShowDesktopButton.PeekNow(0);
                _desktopItem.ContextMenu = BuildDesktopMenu();
            }

            _desktopData.Refresh();
            QuickExtra = _desktopItem;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(FitIconColumns));
        }

        private ContextMenu BuildDesktopMenu()
        {
            var menu = new ContextMenu();
            var show = new System.Windows.Controls.MenuItem { Header = TryFindResource("show_desktop") ?? "Show Desktop" };
            show.Click += (s, e) => ShowDesktopButton.ToggleDesktopNow();
            var peek = new System.Windows.Controls.MenuItem { Header = TryFindResource("peek_at_desktop") ?? "Peek at desktop", IsCheckable = true };
            peek.SetBinding(System.Windows.Controls.MenuItem.IsCheckedProperty, new System.Windows.Data.Binding(nameof(Settings.PeekAtDesktop))
            {
                Source = Settings.Instance, Mode = System.Windows.Data.BindingMode.TwoWay,
            });
            menu.Items.Add(show);
            menu.Items.Add(peek);
            return menu;
        }
    }

    // What the cell's icon button binds to: the same members a shortcut offers.
    public class DesktopQuickItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public DesktopQuickItem()
        {
            Settings.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Settings.ShowDesktopIcon) || e.PropertyName == nameof(Settings.TaskbarScale) || e.PropertyName == nameof(Settings.Theme))
                {
                    Application.Current?.Dispatcher.BeginInvoke(new Action(Refresh));
                }
            };
        }

        public string DisplayName { get; private set; } = "Show Desktop";
        public ImageSource SmallIcon { get; private set; }
        public ImageSource LargeIcon { get; private set; }

        public void Refresh()
        {
            Application app = Application.Current;
            DisplayName = app?.TryFindResource("show_desktop_tip") as string ?? "Show Desktop";
            SmallIcon = app?.TryFindResource("ShowDesktopIconImageSmall") as ImageSource;
            LargeIcon = app?.TryFindResource("ShowDesktopIconImageLarge") as ImageSource;

            // A PNG the user picked for the Show desktop button.
            if (CustomIcons.Load(Settings.Instance.ShowDesktopIcon, 16 * Math.Max(1.0, Settings.Instance.TaskbarScale)) is ImageSource custom)
            {
                SmallIcon = custom;
                LargeIcon = custom;
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SmallIcon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LargeIcon)));
        }
    }
}
