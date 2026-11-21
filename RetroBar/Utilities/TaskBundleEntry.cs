using ManagedShell.WindowsTasks;
using System.Collections.Generic;
using System.Linq;

namespace RetroBar.Utilities
{
    // A projection of real windows. Never closes, merges or modifies their native HWND state.
    public sealed class TaskBundleEntry
    {
        public string Key { get; set; } = "";
        public bool CanBundle { get; set; }
        public TaskBundleExpansion Expansion { get; set; } = TaskBundleExpansion.Flyout;
        public List<ApplicationWindow> Windows { get; } = new();
        public ApplicationWindow Representative => Windows.FirstOrDefault();
        public ApplicationWindow SingleWindow => Representative;
        public bool IsBundle => CanBundle && Windows.Count > 1;
        public int WindowCount => Windows.Count;
        public string DisplayName => Representative == null ? "" :
            TaskButtonRules.AppName(Representative);
    }
}
