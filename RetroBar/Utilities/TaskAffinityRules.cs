using ManagedShell.WindowsTasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RetroBar.Utilities
{
    public enum TaskArrangementMode { Separate, Affinity, Bundle, Hybrid }
    public enum TaskApplicationPresentation { Separate, Affinity, Bundle }
    public enum TaskBundleExpansion { Flyout, Inline }

    public sealed class TaskAffinityGroupRule
    {
        public string Name { get; set; } = "";
        public string Apps { get; set; } = "";
        public TaskAffinityGroupRule Clone() => new() { Name = Name, Apps = Apps };
    }

    public sealed class TaskApplicationPolicy
    {
        public string Apps { get; set; } = "";
        public TaskApplicationPresentation Presentation { get; set; } = TaskApplicationPresentation.Affinity;
        public TaskBundleExpansion Expansion { get; set; } = TaskBundleExpansion.Flyout;
        public TaskApplicationPolicy Clone() => new() { Apps = Apps, Presentation = Presentation, Expansion = Expansion };
    }

    public static class TaskAffinityRules
    {
        public static TaskArrangementMode ModeFor(DisplayTaskbarProfile profile) =>
            profile?.ArrangementOverride ?? Settings.Instance.TaskArrangement;

        public static TaskApplicationPolicy PolicyFor(string app, DisplayTaskbarProfile profile)
        {
            return (profile?.ApplicationOverrides ?? new List<TaskApplicationPolicy>())
                       .FirstOrDefault(p => p != null && DisplayTaskbarProfiles.MatchesAny(app, p.Apps))
                   ?? (Settings.Instance.TaskApplicationPolicies ?? new List<TaskApplicationPolicy>())
                       .FirstOrDefault(p => p != null && DisplayTaskbarProfiles.MatchesAny(app, p.Apps));
        }

        public static TaskApplicationPresentation PresentationFor(string app, DisplayTaskbarProfile profile)
        {
            var mode = ModeFor(profile);
            return mode switch
            {
                TaskArrangementMode.Separate => TaskApplicationPresentation.Separate,
                TaskArrangementMode.Affinity => TaskApplicationPresentation.Affinity,
                TaskArrangementMode.Bundle => TaskApplicationPresentation.Bundle,
                _ => PolicyFor(app, profile)?.Presentation ?? TaskApplicationPresentation.Affinity
            };
        }

        public static TaskBundleExpansion ExpansionFor(string app, DisplayTaskbarProfile profile) =>
            PolicyFor(app, profile)?.Expansion ?? TaskBundleExpansion.Flyout;

        public static string BucketFor(string app, IEnumerable<TaskAffinityGroupRule> groups)
        {
            string normalized = (app ?? "").Trim().ToLowerInvariant();
            if (normalized.Length == 0) return "";
            int index = 0;
            foreach (var group in groups ?? Enumerable.Empty<TaskAffinityGroupRule>())
            {
                if (group != null && DisplayTaskbarProfiles.MatchesAny(app, group.Apps))
                    return "group:" + index.ToString("D6");
                index++;
            }
            return "app:" + normalized;
        }
    }

    // Total, deterministic comparator. Explicit family priorities lead; unknown families
    // stay ordered by first observation, and Hybrid/Separate windows retain individual slots.
    internal sealed class TaskAffinityComparer : IComparer
    {
        private readonly Dictionary<IntPtr, long> _original;
        private readonly Dictionary<string, long> _bucketRanks = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TaskAffinityGroupRule> _groups;
        private readonly DisplayTaskbarProfile _profile;

        public TaskAffinityComparer(IEnumerable<TaskAffinityGroupRule> groups,
            Dictionary<IntPtr, long> original, IEnumerable<ApplicationWindow> windows,
            DisplayTaskbarProfile profile)
        {
            _original = original;
            _profile = profile;
            _groups = groups?.Where(g => g != null).Select(g => g.Clone()).ToList() ?? new();
            foreach (var window in windows ?? Enumerable.Empty<ApplicationWindow>())
            {
                string bucket = Bucket(window);
                long order = Original(window);
                if (!_bucketRanks.TryGetValue(bucket, out long first) || first > order)
                    _bucketRanks[bucket] = order;
            }
        }

        private long Original(ApplicationWindow w) =>
            _original.TryGetValue(w.Handle, out long index) ? index : long.MaxValue;

        private string Bucket(ApplicationWindow w)
        {
            string app = TaskButtonRules.AppKey(w);
            if (string.IsNullOrWhiteSpace(app)) return "single:" + w.Handle.ToInt64();
            if (TaskAffinityRules.PresentationFor(app, _profile) == TaskApplicationPresentation.Separate)
                return "single:" + w.Handle.ToInt64();
            return TaskAffinityRules.BucketFor(app, _groups);
        }

        public int Compare(object x, object y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is not ApplicationWindow a || y is not ApplicationWindow b) return 0;
            if (a.Handle == b.Handle) return 0;
            string ak = Bucket(a), bk = Bucket(b);
            if (!string.Equals(ak, bk, StringComparison.OrdinalIgnoreCase))
            {
                bool ag = ak.StartsWith("group:", StringComparison.Ordinal);
                bool bg = bk.StartsWith("group:", StringComparison.Ordinal);
                if (ag && bg) return string.Compare(ak, bk, StringComparison.Ordinal);
                if (ag) return -1;
                if (bg) return 1;
                long ar = _bucketRanks.TryGetValue(ak, out var av) ? av : long.MaxValue;
                long br = _bucketRanks.TryGetValue(bk, out var bv) ? bv : long.MaxValue;
                int byFirstSeen = ar.CompareTo(br);
                if (byFirstSeen != 0) return byFirstSeen;
                return string.Compare(ak, bk, StringComparison.OrdinalIgnoreCase);
            }
            int byWindowOrder = Original(a).CompareTo(Original(b));
            return byWindowOrder != 0 ? byWindowOrder : a.Handle.ToInt64().CompareTo(b.Handle.ToInt64());
        }
    }
}
