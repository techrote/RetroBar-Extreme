using RetroBar.Utilities;

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
    }
}
