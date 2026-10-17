using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ManagedShell.WindowsTray;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the Icons tab. Gathers every picture replacement in one place (the Start
    // button's picture and icon, quick launch items, notification area icons, and the
    // notification area toggle). Each writes straight to Settings; the taskbar picks it up live.
    public partial class IconsEditor : UserControl
    {
        // An entry in the quick launch / notification area pickers.
        private sealed class Target
        {
            public string Key { get; set; }
            public string Title { get; set; }
            public string FilePath { get; set; } // quick launch: the shortcut's full path
            public string Display { get; set; }
        }

        private bool _loading;

        // UXUI-Extreme: which groups this copy of the editor shows. "All" is everything; the Start/Quick tab
        // and the Layout / Icons tabs each show their own part, from the same settings.
        public static readonly DependencyProperty SectionsProperty = DependencyProperty.Register(
            nameof(Sections), typeof(string), typeof(IconsEditor), new PropertyMetadata("All", (d, e) => ((IconsEditor)d).ApplySections()));

        public string Sections
        {
            get => (string)GetValue(SectionsProperty);
            set => SetValue(SectionsProperty, value);
        }

        private static Visibility Show(bool show) => show ? Visibility.Visible : Visibility.Collapsed;

        private void ApplySections()
        {
            bool startQuick = Sections == "StartQuick", main = Sections == "Main", all = !startQuick && !main;
            grpStartPics.Visibility = Show(all || startQuick);
            grpDesktopIcon.Visibility = Show(all || startQuick);
            grpQuickIcons.Visibility = Show(all || startQuick);
            grpTray.Visibility = Show(all || main);
            grpToggleIcon.Visibility = Show(all || main);
        }

        // Set by the Properties window: where the notification area's icons are listed from.
        public NotificationArea NotificationArea { get; set; }

        public IconsEditor()
        {
            InitializeComponent();
        }

        private string Text(string key) => TryFindResource(key) as string ?? Application.Current.TryFindResource(key) as string ?? key;

        private void IconsEditor_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                Load();
            }
        }

        private void Load()
        {
            _loading = true;
            var s = Settings.Instance;
            txtStartImage.Text = Shown(s.StartButtonImage);
            txtStartIcon.Text = Shown(s.StartButtonIcon);
            txtDesktopIcon.Text = Shown(s.ShowDesktopIcon);
            cboStartIconAlign.SelectedIndex = (int)s.StartButtonIconAlign;
            cboDesktopIconAlign.SelectedIndex = (int)s.ShowDesktopIconAlign;
            txtToggleIcon.Text = Shown(s.TrayToggleIcon);
            LoadQuickLaunch();
            LoadTray();
            _loading = false;
            UpdateButtons();
        }

        private string Shown(string icon) => string.IsNullOrEmpty(icon) ? Text("uxui_icon_theme_current") : icon;

        // ------------------------------------------------------------ Start button
        // Two load buttons, each opening its own folder; the button used says what the picture holds.
        private void StartImage1_OnClick(object sender, RoutedEventArgs e) => ChooseStartImage(StartImageStatesOption.One, @"StartButtons\1state");

        private void StartImage3_OnClick(object sender, RoutedEventArgs e) => ChooseStartImage(StartImageStatesOption.Three, @"StartButtons\3state");

        private void ChooseStartImage(StartImageStatesOption states, string folder)
        {
            string icon = IconPicker.Choose(Window.GetWindow(this), folder);
            if (icon != null)
            {
                Settings.Instance.StartButtonImageStates = states;
                Settings.Instance.StartButtonImage = icon;
                txtStartImage.Text = Shown(icon);
            }
        }

        private void StartImageReset_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Instance.StartButtonImage = "";
            txtStartImage.Text = Shown("");
        }

        private void StartIconAlign_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading && cboStartIconAlign.SelectedIndex >= 0)
            {
                Settings.Instance.StartButtonIconAlign = (TaskButtonAlignOption)cboStartIconAlign.SelectedIndex;
            }
        }

        private void DesktopIconAlign_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading && cboDesktopIconAlign.SelectedIndex >= 0)
            {
                Settings.Instance.ShowDesktopIconAlign = (TaskButtonAlignOption)cboDesktopIconAlign.SelectedIndex;
            }
        }

        private void DesktopIconChoose_OnClick(object sender, RoutedEventArgs e)
        {
            string icon = IconPicker.Choose(Window.GetWindow(this), @"showdesktop\16px");
            if (icon != null)
            {
                Settings.Instance.ShowDesktopIcon = icon;
                txtDesktopIcon.Text = Shown(icon);
            }
        }

        private void DesktopIconReset_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Instance.ShowDesktopIcon = "";
            txtDesktopIcon.Text = Shown("");
        }

        private void StartIconChoose_OnClick(object sender, RoutedEventArgs e)
        {
            string icon = IconPicker.Choose(Window.GetWindow(this), @"start\16px");
            if (icon != null)
            {
                Settings.Instance.StartButtonIcon = icon;
                txtStartIcon.Text = Shown(icon);
            }
        }

        private void StartIconReset_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Instance.StartButtonIcon = "";
            txtStartIcon.Text = Shown("");
        }

        // ------------------------------------------------------------ Notification area toggle
        private void ToggleIconChoose_OnClick(object sender, RoutedEventArgs e)
        {
            string icon = IconPicker.Choose(Window.GetWindow(this), @"toggle\100%");
            if (icon != null)
            {
                Settings.Instance.TrayToggleIcon = icon;
                txtToggleIcon.Text = Shown(icon);
            }
        }

        private void ToggleIconReset_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Instance.TrayToggleIcon = "";
            txtToggleIcon.Text = Shown("");
        }

        // ------------------------------------------------------------ Quick launch
        private Target SelectedQuickLaunch => cboQuickLaunch.SelectedItem as Target;

        private void LoadQuickLaunch()
        {
            string selected = SelectedQuickLaunch?.Key;
            var items = new List<Target>();
            try
            {
                string folder = Environment.ExpandEnvironmentVariables(Settings.Instance.QuickLaunchPath ?? "");
                if (Directory.Exists(folder))
                {
                    items = Directory.EnumerateFiles(folder)
                        .Where(f => (File.GetAttributes(f) & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                        .OrderBy(f => Path.GetFileName(f), StringComparer.CurrentCultureIgnoreCase)
                        .Select(f => new Target
                        {
                            Key = CustomIcons.QuickLaunchKey(f),
                            FilePath = f,
                            Title = Path.GetFileNameWithoutExtension(f),
                        })
                        .ToList();
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // An unreadable folder just means an empty list.
            }

            foreach (Target item in items)
            {
                item.Display = item.Title + (string.IsNullOrEmpty(CustomIcons.QuickLaunchIcon(item.FilePath)) ? "" : " " + Text("uxui_icons_custom"));
            }

            cboQuickLaunch.ItemsSource = items;
            cboQuickLaunch.SelectedItem = items.FirstOrDefault(i => i.Key == selected) ?? items.FirstOrDefault();
            txtQuickLaunchHint.Text = items.Count == 0 ? Text("uxui_icons_ql_none") : Text("uxui_icons_ql_hint");
        }

        private void QuickLaunch_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

        private void QuickLaunchChoose_OnClick(object sender, RoutedEventArgs e)
        {
            Target item = SelectedQuickLaunch;
            string icon = item == null ? null : IconPicker.Choose(Window.GetWindow(this), @"quicklaunch\16px");
            if (icon != null)
            {
                CustomIcons.SetQuickLaunchIcon(item.FilePath, icon);
                LoadQuickLaunch();
                UpdateButtons();
            }
        }

        private void QuickLaunchReset_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedQuickLaunch != null)
            {
                CustomIcons.SetQuickLaunchIcon(SelectedQuickLaunch.FilePath, null);
                LoadQuickLaunch();
                UpdateButtons();
            }
        }

        // ------------------------------------------------------------ Notification area icons
        private Target SelectedTray => cboTray.SelectedItem as Target;

        private void LoadTray()
        {
            string selected = SelectedTray?.Key;
            var items = new Dictionary<string, Target>();

            if (NotificationArea?.TrayIcons != null)
            {
                foreach (ManagedShell.WindowsTray.NotifyIcon icon in NotificationArea.TrayIcons.Cast<ManagedShell.WindowsTray.NotifyIcon>().ToList())
                {
                    string key = icon.Identifier;
                    if (!string.IsNullOrEmpty(key) && !items.ContainsKey(key))
                    {
                        string title = string.IsNullOrWhiteSpace(icon.Title)
                            ? (string.IsNullOrEmpty(icon.Path) ? key : Path.GetFileNameWithoutExtension(icon.Path))
                            : icon.Title.Trim();
                        items[key] = new Target { Key = key, Title = title };
                    }
                }
            }

            // Replacements for icons that aren't running right now stay listed, so they can be changed or cleared.
            foreach (TrayIconReplacementSetting saved in Settings.Instance.TrayIconReplacements ?? new List<TrayIconReplacementSetting>())
            {
                if (!items.ContainsKey(saved.Identifier))
                {
                    items[saved.Identifier] = new Target { Key = saved.Identifier, Title = string.IsNullOrEmpty(saved.Title) ? saved.Identifier : saved.Title };
                }
            }

            var list = items.Values.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            foreach (Target item in list)
            {
                item.Display = item.Title + (string.IsNullOrEmpty(CustomIcons.TrayIcon(item.Key)) ? "" : " " + Text("uxui_icons_custom"));
            }

            cboTray.ItemsSource = list;
            cboTray.SelectedItem = list.FirstOrDefault(i => i.Key == selected) ?? list.FirstOrDefault();
            txtTrayHint.Text = list.Count == 0 ? Text("uxui_icons_tray_none") : Text("uxui_icons_tray_hint");
        }

        private void Tray_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

        private void TrayChoose_OnClick(object sender, RoutedEventArgs e)
        {
            Target item = SelectedTray;
            string icon = item == null ? null : IconPicker.Choose(Window.GetWindow(this), @"tray\16px");
            if (icon != null)
            {
                CustomIcons.SetTrayIcon(item.Key, item.Title, icon);
                LoadTray();
                UpdateButtons();
            }
        }

        private void TrayReset_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedTray != null)
            {
                CustomIcons.SetTrayIcon(SelectedTray.Key, null, null);
                LoadTray();
                UpdateButtons();
            }
        }

        private void UpdateButtons()
        {
            btnQuickLaunchChoose.IsEnabled = SelectedQuickLaunch != null;
            btnQuickLaunchReset.IsEnabled = SelectedQuickLaunch != null && !string.IsNullOrEmpty(CustomIcons.QuickLaunchIcon(SelectedQuickLaunch.FilePath));
            btnTrayChoose.IsEnabled = SelectedTray != null;
            btnTrayReset.IsEnabled = SelectedTray != null && !string.IsNullOrEmpty(CustomIcons.TrayIcon(SelectedTray.Key));
        }
    }
}
