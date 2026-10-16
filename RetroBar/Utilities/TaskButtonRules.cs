using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedShell.WindowsTasks;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-014): per-app task button settings. An app is its exe file name (or its
    // AppUserModelID for Store apps), so every window of the app shares one remembered setting.
    // Each per-app value overrides the all-apps setting, or leaves it alone (Default).
    public static class TaskButtonRules
    {
        public const double IconOnly = -1;
        public const double NarrowWidth = 80, WideWidth = 240; // px at 100%

        public static string AppKey(ApplicationWindow window)
        {
            if (window == null)
            {
                return null;
            }

            if (window.IsUWP && !string.IsNullOrEmpty(window.AppUserModelID))
            {
                return window.AppUserModelID;
            }

            string file = window.WinFileName;
            return string.IsNullOrEmpty(file) ? null : Path.GetFileName(file).ToLowerInvariant();
        }

        public static string AppName(ApplicationWindow window)
        {
            if (window == null)
            {
                return "";
            }

            if (!window.IsUWP && !string.IsNullOrEmpty(window.WinFileName))
            {
                try
                {
                    string description = System.Diagnostics.FileVersionInfo.GetVersionInfo(window.WinFileName).FileDescription;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        return $"{description.Trim()} ({Path.GetFileName(window.WinFileName)})";
                    }
                }
                catch (Exception)
                {
                    // No version info (access denied, gone): fall back to the file name.
                }
                return Path.GetFileName(window.WinFileName);
            }

            return string.IsNullOrEmpty(window.Title) ? window.AppUserModelID ?? "" : window.Title;
        }

        public static TaskButtonAppRule Find(string app)
        {
            if (string.IsNullOrEmpty(app))
            {
                return null;
            }

            return Settings.Instance.TaskButtonAppRules?.FirstOrDefault(r => string.Equals(r.App, app, StringComparison.OrdinalIgnoreCase));
        }

        public static TaskButtonAppRule Find(ApplicationWindow window) => Find(AppKey(window));

        // Changes (or creates) an app's rule and saves the list. A rule left with nothing set is
        // forgotten, so the list only holds apps that are actually customised.
        public static void Update(string app, string name, Action<TaskButtonAppRule> change)
        {
            if (string.IsNullOrEmpty(app))
            {
                return;
            }

            var rules = (Settings.Instance.TaskButtonAppRules ?? new List<TaskButtonAppRule>()).Select(r => r.Clone()).ToList();
            TaskButtonAppRule rule = rules.FirstOrDefault(r => string.Equals(r.App, app, StringComparison.OrdinalIgnoreCase));
            if (rule == null)
            {
                rule = new TaskButtonAppRule { App = app, Name = name ?? app };
                rules.Add(rule);
            }
            else if (!string.IsNullOrEmpty(name))
            {
                rule.Name = name;
            }

            change(rule);

            if (rule.IsEmpty)
            {
                rules.Remove(rule);
            }

            Settings.Instance.TaskButtonAppRules = rules;
        }

        public static void Forget(string app) => Update(app, null, r =>
        {
            r.Width = 0;
            r.Icon = AppOverrideOption.Default;
            r.Label = AppOverrideOption.Default;
        });

        public static bool ShowIcon(TaskButtonAppRule rule) => Effective(rule?.Icon, Settings.Instance.TaskButtonShowIcons);

        // Icon-only buttons hide the label unless the app explicitly shows it.
        public static bool ShowLabel(TaskButtonAppRule rule) =>
            rule?.Width == IconOnly && rule.Label == AppOverrideOption.Default ? false : Effective(rule?.Label, Settings.Instance.TaskButtonShowLabels);

        private static bool Effective(AppOverrideOption? option, bool allApps) => option switch
        {
            AppOverrideOption.Show => true,
            AppOverrideOption.Hide => false,
            _ => allApps,
        };
    }
}
