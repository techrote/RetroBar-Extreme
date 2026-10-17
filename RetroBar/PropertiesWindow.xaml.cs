using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ManagedShell.Common.Helpers;
using RetroBar.Utilities;
using System.Windows;
using ManagedShell.Common.Logging;
using Microsoft.Win32;
using ManagedShell.AppBar;
using System.Windows.Forms;
using ManagedShell.WindowsTray;
using System.Runtime.CompilerServices;
using System.IO;

namespace RetroBar
{
    /// <summary>
    /// Interaction logic for PropertiesWindow.xaml
    /// </summary>
    public partial class PropertiesWindow : Window, INotifyPropertyChanged
    {
        private static PropertiesWindow _instance;

        private readonly double _barSize;
        private readonly DictionaryManager _dictionaryManager;
        private readonly double _dpiScale;
        private readonly NotificationArea _notificationArea;
        private readonly AppBarScreen _screen;

        private FileSystemWatcher _themesWatcher;

        public event PropertyChangedEventHandler PropertyChanged;

        // Previews should always assume bottom edge
        public AppBarEdge AppBarEdge
        {
            get => AppBarEdge.Bottom;
        }

        // Previews should always assume horizontal orientation
        public Orientation Orientation
        {
            get => Orientation.Horizontal;
        }

        // Previews should always assume normal mode
        public AppBarMode AppBarMode
        {
            get => AppBarMode.Normal;
        }

        // Previews should reflect the locked setting
        public bool IsLocked
        {
            get => Settings.Instance.LockTaskbar;
        }

        // Previews should never be scaled
        public bool IsScaled
        {
            get => false;
        }

        // Previews should always assume 1 row
        public int Rows
        {
            get => 1;
        }

