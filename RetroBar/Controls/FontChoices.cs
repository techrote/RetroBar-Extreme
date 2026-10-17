using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme (UX-017): the font lists on the Style and Layout tabs. The built-in fonts come
    // first, each shown in its own face, then a separator and the installed fonts.
    public static class FontChoices
    {
        private static List<string> _installed;

        private static List<string> Installed => _installed ??= System.Windows.Media.Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !BundledFonts.IsBundled(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        // Fills the list; the first entry (Theme default / Same as taskbar) stands for "".
        public static void Fill(ComboBox combo, object firstItem, string selected)
        {
            combo.Items.Clear();
            combo.Items.Add(new ComboBoxItem { Content = firstItem, Tag = "" });
            foreach (string name in BundledFonts.Names)
            {
                combo.Items.Add(new ComboBoxItem { Content = name, Tag = name, FontFamily = BundledFonts.Resolve(name, 12) });
            }
            combo.Items.Add(new Separator());
            foreach (string name in Installed)
            {
                combo.Items.Add(new ComboBoxItem { Content = name, Tag = name });
            }

            string want = BundledFonts.Canonical(selected) ?? selected;
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == (want ?? "")) ?? combo.Items[0];
        }

        // The chosen family name, "" for the first entry, or null while nothing is selected.
        public static string Selected(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag as string;
    }
}
