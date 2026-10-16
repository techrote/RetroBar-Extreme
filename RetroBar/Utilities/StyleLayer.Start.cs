using System;
using System.Windows;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-010): lookup original theme resources, even while a preview is active.
    public static partial class StyleLayer
    {
        public static object ThemeResource(string key) => FindThemeResource(Application.Current?.Resources, _dictionary, key);

        public static object FindThemeResource(ResourceDictionary resources, ResourceDictionary excluded, string key)
        {
            if (resources == null || ReferenceEquals(resources, excluded)) return null;
            // Direct entries take precedence over merged dictionaries, exactly as in WPF.
            foreach (object entry in resources.Keys)
                if (Equals(entry, key)) return resources[key];
            for (int i = resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                object found = FindThemeResource(resources.MergedDictionaries[i], excluded, key);
                if (found != null) return found;
            }
            return null;
        }
    }
}
