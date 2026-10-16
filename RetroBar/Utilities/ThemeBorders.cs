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

        // Every border by the same factor.
        public static string ScaleBorders(string xaml, double factor) => ScaleBorders(xaml, part => factor);

        // UX-013: per part. factorFor gets a style-editor part name (bar, tray, button, buttonActive,
        // buttonFlashing, clock, start) and returns its multiplier; borders in theme styles that
        // belong to no part keep the theme's width.
        public static string ScaleBorders(string xaml, Func<string, double> factorFor)
        {
            XDocument doc = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);

            foreach (XElement element in doc.Descendants().ToList())
            {
                string name = element.Name.LocalName;

                if (name == "Setter")
                {
                    if (ThemeScaler.PropertyName(element) == "BorderThickness" && FactorFor(element, factorFor) is double factor && factor != 1)
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
                    if (!(FactorFor(element, factorFor) is double factor) || factor == 1)
                    {
                        continue;
                    }
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
                if (border != null && FactorFor(element, factorFor) is double borderFactor && borderFactor != 1 &&
                    ThemeScaler.TryScaleList(border.Value, borderFactor, out string scaledBorder))
                {
                    border.Value = scaledBorder;
                }
            }

            return doc.ToString(SaveOptions.DisableFormatting);
        }

        // Theme style keys to style-editor parts, most specific first.
        private static readonly (string prefix, string part)[] KeyParts =
        {
            ("TaskButtonActive", "buttonActive"),
            ("TaskButtonFlashing", "buttonFlashing"),
            ("TaskButton", "button"),
            ("TaskbarWindow", "bar"),
            ("Taskbar", "bar"),
            ("TrayToggle", "tray"),
            ("Tray", "tray"),
            ("StartButton", "start"),
            ("Clock", "clock"),
        };

        public static string PartForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            foreach (var (prefix, part) in KeyParts)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return part;
                }
            }
            return null;
        }

        // The multiplier for a border: from the nearest keyed style / template / resource around it.
        private static double? FactorFor(XElement element, Func<string, double> factorFor)
        {
            XName keyName = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
            foreach (XElement e in element.AncestorsAndSelf())
            {
                string key = (string)e.Attribute(keyName);
                if (key != null)
                {
                    string part = PartForKey(key);
                    return part == null ? (double?)null : factorFor(part);
                }
            }
            return null;
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
