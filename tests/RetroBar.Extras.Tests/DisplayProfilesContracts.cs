using ManagedShell.AppBar;
using RetroBar.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Windows;
using RetroBar.Controls;

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
        Check(new DisplayTaskbarProfile { Rows = 32 }.Clone().Rows == 32 &&
              new DisplayTaskbarProfile { Rows = 33 }.Rows == 32 &&
              new DisplayTaskbarProfile { Rows = 0 }.Rows == 1,
              "Display profiles share the global 1–32 row bounds.");
        var edgeConverter = new RetroBar.Converters.EdgeOrientationConverter();
        var icons = new RetroBar.Converters.IconListOrientation();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Check((Orientation)edgeConverter.Convert(Orientation.Vertical, typeof(Orientation), null, culture) == Orientation.Vertical &&
              (Orientation)edgeConverter.Convert(Orientation.Horizontal, typeof(Orientation), null, culture) == Orientation.Horizontal,
              "Task list wrapping follows each actual host orientation.");
        Check((Orientation)icons.Convert(new object[] { Orientation.Horizontal, 32 }, typeof(Orientation), null, culture) == Orientation.Vertical &&
              (Orientation)icons.Convert(new object[] { Orientation.Vertical, 32 }, typeof(Orientation), null, culture) == Orientation.Horizontal &&
              (Orientation)icons.Convert(new object[] { Orientation.Horizontal, 1 }, typeof(Orientation), null, culture) == Orientation.Horizontal,
              "Quick Launch and tray wrap according to actual host edge and rows.");
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

        // Selected secondary shell host delivers each balloon exactly once.
        Check(NotifyIcon.ShouldDeliverBalloon(true, false, true, true),
            "Selected secondary shell host delivers a matching balloon.");
        Check(!NotifyIcon.ShouldDeliverBalloon(false, false, true, true),
            "Non-host displays cannot deliver a shell-host balloon.");
        Check(!NotifyIcon.ShouldDeliverBalloon(true, true, true, true),
            "Already-handled live balloons are never duplicated.");
        Check(!NotifyIcon.ShouldDeliverBalloon(true, false, false, true) &&
              !NotifyIcon.ShouldDeliverBalloon(true, false, true, false),
            "Mismatched and hidden icon balloons are rejected.");
        Check(StartButton.UseTargetedStartLauncher(true, false, false),
            "Profile-enabled secondary Start targets its own HWND.");
        Check(!StartButton.UseTargetedStartLauncher(false, false, false) &&
              StartButton.UseTargetedStartLauncher(false, true, true),
            "Legacy Start launcher routing is unchanged.");

        var source = new ObservableCollection<object> { "keep", "drop" };
        var sharedView = new ListCollectionView(source);
        Predicate<object> previous = item => (string)item != "drop";
        sharedView.Filter = previous;
        UnpinnedIconFilterLease.Acquire(sharedView);
        UnpinnedIconFilterLease.Acquire(sharedView);
        Check(UnpinnedIconFilterLease.OwnerCount(sharedView) == 2 &&
              !ReferenceEquals(sharedView.Filter, previous),
            "Two trays use a shared static predicate without capturing UI controls.");
        UnpinnedIconFilterLease.Release(sharedView);
        Check(UnpinnedIconFilterLease.OwnerCount(sharedView) == 1,
            "Unloading one of two trays retains the other filter lease.");
        UnpinnedIconFilterLease.Release(sharedView);
        Check(UnpinnedIconFilterLease.OwnerCount(sharedView) == 0 &&
              ReferenceEquals(sharedView.Filter, previous),
            "Final tray unload conditionally restores the previous predicate.");
        UnpinnedIconFilterLease.Acquire(sharedView);
        Predicate<object> external = item => true;
        sharedView.Filter = external;
        UnpinnedIconFilterLease.Release(sharedView);
        Check(ReferenceEquals(sharedView.Filter, external),
            "An external replacement filter is not clobbered on tray unload.");

        // Exercise the real Clock Loaded/Unloaded and settings-change handlers on STA
        // without launching ManagedShell or creating a Windows taskbar.
        using (var clockSettings = new LayoutSettingsScope("ShowClock"))
        {
            clockSettings.Set("ShowClock", false);
            var clock = new Clock { DisplayClockOverride = true };
            try
            {
                clock.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Check(clock.Visibility == Visibility.Visible, "Display clock override can enable clock with global clock disabled.");
                clock.DisplayClockOverride = false;
                Check(clock.Visibility == Visibility.Collapsed, "Profile update stops the clock while loaded.");
                clockSettings.Set("ShowClock", true);
                Check(clock.Visibility == Visibility.Collapsed, "Global clock changes cannot override a disabled display clock.");
                clock.DisplayClockOverride = null;
                Check(clock.Visibility == Visibility.Visible, "Clearing the override inherits the enabled global clock.");
                clockSettings.Set("ShowClock", false);
                Check(clock.Visibility == Visibility.Collapsed, "Legacy clock follows the global setting.");
                clock.DisplayClockOverride = true;
                Check(clock.Visibility == Visibility.Visible, "A live profile change re-enables the clock.");
            }
            finally
            {
                clock.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            }
        }
    }
}
