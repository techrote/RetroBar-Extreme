using ManagedShell;
using ManagedShell.AppBar;
using ManagedShell.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace RetroBar.Utilities
{
    public class WindowManager : IDisposable
    {
        private static object reopenLock = new object();

        private bool _isSettingDisplays;
        private int _pendingDisplayEvents;
        private List<AppBarScreen> _screenState = new List<AppBarScreen>();
        private List<Taskbar> _taskbars = new List<Taskbar>();
        private string _shellHostDeviceName = "";

        // One selected taskbar handles shell/tray integration even when the primary monitor has no bar.
        public bool IsShellHost(AppBarScreen screen) => screen != null &&
            string.Equals(_shellHostDeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase);

        private readonly DictionaryManager _dictionaryManager;
        private readonly ExplorerMonitor _explorerMonitor;
        private readonly StartMenuMonitor _startMenuMonitor;
        private readonly ShellManager _shellManager;
        private readonly Updater _updater;
        private HotkeyManager _hotkeyManager;

        public WindowManager(DictionaryManager dictionaryManager, ExplorerMonitor explorerMonitor, ShellManager shellManager, StartMenuMonitor startMenuMonitor, Updater updater, HotkeyManager hotkeyManager)
        {
            _dictionaryManager = dictionaryManager;
            _explorerMonitor = explorerMonitor;
            _shellManager = shellManager;
            _startMenuMonitor = startMenuMonitor;
            _updater = updater;
            _hotkeyManager = hotkeyManager;

            _shellManager.ExplorerHelper.HideExplorerTaskbar = true;

            openTaskbars();

            _explorerMonitor.ExplorerMonitorStart(this, _shellManager);

            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            bool profilesToggled = e.PropertyName == nameof(Settings.UseDisplayProfiles);
            bool activeProfileChanged = e.PropertyName == nameof(Settings.DisplayTaskbarProfiles) &&
                Settings.Instance.UseDisplayProfiles;
            bool legacyMonitorsChanged = e.PropertyName == nameof(Settings.ShowMultiMon) &&
                !Settings.Instance.UseDisplayProfiles;
            if (profilesToggled || activeProfileChanged || legacyMonitorsChanged)
            {
                _screenState = AppBarScreen.FromAllScreens();
                // Only the legacy ShowMultiMon switch is irrelevant on a single-screen desktop.
                if (legacyMonitorsChanged && _screenState.Count < 2) return;
                // Resizing, recolouring, filtering and moving a profile must not close
                // the taskbar while its own mouse drag hook is active.
                if (activeProfileChanged && TryApplyProfilesInPlace()) return;
                ReopenTaskbars();
            }
        }

        private bool TryApplyProfilesInPlace()
        {
            var enabled = _screenState.Select(screen => new
            {
                Screen = screen,
                Profile = DisplayTaskbarProfiles.Find(Settings.Instance.DisplayTaskbarProfiles, screen.DeviceName)
            }).Where(item => item.Profile?.Enabled == true).ToList();

            // A changed screen set, fallback bar, or shell owner requires a full rebuild.
            if (enabled.Count == 0 || enabled.Count != _taskbars.Count)
                return false;
            var host = enabled.FirstOrDefault(item => item.Screen.Primary && item.Profile.ShowTray)
                       ?? enabled.FirstOrDefault(item => item.Profile.ShowTray)
                       ?? enabled.FirstOrDefault(item => item.Screen.Primary)
                       ?? enabled[0];
            if (!string.Equals(host.Screen.DeviceName, _shellHostDeviceName, StringComparison.OrdinalIgnoreCase))
                return false;
            foreach (var bar in _taskbars)
            {
                var next = enabled.FirstOrDefault(item =>
                    string.Equals(item.Screen.DeviceName, bar.Screen.DeviceName, StringComparison.OrdinalIgnoreCase));
                if (bar.DisplayProfile == null || next == null ||
                    next.Profile.AutoHide != bar.DisplayProfile.AutoHide)
                    return false;
            }

            foreach (var bar in _taskbars)
            {
                var next = enabled.First(item =>
                    string.Equals(item.Screen.DeviceName, bar.Screen.DeviceName, StringComparison.OrdinalIgnoreCase));
                bar.ApplyDisplayProfile(next.Profile);
            }
            return true;
        }

        public void ReopenTaskbars()
        {
            lock (reopenLock)
            {
                closeTaskbars();
                openTaskbars();
            }
        }

        public void NotifyWorkAreaChange()
        {
            ShellLogger.Debug($"WindowManager: Work area change notification received");
            handleDisplayChange();
        }

        public void NotifyDisplayChange(ScreenSetupReason reason)
        {
            ShellLogger.Debug($"WindowManager: Display change notification received ({reason})");
            handleDisplayChange();
        }

        private void handleDisplayChange()
        {
            _pendingDisplayEvents++;

            if (_isSettingDisplays)
            {
                return;
            }

            _isSettingDisplays = true;

            while (_pendingDisplayEvents > 0)
            {
                // Skip re-opening taskbars if the screens haven't changed
                if (!haveDisplaysChanged())
                {
                    _pendingDisplayEvents--;
                    continue;
                }

                ReopenTaskbars();

                _pendingDisplayEvents--;
            }

            _isSettingDisplays = false;
            ShellLogger.Debug($"WindowManager: Finished processing display events");
        }

        public bool IsValidHMonitor(IntPtr hMonitor)
        {
            foreach(var screen in _screenState)
            {
                if (screen.HMonitor == hMonitor)
                {
                    return true;
                }
            }

            return false;
        }

        private void closeTaskbars()
        {
            ShellLogger.Debug($"WindowManager: Closing all taskbars");

            foreach (var taskbar in _taskbars)
            {
                taskbar.AllowClose = true;
                taskbar.Close();
            }

            _taskbars.Clear();
        }

        private void openTaskbars()
        {
            _screenState = AppBarScreen.FromAllScreens();

            ShellLogger.Debug($"WindowManager: Opening taskbars");

            if (Settings.Instance.UseDisplayProfiles)
            {
                var enabled = _screenState.Select(screen => new {
                    Screen = screen,
                    Profile = DisplayTaskbarProfiles.Find(Settings.Instance.DisplayTaskbarProfiles, screen.DeviceName)
                }).Where(item => item.Profile?.Enabled == true).ToList();

                if (enabled.Count == 0)
                {
                    // Recovery: never strand the owner if saved displays disappear or every bar is disabled.
                    AppBarScreen fallback = AppBarScreen.FromPrimaryScreen();
                    _shellHostDeviceName = fallback.DeviceName;
                    ShellLogger.Debug("WindowManager: No enabled connected profiles; opening recovery taskbar");
                    openTaskbar(fallback);
                    return;
                }

                var host = enabled.FirstOrDefault(item => item.Screen.Primary && item.Profile.ShowTray)
                           ?? enabled.FirstOrDefault(item => item.Profile.ShowTray)
                           ?? enabled.FirstOrDefault(item => item.Screen.Primary)
                           ?? enabled[0];
                _shellHostDeviceName = host.Screen.DeviceName;
                foreach (var item in enabled) openTaskbar(item.Screen, item.Profile);
                return;
            }

            _shellHostDeviceName = AppBarScreen.FromPrimaryScreen().DeviceName;
            if (Settings.Instance.ShowMultiMon)
            {
                foreach (var screen in _screenState) openTaskbar(screen);
            }
            else
            {
                openTaskbar(AppBarScreen.FromPrimaryScreen());
            }
        }

        private void openTaskbar(AppBarScreen screen, DisplayTaskbarProfile profile = null)
        {
            ShellLogger.Debug($"WindowManager: Opening taskbar on screen {screen.DeviceName}");
            var edge = profile?.Edge ?? Settings.Instance.Edge;
            bool hide = profile?.AutoHide ?? Settings.Instance.AutoHide;
            Taskbar taskbar = new Taskbar(this, _dictionaryManager, _shellManager, _startMenuMonitor,
                _updater, _hotkeyManager, screen, edge, hide ? AppBarMode.AutoHide : AppBarMode.Normal, profile);
            taskbar.Show();
            _taskbars.Add(taskbar);
        }

        private bool haveDisplaysChanged()
        {
            resetScreenCache();

            var newScreens = AppBarScreen.FromAllScreens();

            if (_screenState.Count == newScreens.Count)
            {
                bool same = true;
                for (int i = 0; i < newScreens.Count; i++)
                {
                    AppBarScreen current = newScreens[i];
                    if (!(_screenState[i].Bounds == current.Bounds && _screenState[i].DeviceName == current.DeviceName && _screenState[i].Primary == current.Primary && _screenState[i].HMonitor == current.HMonitor))
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    ShellLogger.Debug("WindowManager: No display changes");
                    return false;
                }
            }

            return true;
        }

        private void resetScreenCache()
        {
            // use reflection to empty screens cache
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var fi = typeof(Screen).GetField("screens", flags) ?? typeof(Screen).GetField("s_screens", flags)
                ?? throw new Exception("Can't find & reset screens cache inside winforms");
            fi.SetValue(null, null);
        }

        public void Dispose()
        {
            _shellManager.ExplorerHelper.HideExplorerTaskbar = false;
            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
        }
    }
}
