using ManagedShell.AppBar;
using RetroBar.Utilities;
using System;
using System.Collections.Generic;

internal static partial class Program
{
    private static void DisplayProfileContracts()
    {
        var settingsType = typeof(DisplayTaskbarProfiles).Assembly.GetType("RetroBar.Utilities.Settings", true);
        var settings = Activator.CreateInstance(settingsType);
        Check(settingsType.GetProperty("UseDisplayProfiles").GetValue(settings) is false,
            "Display profiles default off, preserving legacy settings.");
        var p = new DisplayTaskbarProfile
        {
            DeviceName = @"\\.\DISPLAY3", Enabled = true,
            Edge = AppBarEdge.Right, Rows = 3, Width = 4,
            WindowSource = DisplayWindowSource.AllMonitors,
            IncludeApps = "terminals,firefox.exe", ExcludeApps = "firefox.exe"
        };
        var copy = p.Clone();
        Check(copy.DeviceName == p.DeviceName && copy.Edge == AppBarEdge.Right && copy.Width == 4, "Profile clone preserves dimensions.");
        copy.Width = 5;
        Check(p.Width == 4, "Clone cannot mutate original.");
        Check(DisplayTaskbarProfiles.Find(new List<DisplayTaskbarProfile> { p }, @"\\.\display3") == p,
            "Device name lookup ignores case.");
        Check(DisplayTaskbarProfiles.AllowsApplication("powershell.exe", p), "Terminal family includes Windows PowerShell.");
        Check(DisplayTaskbarProfiles.AllowsApplication("pwsh.exe", p), "Terminal family includes PowerShell 7.");
        Check(DisplayTaskbarProfiles.AllowsApplication("WindowsTerminal.exe", p), "Terminal family includes Windows Terminal.");
        Check(DisplayTaskbarProfiles.AllowsApplication("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", p),
            "Terminal family includes the Windows Terminal package AUMID.");
        Check(!DisplayTaskbarProfiles.AllowsApplication("firefox.exe", p), "Explicit exclusion overrides inclusion.");
        Check(!DisplayTaskbarProfiles.AllowsApplication("explorer.exe", p), "Include list restricts unrelated windows.");
        Check(DisplayTaskbarProfiles.MatchesAny("firefox.exe", "firefox"), "Executable stem matches without .exe.");
        Check(!DisplayTaskbarProfiles.MatchesAny("notfirefox.exe", "firefox"), "No loose substring matching.");
        Check(!DisplayTaskbarProfiles.MatchesAny(null, "terminals"), "Missing app keys do not match.");
        Check(DisplayTaskbarProfiles.MatchesAny("microsoft.windows.terminal_8wekyb3d8bbwe!App",
            "microsoft.windows.terminal_8wekyb3d8bbwe"), "Explicit AUMID family prefix matches.");
        Check(!DisplayTaskbarProfiles.MatchesAny("google.chromium!App", "microsoft.windows.terminal"),
            "Unrelated AUMID never matches.");
        Check(settingsType.GetProperty("DisplayTaskbarProfiles").GetValue(settings) != null, "Profile storage nonnull.");
    }
}
