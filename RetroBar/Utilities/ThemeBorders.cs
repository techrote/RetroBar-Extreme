using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-013): theme XAML rewrites that sit alongside ThemeScaler (UX-006).
    //
    // - ScaleBorders multiplies every border width in a theme (BorderThickness attributes and
    //   setters, and Thickness resources used for borders) by a whole number, so lines stay
    //   crisp: ×0 removes borders, ×2 doubles them.
    // - InlineIncluded replaces a theme's included base themes (<ResourceDictionary
    //   Source="Windows XP Blue.xaml"/>) with their XAML, so rewrites reach the base theme's
    //   templates too (XP Royale, Olive Green, Silver, Zune... are built on XP Blue).
    public static class ThemeBorders
    {
        private const int MaxIncludeDepth = 4;

        public static string ScaleBorders(string xaml, double factor)
        {
            XDocument doc = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);

            foreach (XElement element in doc.Descendants().ToList())
            {
                string name = element.Name.LocalName;

                if (name == "Setter")
                {
                    if (ThemeScaler.PropertyName(element) == "BorderThickness")
                    {
                        XAttribute value = element.Attribute("Value");
                        if (value != null && ThemeScaler.TryScaleList(value.Value, factor, out string scaled))
                        {
                            value.Value = scaled;
                        }
                    }
                    continue;
                }

                if (name == "Thickness" && IsBorderThickness(element))
                {
                    foreach (XAttribute side in element.Attributes().Where(a => a.Name.Namespace == XNamespace.None &&
                             (a.Name.LocalName == "Left" || a.Name.LocalName == "Top" || a.Name.LocalName == "Right" || a.Name.LocalName == "Bottom")))
                    {
                        if (ThemeScaler.TryScaleList(side.Value, factor, out string scaledSide))
                        {
                            side.Value = scaledSide;
                        }
                    }
                    if (!element.HasElements && ThemeScaler.TryScaleList(element.Value, factor, out string scaledValue))
                    {
                        element.Value = scaledValue;
                    }
                    continue;
                }

                XAttribute border = element.Attribute("BorderThickness");
                if (border != null && ThemeScaler.TryScaleList(border.Value, factor, out string scaledBorder))
                {
                    border.Value = scaledBorder;
                }
            }

            return doc.ToString(SaveOptions.DisableFormatting);
        }

        // A <Thickness> that is a border: inside a BorderThickness setter, or keyed as one
        // (e.g. TrayToggleOuterBorderThickness).
        private static bool IsBorderThickness(XElement thickness)
        {
            XElement setter = thickness.Ancestors().FirstOrDefault(a => a.Name.LocalName == "Setter");
            if (setter != null)
            {
                return ThemeScaler.PropertyName(setter) == "BorderThickness";
            }

            string key = (string)thickness.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"));
            return key != null && key.IndexOf("Border", StringComparison.Ordinal) >= 0;
        }

        // folder: the folder the theme file is in; included themes are looked up next to it.
        public static string InlineIncluded(string xaml, string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return xaml;
            }

            XDocument doc = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            Inline(doc.Root, folder, 0);
            return doc.ToString(SaveOptions.DisableFormatting);
        }

        private static void Inline(XElement root, string folder, int depth)
        {
            var includes = root.Descendants()
                .Where(e => e.Name.LocalName == "ResourceDictionary" && e.Attribute("Source") != null &&
                            e.Parent?.Name.LocalName == "ResourceDictionary.MergedDictionaries")
                .ToList();

            foreach (XElement include in includes)
            {
                string source = (string)include.Attribute("Source");
                if (depth >= MaxIncludeDepth || source.Contains(':') || source.Contains('/') || source.Contains('\\') ||
                    !source.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // only sibling theme files; anything else loads as before
                }

                string path = Path.Combine(folder, source);
                if (!File.Exists(path))
                {
                    continue;
                }

                XElement included = XDocument.Parse(File.ReadAllText(path), LoadOptions.PreserveWhitespace).Root;
                if (included == null || included.Name.LocalName != "ResourceDictionary")
                {
                    continue;
                }

                Inline(included, folder, depth + 1);
                include.ReplaceWith(included);
            }
        }
    }
}
