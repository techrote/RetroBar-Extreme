using RetroBar.Utilities;
using System;
using System.IO;
using System.Diagnostics;

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
        string evalQuickLaunch = Path.Combine(isolated, "QuickLaunch");
        Check(EvaluationSession.IsAllowedQuickLaunchWriteFolder(evalQuickLaunch, evalQuickLaunch) &&
              !EvaluationSession.IsAllowedQuickLaunchWriteFolder(
                  Path.Combine(root, "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar"),
                  evalQuickLaunch) &&
              !EvaluationSession.IsAllowedQuickLaunchWriteFolder(
                  Path.Combine(evalQuickLaunch, "..", "RetroBar", "QuickLaunch"), evalQuickLaunch),
            "Evaluator shortcut destinations reject the actual Windows pinned folder and path escapes.");

        // A real Windows junction inside the isolated profile must be rejected
        // even though its lexical path looks private. No shell is initialized.
        string fixture = Path.Combine(Path.GetTempPath(), "retrobar-eval-alias-" + Guid.NewGuid().ToString("N"));
        string fixtureProfile = EvaluationSession.GetDataDirectory(fixture, true);
        string installedFixture = EvaluationSession.GetDataDirectory(fixture, false);
        string junction = Path.Combine(fixtureProfile, "QuickLaunch");
        try
        {
            Directory.CreateDirectory(fixtureProfile);
            Directory.CreateDirectory(installedFixture);
            Check(EvaluationSession.IsProfileTreeSafe(fixture), "ordinary separate profile directories are accepted");
            Check(EvaluationSession.IsProfilePathSafe(Path.Combine(junction, "new.lnk"), fixtureProfile),
                "a new private shortcut path is accepted");
            Check(!EvaluationSession.IsProfilePathSafe(Path.Combine(installedFixture, "settings.json"), fixtureProfile),
                "canonical sibling production profile is rejected");
            Check(!EvaluationSession.IsProfilePathSafe(Path.Combine(fixtureProfile, "settings.json:stream"), fixtureProfile),
                "alternate data stream is rejected");
            var junctionStart = new ProcessStartInfo("cmd.exe")
            {
                Arguments = "/d /c mklink /J \"" + junction + "\" \"" + installedFixture + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process mklink = Process.Start(junctionStart))
            {
                Check(mklink != null && mklink.WaitForExit(15000) && mklink.ExitCode == 0,
                    "Windows fixture created a real junction");
            }
            Check(!EvaluationSession.IsProfileTreeSafe(fixture),
                "preflight rejects a junction nested in the evaluator tree");
            Check(!EvaluationSession.IsProfilePathSafe(Path.Combine(junction, "settings.json"), fixtureProfile),
                "per-path guard rejects a nested junction before shortcut or settings access");
        }
        finally
        {
            // Remove the junction itself, never recursively delete through it.
            if (Directory.Exists(junction)) Directory.Delete(junction);
            if (Directory.Exists(fixture)) Directory.Delete(fixture, true);
        }

        // Spawn a *shell-free child test process* to prove Settings' real static
        // initializer resolves the isolated path before any settings are loaded.
        // Do not call EvaluationSession.Configure in this parent test process.
        string command = Environment.ProcessPath;
        Check(!string.IsNullOrWhiteSpace(command) && File.Exists(command),
            "Windows test runner has an executable for the cold-start profile probe.");
        var start = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(command), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add("exec");
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
        }
        start.ArgumentList.Add("--probe-evaluation-profile");
        Check(!Directory.Exists(EvaluationSession.GetDataDirectory(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), true)),
            "cold profile probe requires a clean disposable Windows evaluator profile");
        using Process child = Process.Start(start);
        Check(child != null && child.WaitForExit(30000),
            "Isolated static Settings profile probe exits without ever creating ManagedShell.");
        Check(child.ExitCode == 0, "Cold startup bound the actual SettingsManager to the isolated settings.json.");
        string installedSettings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroBar", "settings.json");
        Check(!File.Exists(installedSettings),
            "Cold evaluator startup did not create the owner's production settings.json.");
    }
}
