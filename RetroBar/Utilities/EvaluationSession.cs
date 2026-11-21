using System;
using System.IO;

namespace RetroBar.Utilities
{
    /// <summary>
    /// Opt-in evaluation profile. Configure before the first Settings access or
    /// App construction. Regular installed and unmarked portable builds retain
    /// their original paths and startup behaviour.
    /// </summary>
    public static class EvaluationSession
    {
        public const string LaunchArgument = "--isolated-evaluator";
        public const string PackageMarker = "RetroBar-Evaluation.marker";

        private static bool _configured;
        private static bool _evaluation;

        public static bool IsEnabled => _configured && _evaluation;

        public static string DataDirectory => GetDataDirectory(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), IsEnabled);

        public static string QuickLaunchDirectory => Path.Combine(DataDirectory, "QuickLaunch");

        public static string GetDataDirectory(string localAppData, bool evaluation)
        {
            if (string.IsNullOrWhiteSpace(localAppData))
                throw new ArgumentException("Local AppData is unavailable.", nameof(localAppData));
            // Deliberately distinct sibling roots; never nest under a live
            // %LOCALAPPDATA%\\RetroBar profile or copy its settings.
            return Path.Combine(localAppData, evaluation ? "RetroBar-Evaluation" : "RetroBar");
        }

        public static bool TryResolveArguments(string[] args, bool packageMarked,
            out bool evaluation, out string error)
        {
            evaluation = packageMarked;
            error = null;
            if (args == null || args.Length == 0) return true;
            if (args.Length == 1 && string.Equals(args[0], LaunchArgument, StringComparison.Ordinal))
            {
                evaluation = true;
                return true;
            }

            // Never silently fall back to the real settings when a user mistypes
            // the isolated launch flag.
            error = "Unrecognised RetroBar launch arguments. Use no arguments for normal " +
                    "RetroBar, or --isolated-evaluator for the isolated evaluation profile.";
            return false;
        }

        public static void Configure(bool evaluation)
        {
            if (_configured)
                throw new InvalidOperationException("Evaluation mode must be configured once, before application startup.");
            _evaluation = evaluation;
            _configured = true;
        }

        public static bool IsAllowedQuickLaunchWriteFolder(string path)
        {
            if (!IsEnabled) return true;
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string requested = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string isolated = Path.GetFullPath(QuickLaunchDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(requested, isolated, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                return false;
            }
        }
    }
}
