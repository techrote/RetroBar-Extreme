using System;
using System.IO;
using System.Collections.Generic;

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

        // Fail closed on existing junctions, symlinks, non-directory roots or
        // inaccessible entries, including aliases *inside* the evaluator tree.
        // Never enumerate the installed profile. This is an accidental-data-
        // isolation guard, not a security boundary against concurrent edits.
        public static bool IsReparsePointDataAlias() =>
            IsEnabled && !IsProfileTreeSafe(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        private static FileAttributes? ExistingAttributes(string path)
        {
            try { return File.GetAttributes(path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        private static bool IsPlainDirectoryOrMissing(FileAttributes? attributes) =>
            !attributes.HasValue ||
            ((attributes.Value & FileAttributes.Directory) != 0 &&
             (attributes.Value & FileAttributes.ReparsePoint) == 0);

        public static bool IsProfileTreeSafe(string localAppData)
        {
            try
            {
                string local = Path.GetFullPath(localAppData);
                string isolated = GetDataDirectory(local, true);
                string installed = GetDataDirectory(local, false);
                if (!IsPlainDirectoryOrMissing(ExistingAttributes(local)) ||
                    !IsPlainDirectoryOrMissing(ExistingAttributes(installed)) ||
                    !IsPlainDirectoryOrMissing(ExistingAttributes(isolated)))
                    return false;

                if (!ExistingAttributes(isolated).HasValue) return true;
                var pending = new Stack<string>();
                pending.Push(isolated);
                while (pending.Count > 0)
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
                    {
                        FileAttributes? attributes = ExistingAttributes(entry);
                        if (!attributes.HasValue || (attributes.Value & FileAttributes.ReparsePoint) != 0)
                            return false;
                        if ((attributes.Value & FileAttributes.Directory) != 0)
                            pending.Push(entry);
                    }
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException)
            {
                return false;
            }
        }

        // Check the canonical path and each existing component without following
        // reparse points. Missing descendants may be created by the caller.
        public static bool IsProfilePathSafe(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root)) return false;
            try
            {
                string canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string prefix = canonicalRoot + Path.DirectorySeparatorChar;
                if (!string.Equals(target, canonicalRoot, StringComparison.OrdinalIgnoreCase) &&
                    !target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

                if (!IsPlainDirectoryOrMissing(ExistingAttributes(Path.GetDirectoryName(canonicalRoot))) ||
                    !IsPlainDirectoryOrMissing(ExistingAttributes(canonicalRoot))) return false;
                if (string.Equals(target, canonicalRoot, StringComparison.OrdinalIgnoreCase)) return true;

                string[] parts = target.Substring(prefix.Length).Split(Path.DirectorySeparatorChar);
                string current = canonicalRoot;
                for (int i = 0; i < parts.Length; i++)
                {
                    // NTFS alternate data streams and ambiguous empty path components
                    // are not valid evaluator storage paths.
                    if (parts[i].Length == 0 || parts[i].Contains(":")) return false;
                    current = Path.Combine(current, parts[i]);
                    FileAttributes? attributes = ExistingAttributes(current);
                    if (attributes.HasValue && ((attributes.Value & FileAttributes.ReparsePoint) != 0 ||
                        (i < parts.Length - 1 && (attributes.Value & FileAttributes.Directory) == 0)))
                        return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException)
            {
                return false;
            }
        }

        public static void RequireSafeProfilePath(string path)
        {
            if (IsEnabled && !IsProfilePathSafe(path, DataDirectory))
                throw new InvalidOperationException("Evaluator storage path is outside the private profile or contains an unsafe filesystem alias.");
        }

        // Keep error recovery independent of WPF and ManagedShell so its
        // ordering and failure behaviour can be exercised without shell takeover.
        // Diagnostics are best-effort as well: a failed log must not stop recovery.
        public static void RunRecoverySteps(Action<Exception> onFailure, params Action[] steps)
        {
            foreach (Action step in steps)
            {
                try { step(); }
                catch (Exception ex)
                {
                    try { onFailure?.Invoke(ex); }
                    catch (Exception) { /* Continue attempting the remaining recovery steps. */ }
                }
            }
        }

        public static void Configure(bool evaluation)
        {
            if (_configured)
                throw new InvalidOperationException("Evaluation mode must be configured once, before application startup.");
            _evaluation = evaluation;
            _configured = true;
        }

        public static bool IsAllowedQuickLaunchWriteFolder(string path) =>
            !IsEnabled || (IsAllowedQuickLaunchWriteFolder(path, QuickLaunchDirectory) &&
                IsProfilePathSafe(Environment.ExpandEnvironmentVariables(path), DataDirectory) &&
                IsPlainDirectoryOrMissing(ExistingAttributes(QuickLaunchDirectory)));

        // Pure policy seam: tests can validate disallowed Windows pinned folders
        // and path traversal without changing the process-wide startup profile.
        public static bool IsAllowedQuickLaunchWriteFolder(string path, string evaluationFolder)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(evaluationFolder))
                return false;
            try
            {
                string requested = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string isolated = Path.GetFullPath(evaluationFolder)
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
