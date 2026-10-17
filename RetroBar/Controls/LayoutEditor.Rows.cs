using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the Layout tab's row rules for the task list. Pick a row (1 = top ... or the lowest),
    // list the apps that belong there and whether only they may use it, and list the apps that go to
    // the end of the list. Rules save as you type.
    public partial class LayoutEditor
    {
        private const int LastRowChoice = 6; // index of "Lowest row" in the picker (rows 1-6, then lowest)
        private bool _loadingRow;

        private int PickedRow => cboRowPick.SelectedIndex < 0 ? 1 : cboRowPick.SelectedIndex == LastRowChoice ? -1 : cboRowPick.SelectedIndex + 1;

        private void LoadRows()
        {
            _loadingRow = true;
            if (cboRowPick.SelectedIndex < 0)
            {
                cboRowPick.SelectedIndex = 0;
            }
            ShowRow();
            txtEndApps.Text = Settings.Instance.TaskEndApps ?? "";
            _loadingRow = false;
        }

        private void ShowRow()
        {
            TaskRowRule rule = (Settings.Instance.TaskRowRules ?? new List<TaskRowRule>()).FirstOrDefault(r => r.Row == PickedRow);
            txtRowApps.Text = rule?.Apps ?? "";
            cboRowAlign.SelectedIndex = (int)(rule?.Align ?? TaskButtonAlignOption.Left);
            sldRowHeight.Value = rule?.HeightAdjust ?? 0;
            txtRowHeight.Text = ((int)Math.Round(sldRowHeight.Value)).ToString();
            chkRowOnly.IsChecked = rule?.OnlyListed ?? false;
        }

        private void RowPick_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || !_ready || _loadingRow)
            {
                return;
            }

            _loadingRow = true;
            ShowRow();
            _loadingRow = false;
        }

        private void RowRule_Changed(object sender, TextChangedEventArgs e) => SaveRow();

        private void RowHeight_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_ready || txtRowHeight == null)
            {
                return;
            }

            txtRowHeight.Text = ((int)Math.Round(sldRowHeight.Value)).ToString();
            SaveRow();
        }

        private void RowAlign_SelectionChanged(object sender, SelectionChangedEventArgs e) => SaveRow();

        private void RowRule_Clicked(object sender, RoutedEventArgs e) => SaveRow();

        private void SaveRow()
        {
            if (_loading || !_ready || _loadingRow)
            {
                return;
            }

            int row = PickedRow;
            var rules = (Settings.Instance.TaskRowRules ?? new List<TaskRowRule>()).Select(r => r.Clone()).Where(r => r.Row != row).ToList();
            string apps = txtRowApps.Text.Trim();
            bool only = chkRowOnly.IsChecked == true;
            var align = (TaskButtonAlignOption)Math.Max(0, cboRowAlign.SelectedIndex);
            double heightAdjust = Math.Round(sldRowHeight.Value);
            if (apps.Length > 0 || only || align != TaskButtonAlignOption.Left || heightAdjust != 0)
            {
                rules.Add(new TaskRowRule { Row = row, Apps = apps, OnlyListed = only, Align = align, HeightAdjust = heightAdjust });
            }
            Settings.Instance.TaskRowRules = rules.OrderBy(r => r.Row < 0 ? int.MaxValue : r.Row).ToList();
        }

        private void EndApps_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading || !_ready || _loadingRow)
            {
                return;
            }

            Settings.Instance.TaskEndApps = txtEndApps.Text.Trim();
        }
    }
}
