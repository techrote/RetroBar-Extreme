using ManagedShell.AppBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RetroBar.Utilities
{
    // Serializable display-scoped policy. Profiles are opt-in and do not overwrite legacy settings.
    public sealed class DisplayTaskbarProfile
    {
        public string DeviceName { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public AppBarEdge Edge { get; set; } = AppBarEdge.Bottom;
        public bool AutoHide { get; set; } = false;
        private int _rows = 1;
        public int Rows
        {
            get => _rows;
            set => _rows = Math.Clamp(value, 1, Settings.MaximumTaskbarRows);
        }
        public int Width { get; set; } = 1;
        public bool ShowStart { get; set; } = true;
        public bool ShowQuickLaunch { get; set; } = true;
        public bool ShowTray { get; set; } = true;
        public bool ShowClock { get; set; } = true;
        public bool ShowDesktop { get; set; } = true;
        public DisplayWindowSource WindowSource { get; set; } = DisplayWindowSource.AllMonitors;
        // Comma, semicolon or newline-separated executable names, families or AppUserModelIDs.
        public string IncludeApps { get; set; } = "";
        public string ExcludeApps { get; set; } = "";
        // Null inherits the global arrangement; non-null permits a different mode per taskbar.
        public TaskArrangementMode? ArrangementOverride { get; set; }
        public List<TaskApplicationPolicy> ApplicationOverrides { get; set; } = new();

        public DisplayTaskbarProfile Clone() => new()
        {
            DeviceName = DeviceName, Enabled = Enabled, Edge = Edge, AutoHide = AutoHide,
            Rows = Rows, Width = Width, ShowStart = ShowStart, ShowQuickLaunch = ShowQuickLaunch,
            ShowTray = ShowTray, ShowClock = ShowClock, ShowDesktop = ShowDesktop, WindowSource = WindowSource,
            IncludeApps = IncludeApps, ExcludeApps = ExcludeApps,
            ArrangementOverride = ArrangementOverride,
            ApplicationOverrides = (ApplicationOverrides ?? new List<TaskApplicationPolicy>())
                .Where(p => p != null).Select(p => p.Clone()).ToList()
        };
    }

    public enum DisplayWindowSource
    {
        AllMonitors,
        ThisMonitor
    }

    public static class DisplayTaskbarProfiles
    {
        public static DisplayTaskbarProfile Find(IEnumerable<DisplayTaskbarProfile> profiles, string deviceName) =>
            profiles?.FirstOrDefault(p => p != null &&
                string.Equals(p.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

        public static bool AllowsApplication(string applicationKey, DisplayTaskbarProfile profile)
        {
            if (profile == null) return true;
            if (!string.IsNullOrWhiteSpace(profile.IncludeApps) && !MatchesAny(applicationKey, profile.IncludeApps))
                return false;
            return !MatchesAny(applicationKey, profile.ExcludeApps);
        }

        public static bool MatchesAny(string key, string csv)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(csv)) return false;
            var app = key.Trim().ToLowerInvariant();
            var stem = Path.GetFileNameWithoutExtension(app);
            foreach (var token in csv.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = token.Trim().ToLowerInvariant();
                if (t == "*") return true;
                if (t == "terminals" || t == "terminal")
                {
                    if (new[] { "windowsterminal", "wt", "powershell", "pwsh", "cmd", "wezterm",
                        "alacritty", "mintty", "tabby", "hyper", "conemu", "bash", "wsl", "ubuntu" }.Contains(stem) ||
                        app.StartsWith("microsoft.windowsterminal_", StringComparison.OrdinalIgnoreCase) ||
                        app.StartsWith("microsoft.powershell_", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                // Exact names and AUMIDs; do not substring-match arbitrary executable paths.
                if (app == t || stem == Path.GetFileNameWithoutExtension(t) ||
                    (app.Contains('!') && app.StartsWith(t + "!", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }
    }
}
