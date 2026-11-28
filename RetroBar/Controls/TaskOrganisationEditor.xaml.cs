using ManagedShell.AppBar;
using RetroBar.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RetroBar.Controls
{
    // Keeps one shared family catalogue and a configurable set of policy overrides per display.
    public partial class TaskOrganisationEditor : UserControl
    {
        private sealed class ScopeChoice
        {
            public string DeviceName { get; set; } = "";
            public override string ToString() => DeviceName.Length == 0 ? "Global defaults" : DeviceName;
        }

        private bool _loading, _initialized;
        private List<TaskAffinityGroupRule> _groups = new();
        private List<TaskApplicationPolicy> _policies = new();

        public TaskOrganisationEditor()
        {
            InitializeComponent();
            ModeCombo.ItemsSource = new[] { "Inherit global", "Separate", "Affinity", "Bundle", "Hybrid" };
            PresentationCombo.ItemsSource = Enum.GetValues(typeof(TaskApplicationPresentation));
            ExpansionCombo.ItemsSource = Enum.GetValues(typeof(TaskBundleExpansion));
            PresentationCombo.SelectedIndex = 1;
            ExpansionCombo.SelectedIndex = 0;
        }

        private void Editor_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;
            _loading = true;
            ScopeCombo.ItemsSource = new[] { new ScopeChoice() }
                .Concat(AppBarScreen.FromAllScreens().Select(s => new ScopeChoice { DeviceName = s.DeviceName })).ToList();
            ScopeCombo.SelectedIndex = 0;
            try
            {
                var apps = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var process in Process.GetProcesses())
                {
                    using (process)
                    {
                        try { if (!string.IsNullOrWhiteSpace(process.ProcessName)) apps.Add(process.ProcessName + ".exe"); }
                        catch (Exception) { }
                    }
                }
                RunningApps.ItemsSource = apps.ToList();
            }
            catch (Exception) { RunningApps.ItemsSource = new[] { "firefox.exe", "explorer.exe", "pwsh.exe" }; }
            _loading = false;
            LoadScope();
        }

        private string CurrentDevice => (ScopeCombo.SelectedItem as ScopeChoice)?.DeviceName ?? "";

        private void LoadScope()
        {
            _loading = true;
            _groups = (Settings.Instance.TaskAffinityGroups ?? new()).Where(g => g != null).Select(g => g.Clone()).ToList();
            DisplayTaskbarProfile display = DisplayTaskbarProfiles.Find(Settings.Instance.DisplayTaskbarProfiles, CurrentDevice);
            _policies = (CurrentDevice.Length == 0 ? Settings.Instance.TaskApplicationPolicies : display?.ApplicationOverrides)?
                .Where(p => p != null).Select(p => p.Clone()).ToList() ?? new();
            var mode = CurrentDevice.Length == 0
                ? (TaskArrangementMode?)Settings.Instance.TaskArrangement : display?.ArrangementOverride;
            ModeCombo.SelectedIndex = mode.HasValue ? (int)mode.Value + 1 : 0;
            ModeCombo.IsEnabled = true;
            ReloadGroups(0);
            ReloadPolicies(0);
            _loading = false;
        }

        private void Scope_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_loading && _initialized) LoadScope();
        }

        private void ReloadGroups(int selection)
        {
            GroupList.ItemsSource = null;
            GroupList.ItemsSource = _groups;
            GroupList.SelectedIndex = _groups.Count == 0 ? -1 : Math.Clamp(selection, 0, _groups.Count - 1);
        }

        private void ReloadPolicies(int selection)
        {
            PolicyList.ItemsSource = null;
            PolicyList.ItemsSource = _policies;
            PolicyList.SelectedIndex = _policies.Count == 0 ? -1 : Math.Clamp(selection, 0, _policies.Count - 1);
        }

        private void Group_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (GroupList.SelectedItem is TaskAffinityGroupRule g)
            {
                GroupName.Text = g.Name;
                GroupApps.Text = g.Apps;
            }
            else { GroupName.Text = ""; GroupApps.Text = ""; }
        }

        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            _groups.Add(new TaskAffinityGroupRule { Name = "New family", Apps = "" });
            ReloadGroups(_groups.Count - 1);
        }

        private void RemoveGroup_Click(object sender, RoutedEventArgs e)
        {
            int i = GroupList.SelectedIndex;
            if (i >= 0) { _groups.RemoveAt(i); ReloadGroups(i); }
        }

        private void MoveGroup(int offset)
        {
            int i = GroupList.SelectedIndex, target = i + offset;
            if (i < 0 || target < 0 || target >= _groups.Count) return;
            (_groups[i], _groups[target]) = (_groups[target], _groups[i]);
            ReloadGroups(target);
        }

        private void UpGroup_Click(object sender, RoutedEventArgs e) => MoveGroup(-1);
        private void DownGroup_Click(object sender, RoutedEventArgs e) => MoveGroup(1);

        private void SaveGroup_Click(object sender, RoutedEventArgs e)
        {
            int i = GroupList.SelectedIndex;
            if (i < 0) return;
            _groups[i] = new TaskAffinityGroupRule { Name = GroupName.Text.Trim(), Apps = GroupApps.Text.Trim() };
            ReloadGroups(i);
        }

        private void Policy_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PolicyList.SelectedItem is TaskApplicationPolicy p)
            {
                PolicyApps.Text = p.Apps;
                PresentationCombo.SelectedItem = p.Presentation;
                ExpansionCombo.SelectedItem = p.Expansion;
            }
            else { PolicyApps.Text = ""; PresentationCombo.SelectedIndex = 1; ExpansionCombo.SelectedIndex = 0; }
        }

        private void AddPolicy_Click(object sender, RoutedEventArgs e)
        {
            _policies.Add(new TaskApplicationPolicy { Apps = "firefox.exe" });
            ReloadPolicies(_policies.Count - 1);
        }

        private void RemovePolicy_Click(object sender, RoutedEventArgs e)
        {
            int i = PolicyList.SelectedIndex;
            if (i >= 0) { _policies.RemoveAt(i); ReloadPolicies(i); }
        }

        private void SavePolicy_Click(object sender, RoutedEventArgs e)
        {
            int i = PolicyList.SelectedIndex;
            if (i < 0) return;
            _policies[i] = new TaskApplicationPolicy
            {
                Apps = PolicyApps.Text.Trim(),
                Presentation = PresentationCombo.SelectedItem is TaskApplicationPresentation p ? p : TaskApplicationPresentation.Affinity,
                Expansion = ExpansionCombo.SelectedItem is TaskBundleExpansion x ? x : TaskBundleExpansion.Flyout
            };
            ReloadPolicies(i);
        }

        private void RunningApps_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (RunningApps.SelectedItem is string app) PolicyApps.Text = app;
        }

        private void Examples_Click(object sender, RoutedEventArgs e)
        {
            _groups = new List<TaskAffinityGroupRule> {
                new() { Name = "Terminals", Apps = "terminals" },
                new() { Name = "Firefox", Apps = "firefox.exe" },
                new() { Name = "File Explorer", Apps = "explorer.exe" }
            };
            ReloadGroups(0);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // A selected rule may still be in the editor: commit it before the global Apply.
            if (GroupList.SelectedIndex >= 0) SaveGroup_Click(sender, e);
            if (PolicyList.SelectedIndex >= 0) SavePolicy_Click(sender, e);
            Settings.Instance.TaskAffinityGroups = _groups.Where(g => !string.IsNullOrWhiteSpace(g.Apps)).Select(g => g.Clone()).ToList();
            if (CurrentDevice.Length == 0)
            {
                Settings.Instance.TaskApplicationPolicies = _policies.Where(p => !string.IsNullOrWhiteSpace(p.Apps)).Select(p => p.Clone()).ToList();
                Settings.Instance.TaskArrangement = ModeCombo.SelectedIndex < 1
                    ? TaskArrangementMode.Separate : (TaskArrangementMode)(ModeCombo.SelectedIndex - 1);
            }
            else
            {
                var screen = AppBarScreen.FromAllScreens().FirstOrDefault(s =>
                    string.Equals(s.DeviceName, CurrentDevice, StringComparison.OrdinalIgnoreCase));
                var profile = (DisplayTaskbarProfiles.Find(Settings.Instance.DisplayTaskbarProfiles, CurrentDevice) ??
                    new DisplayTaskbarProfile { DeviceName = CurrentDevice, Enabled = screen?.Primary == true || Settings.Instance.ShowMultiMon,
                        Edge = Settings.Instance.Edge }).Clone();
                profile.ArrangementOverride = ModeCombo.SelectedIndex < 1
                    ? null : (TaskArrangementMode?)(TaskArrangementMode)(ModeCombo.SelectedIndex - 1);
                profile.ApplicationOverrides = _policies.Where(p => !string.IsNullOrWhiteSpace(p.Apps)).Select(p => p.Clone()).ToList();
                Settings.Instance.SaveDisplayTaskbarProfile(profile);
            }
        }
    }
}
