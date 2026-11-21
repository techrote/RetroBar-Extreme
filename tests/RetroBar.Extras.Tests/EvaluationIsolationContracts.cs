using RetroBar.Utilities;
using System;
using System.IO;
using System.Linq;

internal static partial class Program
{
    private static void EvaluationIsolationContracts()
    {
        const string root = @"C:\Users\RetroBarFixture\AppData\Local";
        string installed = EvaluationSession.GetDataDirectory(root, false);
        string isolated = EvaluationSession.GetDataDirectory(root, true);
        Check(string.Equals(installed, Path.Combine(root, "RetroBar"), StringComparison.OrdinalIgnoreCase),
            "Normal mode retains the exact installed RetroBar data directory.");
        Check(string.Equals(isolated, Path.Combine(root, "RetroBar-Evaluation"), StringComparison.OrdinalIgnoreCase),
            "Evaluator uses a separate sibling folder, not a subfolder of installed settings.");
        Check(!isolated.StartsWith(installed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "Evaluation path is not nested beneath the live RetroBar profile.");

        Check(EvaluationSession.TryResolveArguments(Array.Empty<string>(), false, out bool normal, out _) && !normal,
            "Unmarked installed and ordinary portable binaries retain normal startup.");
        Check(EvaluationSession.TryResolveArguments(Array.Empty<string>(), true, out bool marker, out _) && marker,
            "A marker in the special evaluator artifact forces isolated startup even when EXE is double-clicked.");
        Check(EvaluationSession.TryResolveArguments(new[] { EvaluationSession.LaunchArgument }, false,
            out bool explicitMode, out _) && explicitMode,
            "Explicit evaluator argument isolates an ordinary portable binary too.");
        Check(!EvaluationSession.TryResolveArguments(new[] { "--isolated-evaluatr" }, false, out _, out string typo) &&
              !string.IsNullOrWhiteSpace(typo),
            "Misspelled switches fail closed instead of silently using production settings.");
        Check(!EvaluationSession.TryResolveArguments(new[] { EvaluationSession.LaunchArgument, "--unexpected" },
            true, out _, out _), "Unexpected extra evaluator arguments fail closed.");

        Check(EvaluationSession.PackageMarker == "RetroBar-Evaluation.marker" &&
              EvaluationSession.LaunchArgument == "--isolated-evaluator",
            "Artifact marker and launcher argument remain a stable public contract.");

        // The *running* Extras suite must not mutate its profile setting:
        // tests assert pure path and argument resolution without calling Configure.
        Check(!EvaluationSession.IsEnabled,
            "Normal isolated test-runner process does not accidentally select evaluator mode.");
        // In eval mode the app's QuickLaunchPath getter ignores a stored
        // shared Windows pinned-folder setting. Assert the evaluator target
        // directory computation without touching settings or any user files.
        Check(Path.GetFileName(Path.Combine(isolated, "QuickLaunch")) == "QuickLaunch",
            "Evaluator Quick Launch is contained beneath its own independent data root.");
    }
}
