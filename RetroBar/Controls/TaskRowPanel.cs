using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ManagedShell.WindowsTasks;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the task list's panel. Without row rules it is exactly a WrapPanel. With them (on a
    // taskbar along the top or bottom of the screen, which wraps its buttons in rows) it places each
    // button in a row of its own choosing:
    //  - an app named in a row rule goes to that row; with "only these apps", nothing else may use it;
    //  - apps in the "move to the end" list are placed after everything else, optionally in the lowest row;
    //  - everything else fills the rows as the wrapping panel would: the first allowed row it fits in.
    public class TaskRowPanel : WrapPanel
    {
        private Rect[] _slots = Array.Empty<Rect>();
        private bool _custom;

        public TaskRowPanel()
        {
            Loaded += (s, e) => Settings.Instance.PropertyChanged += Settings_PropertyChanged;
            Unloaded += (s, e) => Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.TaskRowRules) || e.PropertyName == nameof(Settings.TaskEndApps) ||
                e.PropertyName == nameof(Settings.TaskEndAppsBottomRow) || e.PropertyName == nameof(Settings.RowCount))
            {
                InvalidateMeasure();
            }
        }

        private static bool HasRules =>
            (Settings.Instance.TaskRowRules?.Any(r => !string.IsNullOrWhiteSpace(r.Apps) || r.OnlyListed || r.Align != TaskButtonAlignOption.Left || r.HeightAdjust != 0) ?? false) ||
            !string.IsNullOrWhiteSpace(Settings.Instance.TaskEndApps);

        protected override Size MeasureOverride(Size constraint)
        {
            _custom = Orientation == Orientation.Horizontal && HasRules && !double.IsInfinity(constraint.Width);
            if (!_custom)
            {
                return base.MeasureOverride(constraint);
            }

            var unlimited = new Size(double.PositiveInfinity, double.PositiveInfinity);
            UIElement[] children = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
            foreach (UIElement child in children)
            {
                child.Measure(unlimited);
            }

            double rowHeight = children.Length == 0 ? 0 : children.Max(c => c.DesiredSize.Height);
            if (rowHeight <= 0)
            {
                _slots = Array.Empty<Rect>();
                return new Size(0, 0);
            }

            int rows = Window.GetWindow(this) is Taskbar bar ? Math.Max(1, bar.Rows)
                : double.IsInfinity(constraint.Height) ? Math.Max(1, Settings.Instance.RowCount) : Math.Max(1, (int)Math.Floor(constraint.Height / rowHeight + 1e-6));
            _slots = Place(children, rows, rowHeight, constraint.Width);
            double width = _slots.Length == 0 ? 0 : _slots.Max(r => r.Right);
            return new Size(width, _slots.Length == 0 ? rows * rowHeight : Math.Max(rows * rowHeight, _totalHeight));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (!_custom)
            {
                return base.ArrangeOverride(finalSize);
            }

            UIElement[] children = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
            for (int i = 0; i < children.Length && i < _slots.Length; i++)
            {
                children[i].Arrange(_slots[i]);
            }
            return finalSize;
        }

        private double _totalHeight;

        private Rect[] Place(UIElement[] children, int rows, double rowHeight, double rowWidth)
        {
            Settings settings = Settings.Instance;
            var rules = (settings.TaskRowRules ?? new List<TaskRowRule>())
                .Select(r => (row: r.Row > 0 ? r.Row - 1 : rows + r.Row, rule: r))
                .Where(r => r.row >= 0 && r.row < rows)
                .ToList();
            var endApps = Split(settings.TaskEndApps);
            var assigned = rules.Select(r => (r.row, apps: Split(r.rule.Apps), only: r.rule.OnlyListed)).ToList();

            // Each row's own height (the base height plus the row's adjustment) and where it starts.
            var heights = Enumerable.Repeat(rowHeight, rows).ToArray();
            foreach (var (adjustRow, adjustRule) in rules)
            {
                heights[adjustRow] = Math.Max(8, rowHeight + adjustRule.HeightAdjust);
            }
            var tops = new double[rows];
            for (int r = 1; r < rows; r++)
            {
                tops[r] = tops[r - 1] + heights[r - 1];
            }
            _totalHeight = tops[rows - 1] + heights[rows - 1];

            var slots = new Rect[children.Length];
            var rowX = new double[rows];
            var keys = children.Select(KeyOf).ToArray();

            IEnumerable<int> order = Enumerable.Range(0, children.Length).Where(i => !Matches(keys[i], endApps))
                .Concat(Enumerable.Range(0, children.Length).Where(i => Matches(keys[i], endApps)));

            foreach (int i in order)
            {
                bool atEnd = Matches(keys[i], endApps);
                var allowed = assigned.Where(a => Matches(keys[i], a.apps)).Select(a => a.row).Distinct().ToList();
                if (allowed.Count == 0)
                {
                    var reserved = new HashSet<int>(assigned.Where(a => a.only).Select(a => a.row));
                    allowed = Enumerable.Range(0, rows).Where(r => !reserved.Contains(r)).ToList();
                    if (allowed.Count == 0)
                    {
                        allowed = Enumerable.Range(0, rows).ToList(); // every row reserved: no restriction left
                    }
                }

                if (atEnd && settings.TaskEndAppsBottomRow)
                {
                    allowed = new List<int> { allowed.Max() };
                }

                // The first allowed row (from the top) it fits in; if none has room, the shortest.
                double width = children[i].DesiredSize.Width;
                allowed.Sort();
                int row = allowed.FirstOrDefault(r => rowX[r] + width <= rowWidth + 0.5, -1);
                if (row < 0)
                {
                    row = allowed.OrderBy(r => rowX[r]).First();
                }
                slots[i] = new Rect(rowX[row], tops[row], width, heights[row]);
                rowX[row] += width;
            }

            // Row alignment: slide each row's buttons left, to the centre, or to the right.
            foreach (var (row, rule) in rules)
            {
                if (rule.Align == TaskButtonAlignOption.Left)
                {
                    continue;
                }

                double spare = Math.Max(0, rowWidth - rowX[row]);
                double shift = rule.Align == TaskButtonAlignOption.Centre ? Math.Floor(spare / 2) : spare;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (Math.Abs(slots[i].Y - tops[row]) < 0.5)
                    {
                        slots[i].X += shift;
                    }
                }
            }

            return slots;
        }

        // The app a button stands for: its exe name (lower case, no extension), and for Store apps its AppUserModelID.
        private static (string stem, string aumid) KeyOf(UIElement child)
        {
            if (!(child is FrameworkElement element) || !(element.DataContext is ApplicationWindow window))
            {
                return ("", "");
            }

            string file = TaskButtonRules.AppKey(window) ?? "";
            string stem = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            return (stem, (window.AppUserModelID ?? "").ToLowerInvariant());
        }

        public static List<string> Split(string text) =>
            (text ?? "").Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => System.IO.Path.GetFileNameWithoutExtension(t.Trim()).ToLowerInvariant())
                .Where(t => t.Length > 0)
                .ToList();

        // An entry matches an app by its exe name (".exe" optional), by the start of it when the entry is
        // four letters or more ("calc" finds CalculatorApp), or by part of a Store app's ID.
        private static bool Matches((string stem, string aumid) key, List<string> entries) =>
            entries.Any(e => key.stem == e || (e.Length >= 4 && key.stem.StartsWith(e)) || (e.Length >= 4 && key.aumid.Contains(e)));
    }
}
