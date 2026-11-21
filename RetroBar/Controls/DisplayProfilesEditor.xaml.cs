using ManagedShell.AppBar;
using RetroBar.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RetroBar.Controls
{
    // Display policy editor: Save/Apply is explicit to avoid rebuilding AppBars on each keystroke.
    public partial class DisplayProfilesEditor : UserControl
    {
        private sealed class ScreenChoice
        {
            public AppBarScreen Screen { get; set; }
            public override string ToString() => Screen.DeviceName + (Screen.Primary ? " (primary)" : "");
        }

        private bool _loading;

        public DisplayProfilesEditor()
        {
            InitializeComponent();
            EdgePicker.ItemsSource = new[] { AppBarEdge.Left, AppBarEdge.Top, AppBarEdge.Right, AppBarEdge.Bottom };
            WindowSourcePicker.ItemsSource = new[] { DisplayWindowSource.AllMonitors, DisplayWindowSource.ThisMonitor };
        }

        private void Editor_Loaded(object sender, RoutedEventArgs e) => RefreshScreens();

        private DisplayTaskbarProfile DefaultFor(AppBarScreen screen) => new()
        {
            DeviceName = screen.DeviceName,
            Enabled = screen.Primary || Settings.Instance.ShowMultiMon,
            Edge = Settings.Instance.Edge,
            Rows = Settings.Instance.RowCount,
            Width = Settings.Instance.TaskbarWidth,
            AutoHide = Settings.Instance.AutoHide,
            ShowStart = screen.Primary || Settings.Instance.ShowStartButtonMultiMon,
            ShowQuickLaunch = Settings.Instance.ShowQuickLaunch,
            ShowTray = true,
            ShowClock = Settings.Instance.ShowClock,
            WindowSource = Settings.Instance.MultiMonMode == MultiMonOption.AllTaskbars ||
                (screen.Primary && Settings.Instance.MultiMonMode == MultiMonOption.SameAsWindowAndPrimary)
                ? DisplayWindowSource.AllMonitors : DisplayWindowSource.ThisMonitor
        };

        private void RefreshScreens()
        {
            string previous = (DisplayPicker.SelectedItem as ScreenChoice)?.Screen.DeviceName;
            _loading = true;
            var choices = AppBarScreen.FromAllScreens().Select(s => new ScreenChoice { Screen = s }).ToList();
            DisplayPicker.ItemsSource = choices;
            DisplayPicker.SelectedItem = choices.FirstOrDefault(s =>
                string.Equals(s.Screen.DeviceName, previous, StringComparison.OrdinalIgnoreCase)) ??
                choices.FirstOrDefault(s => s.Screen.Primary) ?? choices.FirstOrDefault();
            ProfilesActive.IsChecked = Settings.Instance.UseDisplayProfiles;
            _loading = false;
            ShowSelected();
        }

        private void ShowSelected()
        {
            var screen = (DisplayPicker.SelectedItem as ScreenChoice)?.Screen;
            ProfileFields.IsEnabled = screen != null;
            if (screen == null) return;
            var profile = DisplayTaskbarProfiles.Find(Settings.Instance.DisplayTaskbarProfiles, screen.DeviceName) ?? DefaultFor(screen);
            _loading = true;
            BarEnabled.IsChecked = profile.Enabled;
            EdgePicker.SelectedItem = profile.Edge;
            WindowSourcePicker.SelectedItem = profile.WindowSource;
            RowsText.Text = profile.Rows.ToString();
            WidthText.Text = profile.Width.ToString();
            AutoHide.IsChecked = profile.AutoHide;
            ShowStart.IsChecked = profile.ShowStart;
            ShowQuickLaunch.IsChecked = profile.ShowQuickLaunch;
            ShowTray.IsChecked = profile.ShowTray;
            ShowClock.IsChecked = profile.ShowClock;
            IncludeApps.Text = profile.IncludeApps ?? "";
            ExcludeApps.Text = profile.ExcludeApps ?? "";
            _loading = false;
        }

        private void EnsureProfilesForConnectedScreens()
        {
            var profiles = (Settings.Instance.DisplayTaskbarProfiles ?? new List<DisplayTaskbarProfile>())
                .Where(p => p != null).Select(p => p.Clone()).ToList();
            foreach (var screen in AppBarScreen.FromAllScreens())
            {
                if (DisplayTaskbarProfiles.Find(profiles, screen.DeviceName) == null)
                    profiles.Add(DefaultFor(screen));
            }
            Settings.Instance.DisplayTaskbarProfiles = profiles;
        }

        private void ProfilesActive_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (ProfilesActive.IsChecked == true)
                EnsureProfilesForConnectedScreens();
            Settings.Instance.UseDisplayProfiles = ProfilesActive.IsChecked == true;
            ShowSelected();
        }

        private void DisplayPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading) ShowSelected();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshScreens();

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            var screen = (DisplayPicker.SelectedItem as ScreenChoice)?.Screen;
            if (screen == null) return;
            var p = (DisplayTaskbarProfiles.Find(Settings.Instance.DisplayTaskbarProfiles, screen.DeviceName) ?? DefaultFor(screen)).Clone();
            p.Enabled = BarEnabled.IsChecked == true;
            if (EdgePicker.SelectedItem is AppBarEdge edge) p.Edge = edge;
            if (WindowSourcePicker.SelectedItem is DisplayWindowSource source) p.WindowSource = source;
            if (int.TryParse(RowsText.Text, out int rows)) p.Rows = Math.Clamp(rows, 1, Settings.Instance.RowLimit);
            if (int.TryParse(WidthText.Text, out int width)) p.Width = Math.Clamp(width, 1, Settings.Instance.TaskbarWidthLimit);
            p.AutoHide = AutoHide.IsChecked == true;
            p.ShowStart = ShowStart.IsChecked == true;
            p.ShowQuickLaunch = ShowQuickLaunch.IsChecked == true;
            p.ShowTray = ShowTray.IsChecked == true;
            p.ShowClock = ShowClock.IsChecked == true;
            p.IncludeApps = IncludeApps.Text.Trim();
            p.ExcludeApps = ExcludeApps.Text.Trim();
            Settings.Instance.SaveDisplayTaskbarProfile(p);
            ShowSelected();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            var screen = (DisplayPicker.SelectedItem as ScreenChoice)?.Screen;
            if (screen == null) return;
            Settings.Instance.SaveDisplayTaskbarProfile(DefaultFor(screen));
            ShowSelected();
        }
    }
}
