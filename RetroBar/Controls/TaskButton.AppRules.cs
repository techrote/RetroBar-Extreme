using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-014): per-app task button settings. Icon and label visibility come from
    // the all-apps settings or the app's remembered rule; widths are applied by the task list
    // (TaskButtonWidthConverter). The right-click menu gets a "Button" submenu to set this app's
    // width, icon and label; the Layout tab lists and edits every remembered app.
    public partial class TaskButton
    {
        private MenuItem _appButtonMenu;

        private void ApplyAppRule()
        {
            TaskButtonAppRule rule = TaskButtonRules.Find(Window);
            TaskIconImage.Visibility = TaskButtonRules.ShowIcon(rule) ? Visibility.Visible : Visibility.Collapsed;
            TaskLabelText.Visibility = TaskButtonRules.ShowLabel(rule) ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool IsAppRuleSetting(string name) =>
            name == nameof(Settings.TaskButtonAppRules) ||
            name == nameof(Settings.TaskButtonShowIcons) ||
            name == nameof(Settings.TaskButtonShowLabels);

        private string MenuText(string key) => TryFindResource(key) as string ?? key;

        // Adds (once) and refreshes the "Button" submenu, just above End task / Close.
        private void UpdateAppButtonMenu()
        {
            string app = TaskButtonRules.AppKey(Window);
            if (AppButton.ContextMenu == null)
            {
                return;
            }

            if (_appButtonMenu == null)
            {
                _appButtonMenu = new MenuItem();
                _appButtonMenu.SetResourceReference(HeaderedItemsControl.HeaderProperty, "uxui_taskbutton_menu");
                var items = AppButton.ContextMenu.Items;
                int separator = items.Cast<object>().ToList().FindIndex(i => i is Separator);
                int at = separator >= 0 ? separator + 1 : items.Count;
                items.Insert(at, _appButtonMenu);
                items.Insert(at + 1, new Separator());
            }

            _appButtonMenu.IsEnabled = app != null;
            _appButtonMenu.Items.Clear();
            if (app == null)
            {
                return;
            }

            string name = TaskButtonRules.AppName(Window);
            TaskButtonAppRule rule = TaskButtonRules.Find(app) ?? new TaskButtonAppRule();

            var width = new MenuItem();
            width.SetResourceReference(HeaderedItemsControl.HeaderProperty, "uxui_taskbutton_width");
            width.Items.Add(Choice("uxui_taskbutton_width_default", rule.Width == 0, () => TaskButtonRules.Update(app, name, r => r.Width = 0)));
            width.Items.Add(Choice("uxui_taskbutton_width_icon", rule.Width == TaskButtonRules.IconOnly, () => TaskButtonRules.Update(app, name, r => r.Width = TaskButtonRules.IconOnly)));
            width.Items.Add(Choice("uxui_taskbutton_width_narrow", rule.Width == TaskButtonRules.NarrowWidth, () => TaskButtonRules.Update(app, name, r => r.Width = TaskButtonRules.NarrowWidth)));
            width.Items.Add(Choice("uxui_taskbutton_width_wide", rule.Width == TaskButtonRules.WideWidth, () => TaskButtonRules.Update(app, name, r => r.Width = TaskButtonRules.WideWidth)));
            bool custom = rule.Width > 0 && rule.Width != TaskButtonRules.NarrowWidth && rule.Width != TaskButtonRules.WideWidth;
            if (custom)
            {
                width.Items.Add(new MenuItem { Header = $"{rule.Width:0} px", IsCheckable = true, IsChecked = true, IsEnabled = false });
            }
            _appButtonMenu.Items.Add(width);

            // Toggles set an explicit per-app value (the opposite of what is shown now).
            bool iconShown = TaskButtonRules.ShowIcon(rule);
            var icon = new MenuItem { IsCheckable = true, IsChecked = iconShown };
            icon.SetResourceReference(HeaderedItemsControl.HeaderProperty, "uxui_taskbutton_show_icon");
            icon.Click += (s, e) => TaskButtonRules.Update(app, name, r => r.Icon = iconShown ? AppOverrideOption.Hide : AppOverrideOption.Show);
            _appButtonMenu.Items.Add(icon);

            bool labelShown = TaskButtonRules.ShowLabel(rule);
            var label = new MenuItem { IsCheckable = true, IsChecked = labelShown };
            label.SetResourceReference(HeaderedItemsControl.HeaderProperty, "uxui_taskbutton_show_label");
            label.Click += (s, e) => TaskButtonRules.Update(app, name, r => r.Label = labelShown ? AppOverrideOption.Hide : AppOverrideOption.Show);
            _appButtonMenu.Items.Add(label);

            _appButtonMenu.Items.Add(new Separator());
            var reset = new MenuItem { IsEnabled = TaskButtonRules.Find(app) != null };
            reset.SetResourceReference(HeaderedItemsControl.HeaderProperty, "uxui_taskbutton_reset");
            reset.Click += (s, e) => TaskButtonRules.Forget(app);
            _appButtonMenu.Items.Add(reset);
        }

        private MenuItem Choice(string key, bool selected, System.Action choose)
        {
            var item = new MenuItem { IsCheckable = true, IsChecked = selected };
            item.SetResourceReference(HeaderedItemsControl.HeaderProperty, key);
            item.Click += (s, e) => choose();
            return item;
        }
    }
}
