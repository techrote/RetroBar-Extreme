using System;
using System.Collections.Generic;
using System.Linq;

namespace RetroBar.Utilities
{
    // All indices refer to the WindowManager's current live taskbar list.
    // Eligible means an actually rendered, visible rectangle target exists
    // for this *specific* ApplicationWindow, not merely that a profile allows it.
    public readonly record struct WindowRectangleCandidate(
        int Index, string DeviceName, IntPtr Monitor, bool ShellHost, bool Eligible);

    public static class WindowRectangleOwnership
    {
        public static int Select(IntPtr windowMonitor, IEnumerable<WindowRectangleCandidate> candidates)
        {
            return (candidates ?? Enumerable.Empty<WindowRectangleCandidate>())
                .Where(candidate => candidate.Eligible)
                // The physically local taskbar wins when it actually shows the
                // window; otherwise choose an eligible shell host, then a
                // deterministic connected display. Event subscription order is irrelevant.
                .OrderByDescending(candidate => windowMonitor != IntPtr.Zero &&
                                                candidate.Monitor == windowMonitor)
                .ThenByDescending(candidate => candidate.ShellHost)
                .ThenBy(candidate => candidate.DeviceName ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(candidate => candidate.Index)
                .Select(candidate => candidate.Index)
                .DefaultIfEmpty(-1)
                .First();
        }
    }
}
