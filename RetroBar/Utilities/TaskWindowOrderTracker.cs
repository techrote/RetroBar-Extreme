using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroBar.Utilities
{
    // Tracks first-observed order of live window *objects*, not just their native
    // handles. Windows can recycle an HWND after the previous window is destroyed.
    public sealed class TaskWindowOrderTracker
    {
        private readonly Dictionary<IntPtr, (object Identity, long Order)> _live = new();
        private long _next;

        public int Count => _live.Count;

        public Dictionary<IntPtr, long> Reconcile(IEnumerable<(IntPtr Handle, object Identity)> windows)
        {
            var seen = new HashSet<IntPtr>();
            foreach (var (handle, identity) in windows ?? Enumerable.Empty<(IntPtr Handle, object Identity)>())
            {
                if (identity == null) continue;
                seen.Add(handle);
                if (!_live.TryGetValue(handle, out var old) || !ReferenceEquals(old.Identity, identity))
                    _live[handle] = (identity, ++_next);
            }

            foreach (var stale in _live.Keys.Where(handle => !seen.Contains(handle)).ToArray())
                _live.Remove(stale);

            return _live.ToDictionary(pair => pair.Key, pair => pair.Value.Order);
        }
    }
}
