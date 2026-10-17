using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-006): rewrites a theme's XAML text for native sizing.
    //
    // Multiplies size numbers by the scale and rounds each to a whole pixel, so the theme is
    // drawn at its real size instead of being stretched. Deliberately NOT scaled:
    //   - BorderThickness / StrokeThickness: 1px lines stay 1px, which keeps edges sharp;
    //   - FontSize: taskbar text is sized by TaskbarTextStyler, so menus/tooltips/Properties keep
    //     100% fonts as in stock RetroBar;
    //   - geometry (Path Data, Points), corner radii, opacities, anything that isn't a plain number.
    public static class ThemeScaler
    {
        private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";

        // Attributes / Setter properties whose numbers are sizes.
        private static readonly HashSet<string> SizeProperties = new HashSet<string>
        {
            "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "Margin", "Padding",
        };

        // Attributes of a <Thickness> element.
        private static readonly HashSet<string> ThicknessSides = new HashSet<string> { "Left", "Top", "Right", "Bottom" };

        // Keyed Double/Thickness resources that are sizes, e.g. TaskbarHeight, TaskButtonWidth, TaskButtonMargin.
        private static readonly Regex SizeKey = new Regex("(Height|Width|Size|Margin|Padding|Offset)$", RegexOptions.Compiled);
        private static readonly Regex NotSizeKey = new Regex("(Border|Stroke|Opacity|Radius|Font)", RegexOptions.Compiled);

        public static string Scale(string xaml, double scale)
        {
            XDocument doc = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);

            foreach (XElement element in doc.Descendants())
            {
                string name = element.Name.LocalName;

                if (name == "Setter")
                {
                    ScaleSetter(element, scale);
                    continue;
                }

                if (name == "Thickness")
                {
                    ScaleThicknessElement(element, scale);
                    continue;
                }

                if (name == "Double" && IsSizeKey(element))
                {
                    if (TryScaleList(element.Value, scale, out string scaled))
                    {
                        element.Value = scaled;
                    }
                    continue;
                }

                foreach (XAttribute attribute in element.Attributes())
                {
                    if (!attribute.IsNamespaceDeclaration &&
                        attribute.Name.Namespace == XNamespace.None &&
                        SizeProperties.Contains(attribute.Name.LocalName) &&
                        TryScaleList(attribute.Value, scale, out string scaled))
                    {
                        attribute.Value = scaled;
                    }
                }
            }

            return doc.ToString(SaveOptions.DisableFormatting);
        }

        private static void ScaleSetter(XElement setter, double scale)
        {
            string property = PropertyName(setter);

            if (property == null || !SizeProperties.Contains(property))
            {
                return;
            }

            XAttribute value = setter.Attribute("Value");

            if (value != null && TryScaleList(value.Value, scale, out string scaled))
            {
                value.Value = scaled;
            }
        }

        private static void ScaleThicknessElement(XElement thickness, double scale)
        {
            // Only scale a Thickness that is a margin/padding, never a border.
            XElement setter = thickness.Ancestors().FirstOrDefault(a => a.Name.LocalName == "Setter");

            if (setter != null)
            {
                string property = PropertyName(setter);
                if (property == null || !SizeProperties.Contains(property))
                {
                    return;
                }
            }
            else if (!IsSizeKey(thickness))
            {
                return;
            }

            foreach (XAttribute attribute in thickness.Attributes())
            {
                if (attribute.Name.Namespace == XNamespace.None &&
                    ThicknessSides.Contains(attribute.Name.LocalName) &&
                    TryScaleList(attribute.Value, scale, out string scaledSide))
                {
                    attribute.Value = scaledSide;
                }
            }

            if (!thickness.HasElements && TryScaleList(thickness.Value, scale, out string scaled))
            {
                thickness.Value = scaled;
            }
        }

        private static bool IsSizeKey(XElement element)
        {
            string key = (string)element.Attribute(XamlNs + "Key");
            return key != null && SizeKey.IsMatch(key) && !NotSizeKey.IsMatch(key);
        }

        internal static string PropertyName(XElement setter)
        {
            string property = (string)setter.Attribute("Property");

            if (string.IsNullOrEmpty(property))
            {
                return null;
            }

            // "Control.Padding" / "FrameworkElement.Width" -> "Padding" / "Width"
            int dot = property.LastIndexOf('.');
            return dot >= 0 ? property.Substring(dot + 1) : property;
        }

        // Scales "16", "0,0,3,0", "2 1", "-1,0,0,0". Leaves anything else (Auto, *, bindings) alone.
        internal static bool TryScaleList(string text, double scale, out string result)
        {
            result = text;

            if (string.IsNullOrWhiteSpace(text) || text.Contains('{'))
            {
                return false;
            }

            string trimmed = text.Trim();
            char separator = trimmed.Contains(',') ? ',' : ' ';
            string[] parts = trimmed.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 || parts.Length > 4)
            {
                return false;
            }

            var scaledParts = new List<string>(parts.Length);

            foreach (string part in parts)
            {
                if (!double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                    double.IsNaN(value) || double.IsInfinity(value))
                {
                    return false;
                }

                double scaled = Math.Round(value * scale, MidpointRounding.AwayFromZero);
                scaledParts.Add(scaled.ToString(CultureInfo.InvariantCulture));
            }

            result = string.Join(separator == ',' ? "," : " ", scaledParts);
            return true;
        }
    }
}
