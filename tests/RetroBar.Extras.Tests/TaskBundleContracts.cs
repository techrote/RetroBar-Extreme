using RetroBar.Utilities;
using RetroBar.Converters;
using ManagedShell.AppBar;
using System.Windows.Controls;

internal static partial class Program
{
    private static void TaskBundleContracts()
    {
        var entry = new TaskBundleEntry { Key = "bundle:firefox.exe", CanBundle = true, Expansion = TaskBundleExpansion.Inline };
        Check(entry.WindowCount == 0 && !entry.IsBundle, "Empty groups must not present a bundle.");
        entry.Windows.Add(null);
        Check(entry.WindowCount == 1 && !entry.IsBundle, "Single window stays an individual button.");
        entry.Windows.Add(null);
        Check(entry.WindowCount == 2 && entry.IsBundle, "Two windows enter Bundle presentation.");
        Check(entry.Expansion == TaskBundleExpansion.Inline, "Expansion policy is retained.");
        entry.CanBundle = false;
        Check(entry.WindowCount == 2 && !entry.IsBundle, "Separate presentation never combines windows.");
        entry.Windows.Clear();
        Check(entry.Representative == null && entry.SingleWindow == null && entry.DisplayName == "", "Empty representatives are safe.");
        var profile = new DisplayTaskbarProfile { ArrangementOverride = TaskArrangementMode.Hybrid };
        profile.ApplicationOverrides.Add(new TaskApplicationPolicy {
            Apps = "firefox.exe", Presentation = TaskApplicationPresentation.Bundle, Expansion = TaskBundleExpansion.Flyout });
        profile.ApplicationOverrides.Add(new TaskApplicationPolicy {
            Apps = "explorer.exe", Presentation = TaskApplicationPresentation.Separate, Expansion = TaskBundleExpansion.Inline });
        Check(TaskAffinityRules.PresentationFor("firefox.exe", profile) == TaskApplicationPresentation.Bundle, "Firefox bundles.");
        Check(TaskAffinityRules.ExpansionFor("firefox.exe", profile) == TaskBundleExpansion.Flyout, "Firefox uses flyout.");
        Check(TaskAffinityRules.PresentationFor("explorer.exe", profile) == TaskApplicationPresentation.Separate, "Explorer stays separate.");
        Check(TaskAffinityRules.ExpansionFor("explorer.exe", profile) == TaskBundleExpansion.Inline, "Dormant expansion settings persist.");
        Check(TaskAffinityRules.PresentationFor("pwsh.exe", profile) == TaskApplicationPresentation.Affinity, "Other apps retain affinity.");
        var outer = new TaskBundleEntryWidthConverter();
        var vertical = outer.Convert(new object[] { Orientation.Vertical, 172.0 }, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture);
        var horizontal = outer.Convert(new object[] { Orientation.Horizontal, 172.0 }, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture);
        Check(vertical is double vw && vw == 172.0, "Vertical bundle width follows the taskbar.");
        Check(horizontal is double hw && double.IsNaN(hw), "Horizontal bundle width grows with inline children.");
        var inner = new TaskBundleButtonWidthConverter();
        var fallback = inner.Convert(new object[] { new TaskBundleEntry(), 172.0, 0, 1, null, 0.0, 24.0, 172.0, AppBarEdge.Right },
            typeof(double), null, System.Globalization.CultureInfo.InvariantCulture);
        Check(fallback is double bw && bw == 172.0, "Bundle width adapter uses normal taskbar width when no representative is available.");
    }
}
