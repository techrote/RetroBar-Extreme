using RetroBar.Utilities;
using System.Collections.Generic;

internal static partial class Program
{
    private static void TaskAffinityContracts()
    {
        var profile = new DisplayTaskbarProfile();
        var settingsType = typeof(TaskAffinityRules).Assembly.GetType("RetroBar.Utilities.Settings", true);
        var settings = System.Activator.CreateInstance(settingsType);
        Check(settingsType.GetProperty("TaskArrangement").GetValue(settings) is TaskArrangementMode.Separate,
            "Legacy mode remains the default.");
        Check(TaskAffinityRules.ModeFor(profile) == TaskArrangementMode.Separate, "Null per-display override inherits global mode.");
        profile.ArrangementOverride = TaskArrangementMode.Hybrid;
        Check(TaskAffinityRules.ModeFor(profile) == TaskArrangementMode.Hybrid, "Per-display override selects Hybrid.");
        Check(TaskAffinityRules.PresentationFor("firefox.exe", profile) == TaskApplicationPresentation.Affinity,
            "Hybrid unspecified apps use affinity, not bundling.");
        profile.ApplicationOverrides.Add(new TaskApplicationPolicy
        {
            Apps = "firefox.exe", Presentation = TaskApplicationPresentation.Bundle, Expansion = TaskBundleExpansion.Inline
        });
        Check(TaskAffinityRules.PresentationFor("firefox.exe", profile) == TaskApplicationPresentation.Bundle,
            "Per-display policy selects bundling.");
        Check(TaskAffinityRules.ExpansionFor("firefox.exe", profile) == TaskBundleExpansion.Inline, "Per-display inline expansion.");
        Check(TaskAffinityRules.PresentationFor("explorer.exe", profile) == TaskApplicationPresentation.Affinity, "Other apps unaffected.");
        var clone = profile.Clone();
        clone.ApplicationOverrides[0].Presentation = TaskApplicationPresentation.Separate;
        Check(profile.ApplicationOverrides[0].Presentation == TaskApplicationPresentation.Bundle, "Display profile deep-clones app overrides.");
        var groups = new List<TaskAffinityGroupRule> {
            new() { Name = "Terminals", Apps = "terminals" },
            new() { Name = "Browsers", Apps = "firefox.exe,chrome.exe" },
        };
        Check(TaskAffinityRules.BucketFor("pwsh.exe", groups) == TaskAffinityRules.BucketFor("cmd.exe", groups),
            "Terminals share group bucket.");
        Check(TaskAffinityRules.BucketFor("firefox.exe", groups) != TaskAffinityRules.BucketFor("pwsh.exe", groups),
            "Separate groups remain separate.");
        Check(TaskAffinityRules.BucketFor("explorer.exe", groups).StartsWith("app:"),
            "Unlisted apps have their own identity.");
    }
}
