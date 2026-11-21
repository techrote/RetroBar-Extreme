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

    // Total, deterministic comparer: every window remains a separate element in Affinity mode.
    // Never return a nonzero comparison for the same window, unlike a common upstream prototype.
    internal sealed class TaskAffinityComparer : IComparer
    {
        private readonly Dictionary<IntPtr, long> _order;
        private readonly Dictionary<string, int> _bucketOrder = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TaskAffinityGroupRule> _groups;

        public TaskAffinityComparer(IEnumerable<TaskAffinityGroupRule> groups, Dictionary<IntPtr, long> windowOrder)
        {
            _order = windowOrder;
            _groups = groups?.Where(g => g != null).Select(g => g.Clone()).ToList() ?? new();
        }

        private string Bucket(ApplicationWindow w) =>
            TaskAffinityRules.BucketFor(TaskButtonRules.AppKey(w) ?? ("window:" + w.Handle), _groups);

        private long Rank(ApplicationWindow w)
        {
            string bucket = Bucket(w);
            if (bucket.StartsWith("group:", StringComparison.Ordinal))
                return int.Parse(bucket.Substring(6));
            // Unlisted applications keep first-seen affinity groups rather than jumping alphabetically.
            long rank = long.MaxValue;
            foreach (var entry in _order)
            {
                // Rank of unlisted buckets is assigned in OrderFor below; this is a fallback only.
                if (entry.Key == w.Handle) rank = entry.Value;
            }
            return 1000000 + rank;
        }

        public int Compare(object x, object y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is not ApplicationWindow a || y is not ApplicationWindow b) return 0;
            if (a.Handle == b.Handle) return 0;
            string ak = Bucket(a), bk = Bucket(b);
            if (!string.Equals(ak, bk, StringComparison.OrdinalIgnoreCase))
            {
                bool ar = ak.StartsWith("group:", StringComparison.Ordinal);
                bool br = bk.StartsWith("group:", StringComparison.Ordinal);
                if (ar && br) return string.Compare(ak, bk, StringComparison.Ordinal);
                if (ar) return -1;
                if (br) return 1;
                // Unlisted application affinity is lexically deterministic until a first-seen
                // rank is persisted for the bucket.
                return string.Compare(ak, bk, StringComparison.OrdinalIgnoreCase);
            }
            long oa = _order.TryGetValue(a.Handle, out var va) ? va : long.MaxValue;
            long ob = _order.TryGetValue(b.Handle, out var vb) ? vb : long.MaxValue;
            int cmp = oa.CompareTo(ob);
            return cmp != 0 ? cmp : a.Handle.ToInt64().CompareTo(b.Handle.ToInt64());
        }
    }
}
