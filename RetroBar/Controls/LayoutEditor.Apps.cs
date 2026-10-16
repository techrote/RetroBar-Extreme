using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-014): the Layout tab's task button section. All-apps width / icons /
    // labels, plus the remembered per-app settings (added from a task button's right-click
    // Button menu) with width, icon and label for the chosen app, and Forget.
    public partial class LayoutEditor
    {
        // App width list: Default, Icon only, Narrow, Wide, Custom
        private const int APP_DEFAULT = 0, APP_ICON = 1, APP_NARROW = 2, APP_WIDE = 3, APP_CUSTOM = 4;

        private bool _updatingApp;
        private bool _watchingRules, _visibilityHooked;

        private TaskButtonAppRule SelectedApp => cboApp.SelectedItem as TaskButtonAppRule;

        private void LoadApps()
        {
            if (!_watchingRules)
            {
                Settings.Instance.PropertyChanged += Rules_PropertyChanged;
                if (!_visibilityHooked)
                {
                    _visibilityHooked = true;
                    IsVisibleChanged += (s, e) =>
                    {
                        if (!IsVisible && _watchingRules)
                        {
                            Settings.Instance.PropertyChanged -= Rules_PropertyChanged;
                            _watchingRules = false;
                        }
                    };
                }
                _watchingRules = true;
            }

            string selected = SelectedApp?.App;
            var rules = (Settings.Instance.TaskButtonAppRules ?? new System.Collections.Generic.List<TaskButtonAppRule>())
                .Select(r => r.Clone())
                .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _updatingApp = true;
            cboApp.ItemsSource = rules;
            cboApp.SelectedItem = rules.FirstOrDefault(r => r.App == selected) ?? rules.FirstOrDefault();
            _updatingApp = false;

            bool any = rules.Count > 0;
            cboApp.IsEnabled = any;
            panelApp.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            ShowApp(SelectedApp);
        }

        // The rules also change from a task button's right-click menu while this tab is open.
        private void Rules_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.TaskButtonAppRules) && !_updatingApp)
            {
                Dispatcher.BeginInvoke(new Action(LoadApps));
            }
        }

        private void ShowApp(TaskButtonAppRule rule)
        {
            if (rule == null)
            {
                return;
            }

            _updatingApp = true;
            cboAppWidth.SelectedIndex = rule.Width == 0 ? APP_DEFAULT
                : rule.Width == TaskButtonRules.IconOnly ? APP_ICON
                : rule.Width == TaskButtonRules.NarrowWidth ? APP_NARROW
                : rule.Width == TaskButtonRules.WideWidth ? APP_WIDE
                : APP_CUSTOM;
            sldAppWidth.Value = rule.Width > 0 ? rule.Width : 160;
            cboAppIcon.SelectedIndex = (int)rule.Icon;
            cboAppLabel.SelectedIndex = (int)rule.Label;
            _updatingApp = false;
            UpdateTexts();
        }

        private void CboApp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_updatingApp)
            {
                ShowApp(SelectedApp);
            }
        }

        private void AppCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingApp || _loading || SelectedApp == null ||
                cboAppWidth.SelectedIndex < 0 || cboAppIcon.SelectedIndex < 0 || cboAppLabel.SelectedIndex < 0)
            {
                return;
            }

            SaveApp();
        }

        private void SldAppWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_updatingApp && !_loading && SelectedApp != null && cboAppWidth.SelectedIndex == APP_CUSTOM)
            {
                SaveApp();
            }
            UpdateTexts();
        }

        private void SldTaskWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_loading && cboTaskWidth != null && cboTaskWidth.SelectedIndex == 1)
            {
                Settings.Instance.TaskButtonWidthPx = Math.Round(sldTaskWidth.Value);
            }
            UpdateTexts();
        }

        private void SaveApp()
        {
            TaskButtonAppRule selected = SelectedApp;
            double width = cboAppWidth.SelectedIndex switch
            {
                APP_ICON => TaskButtonRules.IconOnly,
                APP_NARROW => TaskButtonRules.NarrowWidth,
                APP_WIDE => TaskButtonRules.WideWidth,
                APP_CUSTOM => Math.Round(sldAppWidth.Value),
                _ => 0,
            };
            var icon = (AppOverrideOption)cboAppIcon.SelectedIndex;
            var label = (AppOverrideOption)cboAppLabel.SelectedIndex;

            _updatingApp = true;
            TaskButtonRules.Update(selected.App, null, r =>
            {
                r.Width = width;
                r.Icon = icon;
                r.Label = label;
            });
            _updatingApp = false;

            // Setting everything back to Default forgets the app.
            if (TaskButtonRules.Find(selected.App) == null)
            {
                LoadApps();
            }
            UpdateTexts();
        }

        private void ForgetApp_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedApp != null)
            {
                _updatingApp = true;
                TaskButtonRules.Forget(SelectedApp.App);
                _updatingApp = false;
                LoadApps();
            }
        }
    }
}