        private PropertiesWindow(NotificationArea notificationArea, DictionaryManager dictionaryManager, AppBarScreen screen, double dpiScale, double barSize)
        {
            _barSize = barSize;
            _dictionaryManager = dictionaryManager;
            _dpiScale = dpiScale;
            _notificationArea = notificationArea;
            _screen = screen;

            InitializeComponent();

            LoadPreviewHeight();
            LoadAutoStart();
            LoadLanguages();
            LoadStyleTab(); // UXUI-Extreme (UX-005)
            LoadOSSupport();
            LoadRows();
            LoadThemes();
            LoadWidth();
            LoadAppInfo();
            LoadClockActions();

            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.LockTaskbar))
            {
                OnPropertyChanged(nameof(IsLocked));
                LoadPreviewHeight();
            }
            else if (e.PropertyName == nameof(Settings.Theme))
            {
                LoadPreviewHeight();
            }
            else if (e.PropertyName == nameof(Settings.Language))
            {
                LoadAppInfo();
                LoadClockActions();
            }
        }

        public static PropertiesWindow Open(NotificationArea notificationArea, DictionaryManager dictionaryManager, AppBarScreen screen, double dpiScale, double barSize)
        {
            if (_instance == null)
            {
                _instance = new PropertiesWindow(notificationArea, dictionaryManager, screen, dpiScale, barSize);
                _instance.Show();
            }
            else
            {
                _instance.Activate();
            }

            return _instance;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void LoadPreviewHeight()
        {
            double size = System.Windows.Application.Current.FindResource("TaskbarHeight") as double? ?? 0;

            if (!IsLocked)
            {
                size += System.Windows.Application.Current.FindResource("TaskbarUnlockedSize") as double? ?? 0;
            }

            TaskbarAppearancePreviewControl.Height = size;
            NotificationAreaPreviewControl.Height = size;
        }

        private void LoadAutoStart()
        {
            try
            {
                RegistryKey rKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                List<string> rKeyValueNames = rKey?.GetValueNames().ToList();

                if (rKeyValueNames != null)
                {
                    if (rKeyValueNames.Contains("RetroBar"))
                    {
                        cbAutoStart.IsChecked = true;
                    }
                    else
                    {
                        cbAutoStart.IsChecked = false;
                    }
                }
            }
            catch (Exception e)
            {
                ShellLogger.Error($"PropertiesWindow: Unable to load autorun setting from registry: {e.Message}");
            }
        }

        private void LoadLanguages()
        {
            foreach (var language in _dictionaryManager.GetLanguages())
            {
                cboLanguageSelect.Items.Add(language);
            }
        }

        private void LoadOSSupport()
        {
            if (!EnvironmentHelper.IsWindows10OrBetter)
            {
                cbAllowBlurBehind.Visibility = Visibility.Collapsed;
                cbShowStartButtonMultiMon.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadRows()
        {
            for (int i = 1; i <= Settings.Instance.RowLimit; i++)
            {
                cboRowCount.Items.Add(i.ToString());
            }
        }

        private void LoadAppInfo()
        {
            gbRetroBar.Header = System.Windows.Forms.Application.ProductName;
            txtVersion.Text = string.Format((string)FindResource("version"), System.Windows.Forms.Application.ProductVersion);
        }

        private void LoadWidth()
        {
            sldTaskbarWidth.Maximum = Settings.Instance.TaskbarWidthLimit;
        }

        private void LoadThemes()
        {
            foreach (var theme in _dictionaryManager.GetThemes())
            {
                cboThemeSelect.Items.Add(theme);
            }
            try
            {
                string path = _dictionaryManager.GetThemeInstallDir();
                Directory.CreateDirectory(path);

                _themesWatcher = new FileSystemWatcher(_dictionaryManager.GetThemeInstallDir());
                _themesWatcher.Created += ThemesWatcher_Created;
                _themesWatcher.Deleted += ThemesWatcher_Deleted;
                _themesWatcher.Renamed += ThemesWatcher_Renamed;
                _themesWatcher.Filter = "*.xaml";
                _themesWatcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                ShellLogger.Warning($"Unable to watch custom themes directory: {e}");
            }
        }

        private void ThemesWatcher_Created(object sender, FileSystemEventArgs e)
        {
            string newTheme = Path.GetFileNameWithoutExtension(e.FullPath);
            Dispatcher.BeginInvoke(() => 
            {
                if (!cboThemeSelect.Items.Contains(newTheme))
                {
                    cboThemeSelect.Items.Add(newTheme);
                }
            });
        }

        private void ThemesWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            string removedTheme = Path.GetFileNameWithoutExtension(e.FullPath);
            Dispatcher.BeginInvoke(() =>
            {
                if (cboThemeSelect.Items.Contains(removedTheme))
                {
                    if (cboThemeSelect.SelectedItem is string selected && selected == removedTheme)
                    {
                        cboThemeSelect.SelectedIndex = 0;
                    }
                    cboThemeSelect.Items.Remove(removedTheme);
                }
            });
        }

        private void ThemesWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            string removedTheme = Path.GetFileNameWithoutExtension(e.OldFullPath);
            string newTheme = Path.GetFileNameWithoutExtension(e.FullPath);
            Dispatcher.BeginInvoke(() =>
            {
                if (cboThemeSelect.Items.Contains(removedTheme))
                {
                    if (cboThemeSelect.SelectedItem is string selected && selected == removedTheme)
                    {
                        cboThemeSelect.SelectedIndex = 0;
                    }
                    cboThemeSelect.Items.Remove(removedTheme);
                }
                if (!cboThemeSelect.Items.Contains(newTheme))
                {
                    cboThemeSelect.Items.Add(newTheme);
                }
            });
        }

        private void UpdateWindowPosition()
        {
            switch (Settings.Instance.Edge)
            {
                case AppBarEdge.Left:
                    Left = (_screen.Bounds.Left / _dpiScale) + _barSize + 10;
                    Top = (_screen.WorkingArea.Top / _dpiScale) + 10;
                    break;
                case AppBarEdge.Top:
                    Left = (_screen.WorkingArea.Left / _dpiScale) + 10;
                    Top = (_screen.Bounds.Top / _dpiScale) + _barSize + 10;
                    break;
                case AppBarEdge.Right:
                    Left = (_screen.Bounds.Right / _dpiScale) - _barSize - Width - 10;
                    Top = (_screen.WorkingArea.Top / _dpiScale) + 10;
                    break;
                case AppBarEdge.Bottom:
                    Left = (_screen.WorkingArea.Left / _dpiScale) + 10;
                    Top = (_screen.Bounds.Bottom / _dpiScale) - _barSize - Height - 10;
                    break;
            }
        }

        private void OK_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetQuickLaunchLocation_OnClick(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.Description = (string)FindResource("quick_launch_folder");
#if NETCOREAPP3_0_OR_GREATER
            fbd.UseDescriptionForTitle = true;
#endif
            fbd.ShowNewFolderButton = false;
            fbd.SelectedPath = Settings.Instance.QuickLaunchPath;

            if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.Instance.QuickLaunchPath = fbd.SelectedPath;
            }
        }

        private void PropertiesWindow_OnClosing(object sender, CancelEventArgs e)
        {
            _instance = null;
            if (_themesWatcher != null)
            {
                _themesWatcher.Created -= ThemesWatcher_Created;
                _themesWatcher.Deleted -= ThemesWatcher_Deleted;
                _themesWatcher.Renamed -= ThemesWatcher_Renamed;
            }
            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
        }

        private void PropertiesWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = 10;
            Top = (ScreenHelper.PrimaryMonitorDeviceSize.Height / DpiHelper.DpiScale) - Height - 40;
            UpdateWindowPosition();

            // UXUI-Extreme (UX-009): the Colours tab grows the window to fit; cap it at the
            // screen's working area (room left for the title bar, tabs and OK button) and keep
            // the window on screen as it resizes.
            GetFreeArea(out double freeTop, out double freeBottom);
            ColoursScroll.MaxHeight = Math.Max(300, freeBottom - freeTop - 140);
            SizeChanged += (s, args) => KeepInWorkingArea();
            KeepInWorkingArea();
        }

        // UXUI-Extreme (UX-009): screen space not covered by this taskbar. The screen's own
        // working area doesn't always exclude RetroBar's bar, so take it off by edge, as
        // UpdateWindowPosition does.
        private void GetFreeArea(out double top, out double bottom)
        {
            top = (_screen.WorkingArea.Top / _dpiScale) + 10;
            bottom = (_screen.WorkingArea.Bottom / _dpiScale) - 10;

            if (Settings.Instance.Edge == AppBarEdge.Bottom)
            {
                bottom = Math.Min(bottom, (_screen.Bounds.Bottom / _dpiScale) - _barSize - 10);
            }
            else if (Settings.Instance.Edge == AppBarEdge.Top)
            {
                top = Math.Max(top, (_screen.Bounds.Top / _dpiScale) + _barSize + 10);
            }
        }

        private void KeepInWorkingArea()
        {
            GetFreeArea(out double top, out double bottom);

            if (Top + ActualHeight > bottom)
            {
                Top = Math.Max(top, bottom - ActualHeight);
            }
        }

        private void PropertiesWindow_OnContentRendered(object sender, EventArgs e)
        {
            UpdateWindowPosition();
        }

        private void CbAutoStart_OnChecked(object sender, RoutedEventArgs e)
        {
            try
            {
                RegistryKey rKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                var chkBox = (System.Windows.Controls.CheckBox)sender;

                if (chkBox.IsChecked.Equals(false))
                {
                    rKey?.DeleteValue("RetroBar");
                }
                else
                {
                    // Registry Run values are command lines; quote the executable path to handle spaces in usernames/paths.
                    rKey?.SetValue("RetroBar", $"\"{ExePath.GetExecutablePath()}\"");
                }
            }
            catch (Exception exception)
            {
                ShellLogger.Error($"PropertiesWindow: Unable to update registry autorun setting: {exception.Message}");
            }
        }

        private void LoadClockActions()
        {
            if (EnvironmentHelper.IsWindows10OrBetter)
            {
                return;
            }

            // Remove options unsupported prior to Windows 10.
            var availableClockActions = (FindResource("clock_click_action_values") as Array)?.Cast<object>().ToList();
            if (availableClockActions == null)
            {
                return;
            }

            availableClockActions.RemoveAt((int)ClockClickOption.OpenNotificationCenter);
            availableClockActions.RemoveAt((int)ClockClickOption.OpenModernCalendar);
            cboClockAction.ItemsSource = availableClockActions;
        }

        private void CboEdgeSelect_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboEdgeSelect.SelectedItem == null)
            {
                cboEdgeSelect.SelectedValue = cboEdgeSelect.Items[(int)Settings.Instance.Edge];
            }
        }

        private void CboMultiMonMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboMultiMonMode.SelectedItem == null)
            {
                cboMultiMonMode.SelectedValue = cboMultiMonMode.Items[(int)Settings.Instance.MultiMonMode];
            }
        }

        // UXUI-Extreme (UX-008): keep a selection when the language (item list) changes
        private void CboClockLines_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboClockLines.SelectedItem == null)
            {
                cboClockLines.SelectedValue = cboClockLines.Items[(int)Settings.Instance.ClockLines];
            }
        }

        // UXUI-Extreme (UX-008): keep a selection when the language (item list) changes
        private void CboClockTimeAlignment_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboClockTimeAlignment.SelectedItem == null)
            {
                cboClockTimeAlignment.SelectedValue = cboClockTimeAlignment.Items[(int)Settings.Instance.ClockTimeAlignment];
            }
        }

        // UXUI-Extreme (UX-006): keep a selection when the language (item list) changes
        private void CboScalingMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboScalingMode.SelectedItem == null)
            {
                cboScalingMode.SelectedValue = cboScalingMode.Items[(int)Settings.Instance.ScalingMode];
            }
        }

        // UXUI-Extreme (UX-005): Style tab — taskbar font settings
        private bool _loadingStyleTab = true; // true until LoadStyleTab finishes, so control events during InitializeComponent never overwrite settings (UX-008)

        private void LoadStyleTab()
        {
            _loadingStyleTab = true;

            // Font list: "Theme default" first, then the built-in fonts (UX-017), then installed fonts.
            Controls.FontChoices.Fill(cboFontFamily, FindResource("uxui_theme_default"), Settings.Instance.FontOverrideFamily);

            int weightIndex = Settings.Instance.FontOverrideWeight / 100;
            cboFontWeight.SelectedIndex = weightIndex >= 0 && weightIndex <= 9 ? weightIndex : 0;

            UpdateFontSizeText();
            SldRowHeightAdjust_ValueChanged(null, null); // UXUI-Extreme (UX-007)
            sldLineSpacing.Value = Settings.Instance.FontOverrideLineSpacing > 0 ? Settings.Instance.FontOverrideLineSpacing : LineSpacingAutoPosition; // UX-008
            UpdateLineSpacingText();
            _loadingStyleTab = false;
        }

        private void CboFontFamily_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string family = Controls.FontChoices.Selected(cboFontFamily);
            if (_loadingStyleTab || family == null)
            {
                return;
            }

            Settings.Instance.FontOverrideFamily = family;
        }

        private void CboFontWeight_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboFontWeight.SelectedIndex < 0)
            {
                // Item list was reloaded (language change); restore the selection.
                int weightIndex = Settings.Instance.FontOverrideWeight / 100;
                cboFontWeight.SelectedIndex = weightIndex >= 0 && weightIndex <= 9 ? weightIndex : 0;
                return;
            }

            if (_loadingStyleTab)
            {
                return;
            }

            Settings.Instance.FontOverrideWeight = cboFontWeight.SelectedIndex * 100;
        }

        private void SldFontSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateFontSizeText();
        }

        private void UpdateFontSizeText()
        {
            if (txtFontSize == null)
            {
                return;
            }

            // Show the size at 100% and what it actually becomes on screen at the current scale.
            double size = Settings.Instance.FontOverrideSize;
            if (size > 0)
            {
                double onScreen = TaskbarTextStyler.OnScreenFontSize(size);
                txtFontSize.Text = Settings.Instance.TaskbarScale > 1.0 ? $"{size:0} → {onScreen:0.#} px" : $"{size:0} px";
            }
            else
            {
                txtFontSize.Text = FindResource("uxui_theme_default") as string;
            }
        }

        // UXUI-Extreme (UX-008): slider's leftmost position means "Auto" (the font's own spacing)
        private const double LineSpacingAutoPosition = 0.55;

        private void SldLineSpacing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loadingStyleTab || sldLineSpacing == null)
            {
                return;
            }

            double value = sldLineSpacing.Value;
            Settings.Instance.FontOverrideLineSpacing = value < LineSpacingAutoPosition + 0.025 ? 0 : Math.Round(value, 2);
            UpdateLineSpacingText();
        }

        private void UpdateLineSpacingText()
        {
            if (txtLineSpacing == null)
            {
                return;
            }

            double spacing = Settings.Instance.FontOverrideLineSpacing;
            txtLineSpacing.Text = spacing > 0 ? $"{spacing:0.00} × size" : "Auto (font)";
        }

        // UXUI-Extreme (UX-007)
        private void SldRowHeightAdjust_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtRowHeightAdjust == null)
            {
                return;
            }

            double adjust = Settings.Instance.RowHeightAdjust;
            txtRowHeightAdjust.Text = adjust == 0 ? "0 px (auto)" : $"{adjust:+0;-0} px";
        }

        private void ResetFont_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Instance.FontOverrideFamily = "";
            Settings.Instance.FontOverrideWeight = 0;
            Settings.Instance.FontOverrideSize = 0;
            Settings.Instance.FontOverrideItalic = false;
            Settings.Instance.FontOverrideLineSpacing = 0; // UX-008
            LoadStyleTab();
        }

        // UXUI-Extreme (UX-002): keep a selection when the language (item list) changes
        private void CboPixelTextMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboPixelTextMode.SelectedItem == null)
            {
                cboPixelTextMode.SelectedValue = cboPixelTextMode.Items[(int)Settings.Instance.PixelTextMode];
            }
        }

        private void CboInvertIconsMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboInvertIconsMode.SelectedItem == null)
            {
                cboInvertIconsMode.SelectedValue = cboInvertIconsMode.Items[(int)Settings.Instance.InvertIconsMode];
            }
        }

        private void CboMiddleMouseAction_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboMiddleMouseAction.SelectedItem == null)
            {
                cboMiddleMouseAction.SelectedValue = cboMiddleMouseAction.Items[(int)Settings.Instance.TaskMiddleClickAction];
            }
        }

        private void CboClockAction_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboClockAction.SelectedItem == null)
            {
                cboClockAction.SelectedValue = cboClockAction.Items[(int)Settings.Instance.ClockClickAction];
            }
        }

        private void CboTaskWheelAction_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboTaskWheelAction.SelectedItem == null)
            {
                cboTaskWheelAction.SelectedValue = cboTaskWheelAction.Items[(int)Settings.Instance.TaskWheelAction];
            }
        }

        private void CboRowCount_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboRowCount.SelectedItem == null)
            {
                cboRowCount.SelectedValue = cboRowCount.Items[Settings.Instance.RowCount - 1];
            }
        }

        private void CboWinNumHotkeysAction_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this != _instance)
            {
                // Don't pop a message box if we're closed but haven't been GC'd yet
                return;
            }
            if (cboWinNumHotkeysAction.SelectedItem == null)
            {
                cboWinNumHotkeysAction.SelectedValue = cboWinNumHotkeysAction.Items[(int)Settings.Instance.WinNumHotkeysAction];
            }
            else if (e.RemovedItems.Count > 0 && e.RemovedItems[0] == cboWinNumHotkeysAction.Items[0])
            {
                System.Windows.MessageBox.Show((string)System.Windows.Application.Current.FindResource("hotkey_warning_text"), (string)System.Windows.Application.Current.FindResource("hotkey_warning_title"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CustomizeNotifications_OnClick(object sender, RoutedEventArgs e)
        {
            OpenCustomizeNotifications();
        }

        public void OpenCustomizeNotifications()
        {
            NotificationPropertiesWindow.Open(_notificationArea, new Point(Left, Top));
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            ShellHelper.ExecuteProcess(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        private void OpenCustomThemesFolder_OnClick(object sender, RoutedEventArgs e)
        {
            string path = _dictionaryManager.GetThemeInstallDir();
            Directory.CreateDirectory(path);
            ShellHelper.StartProcess(path);
        }

        private void ClockFormatHelpButton_Click(object sender, RoutedEventArgs e)
        {
            ShellHelper.ExecuteProcess("https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings#table");
        }
    }
}