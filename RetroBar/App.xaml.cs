using System;
using ManagedShell;
using RetroBar.Utilities;
using System.Windows;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using Application = System.Windows.Application;
using System.Windows.Interop;
using System.Windows.Media;
using ManagedShell.Common.Enums;
using System.Diagnostics;
using System.Reflection;
using ManagedShell.Common.Logging;
using System.Linq;

namespace RetroBar
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool _errorVisible;
        private bool _evaluationShutdownRequested;
        private bool _evaluationCleanupCompleted;
        private ManagedShellLogger _logger;
        private WindowManager _windowManager;

        private readonly DictionaryManager _dictionaryManager;
        private readonly ExplorerMonitor _explorerMonitor;
        private readonly ShellManager _shellManager;
        private readonly StartMenuMonitor _startMenuMonitor;
        private readonly Updater _updater;
        private readonly HotkeyManager _hotkeyManager;

        public App()
        {
            _shellManager = SetupManagedShell();

            _explorerMonitor = new ExplorerMonitor();
            _startMenuMonitor = new StartMenuMonitor(new AppVisibilityHelper(false));
            _dictionaryManager = new DictionaryManager();
            _updater = new Updater();
            _hotkeyManager = new HotkeyManager();

            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        public void ExitGracefully()
        {
            if (EvaluationSession.IsEnabled)
            {
                RequestEvaluationShutdown(0);
                return;
            }
            _shellManager.AppBarManager.SignalGracefulShutdown();
            Current.Shutdown();
        }

        // Evaluator failures must never re-enter the normal FailFast/restart path.
        // Restore Explorer even if WindowManager construction failed after hiding it,
        // and attempt shutdown independently of individual shell-service failures.
        private void RequestEvaluationShutdown(int exitCode)
        {
            if (_evaluationShutdownRequested) return;
            _evaluationShutdownRequested = true;
            EvaluationSession.RunRecoverySteps(
                ex => Trace.TraceError("Evaluator recovery: " + ex),
                () => _shellManager?.AppBarManager.SignalGracefulShutdown(),
                () => { if (_shellManager != null) _shellManager.ExplorerHelper.HideExplorerTaskbar = false; },
                () => Shutdown(exitCode));
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            if (Settings.Instance.UseSoftwareRendering)
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }

            _dictionaryManager.SetLanguageFromSettings();
            loadTheme();
            TaskbarTextStyler.Initialize(); // UXUI-Extreme (UX-002, UX-005)
            TaskbarImageQuality.Initialize(); // UXUI-Extreme: smooth icon scaling
            StyleLayer.Initialize(); // UXUI-Extreme (UX-003): reload when Style.json changes
            // UXUI-Extreme (UX-013): per-part borders are baked into the theme, so a style that
            // changes them needs the theme reloaded and the taskbars rebuilt.
            StyleLayer.ThemeReloadNeeded += (s, args) => Dispatcher.BeginInvoke(new Action(() =>
            {
                loadTheme();
                _windowManager?.ReopenTaskbars();
            }));
            _windowManager = new WindowManager(_dictionaryManager, _explorerMonitor, _shellManager, _startMenuMonitor, _updater, _hotkeyManager);
        }

        private void App_OnExit(object sender, ExitEventArgs e)
        {
            ExitApp();
        }

        private void App_OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            ExitApp();
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.UseSoftwareRendering))
            {
                if (Settings.Instance.UseSoftwareRendering)
                {
                    RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
                }
                else
                {
                    RenderOptions.ProcessRenderMode = RenderMode.Default;
                }
            }
            else if (e.PropertyName == nameof(Settings.Theme) || e.PropertyName == nameof(Settings.TaskbarScale))
            {
                setTaskIconSize();
            }

            // UXUI-Extreme (UX-006): native sizing bakes the scale into the theme, so a scale or
            // mode change that alters it needs the theme reloaded and the taskbars rebuilt.
            if ((e.PropertyName == nameof(Settings.TaskbarScale) || e.PropertyName == nameof(Settings.ScalingMode)) &&
                ScaleModel.ThemeScale != _loadedThemeScale)
            {
                loadTheme();
                _windowManager?.ReopenTaskbars();
            }

        }

        private double _loadedThemeScale = 1.0; // UXUI-Extreme (UX-006)

        private void loadTheme()
        {
            _loadedThemeScale = ScaleModel.ThemeScale; // UXUI-Extreme (UX-006)
            _dictionaryManager.SetThemeFromSettings();
            setTaskIconSize();
        }

        private void setTaskIconSize()
        {
            bool useLargeIcons = Settings.Instance.TaskbarScale > 1 || (FindResource("UseLargeIcons") as bool? ?? false);

            if (_shellManager.TasksService.TaskIconSize != IconSize.Small != useLargeIcons)
            {
                _shellManager.TasksService.TaskIconSize = useLargeIcons ? IconSize.Large : IconSize.Small;
            }
        }

        private ShellManager SetupManagedShell()
        {
            EnvironmentHelper.IsAppRunningAsShell = NativeMethods.GetShellWindow() == IntPtr.Zero;

            _logger = new ManagedShellLogger();

            ShellConfig config = ShellManager.DefaultShellConfig;
            config.PinnedNotifyIcons = Settings.Instance.NotifyIconBehaviors.Where(setting => setting.Behavior == NotifyIconBehavior.AlwaysShow).Select(setting => setting.Identifier).ToArray();

            return new ShellManager(config);
        }

        public void RestartApp()
        {
            if (EvaluationSession.IsEnabled)
            {
                // Crash/restart recovery must never restart as a production
                // instance or race the running shell-owner mutex. Exit and
                // require a deliberate fresh launch from the evaluation ZIP.
                ShellLogger.Warning("Evaluation restart suppressed: close and launch the isolated evaluator again.");
                ExitGracefully();
                return;
            }
            try
            {
                // run the program again
                Process current = new Process();
                current.StartInfo.FileName = ExePath.GetExecutablePath();
                current.Start();

                // close this instance
                ExitGracefully();
            }
            catch
            { }
        }

        private void ExitApp()
        {
            if (EvaluationSession.IsEnabled)
            {
                if (_evaluationCleanupCompleted) return;
                _evaluationCleanupCompleted = true;
                // A startup failure may leave the WindowManager unset. Restore the
                // native taskbar directly before disposing services; one failed
                // disposal must not prevent the rest of the cleanup sequence.
                EvaluationSession.RunRecoverySteps(
                    ex => Trace.TraceError("Evaluator cleanup: " + ex),
                    () => { if (_shellManager != null) _shellManager.ExplorerHelper.HideExplorerTaskbar = false; },
                    () => Settings.Instance.PropertyChanged -= Settings_PropertyChanged,
                    () => _explorerMonitor?.Dispose(),
                    () => _windowManager?.Dispose(),
                    () => _dictionaryManager?.Dispose(),
                    () => _shellManager?.Dispose(),
                    () => _startMenuMonitor?.Dispose(),
                    () => _updater?.Dispose(),
                    () => _hotkeyManager?.Dispose(),
                    () => _logger?.Dispose());
                return;
            }
            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;

            _explorerMonitor.Dispose();
            _windowManager.Dispose();
            _dictionaryManager.Dispose();
            _shellManager.Dispose();
            _startMenuMonitor.Dispose();
            _updater.Dispose();
            _hotkeyManager.Dispose();
            _logger.Dispose();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            if (EvaluationSession.IsEnabled)
            {
                // No automatic relaunch or FailFast in the guarded evaluator.
                // Logging itself may fail (including a damaged rendering thread),
                // but it must not suppress the recovery attempt.
                e.Handled = true;
                EvaluationSession.RunRecoverySteps(
                    ex => Trace.TraceError("Evaluator exception logging: " + ex),
                    () => ShellLogger.Error("Isolated evaluation failed; attempting graceful Explorer recovery.", e.Exception));
                RequestEvaluationShutdown(1);
                return;
            }

            Assembly assembly = Assembly.GetExecutingAssembly();
            // Single-file portable builds have an empty Assembly.Location.
            string version = string.IsNullOrEmpty(assembly.Location)
                ? assembly.GetName().Version?.ToString() ?? "unknown"
                : FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;

            string inner = "";
            if (e.Exception.InnerException != null)
                inner = "\r\n\r\nInner exception:\r\nMessage: " + e.Exception.InnerException.Message + "\r\nTarget Site: " + e.Exception.InnerException.TargetSite + "\r\n\r\n" + e.Exception.InnerException.StackTrace;

            string msg = "Would you like to restart RetroBar?\r\n\r\nPlease submit a bug report with a screenshot of this error. Thanks! \r\nMessage: " + e.Exception.Message + "\r\nTarget Site: " + e.Exception.TargetSite + "\r\nVersion: " + version + "\r\n\r\n" + e.Exception.StackTrace + inner;

            ShellLogger.Error(msg, e.Exception);

            string dMsg;

            if (msg.Length > 1000)
                dMsg = msg.Substring(0, 999) + "...";
            else
                dMsg = msg;

            try
            {
                if (!_errorVisible)
                {
                    _errorVisible = true;

                    // Automatically restart for known render thread failure messages.
                    if (e.Exception.Message.StartsWith("UCEERR_RENDERTHREADFAILURE"))
                    {
                        RestartApp();
                        Environment.FailFast("Automatically restarted RetroBar due to a render thread failure.");
                    }
                    else
                    {
                        if (MessageBox.Show(dMsg, "RetroBar Error", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            // it's like getting a morning coffee.
                            RestartApp();
                            Environment.FailFast("User restarted RetroBar due to an exception.");
                        }
                    }

                    _errorVisible = false;
                }
            }
            catch
            {
                // If this fails we're probably up the creek. Abandon ship!
                ExitGracefully();
            }

            e.Handled = true;
        }
    }
}