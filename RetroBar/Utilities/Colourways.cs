using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-009): colourways. From a base colour (the task button face) and an accent,
    // build a matching set of colours for the task buttons in every state, the notification
    // area, the clock and the 3D edges. The editor writes the result into the style as ordinary
    // part settings, so everything can be fine-tuned afterwards. Presets are just named
    // base/accent pairs run through the same generator.
    public static class Colourways
    {
        public readonly struct Preset
        {
            public readonly string Name;
            public readonly Color Base;
            public readonly Color Accent;

            public Preset(string name, string baseColour, string accent)
            {
                Name = name;
                Base = BrushSpec.ParseColor(baseColour);
                Accent = BrushSpec.ParseColor(accent);
            }
        }

        public static readonly Preset[] Presets =
        {
            new Preset("Steel", "#4e545a", "#ff9a3c"),
            new Preset("Neon", "#3b1a6e", "#42fffb"),
            new Preset("Olive", "#5b6b3a", "#e8c860"),
            new Preset("Ember", "#4a2a22", "#ff6a2a"),
            new Preset("Ice", "#9fbcd2", "#1f5fa8"),
            new Preset("Classic", "#c0c0c0", "#000080"),
        };

        // Parts the generator writes; the rest of the style (bar, saved name, palette) is kept.
        public static readonly string[] GeneratedParts = { "button", "buttonHover", "buttonActive", "buttonFlashing", "tray", "clock", "bevel" };

        public static void Apply(StyleDocument style, Color baseColour, Color accent)
        {
            foreach (string part in GeneratedParts)
            {
                style.Parts.Remove(part);
            }

            foreach (var entry in Generate(baseColour, accent))
            {
                string[] name = entry.Key.Split('.');
                style.Set(name[0], name[1], entry.Value);
            }
        }

        public static Dictionary<string, BrushSpec> Generate(Color b, Color accent)
        {
            Color face = b;
            Color faceTop = Lighter(b, 0.10), faceBottom = Darker(b, 0.08);
            Color hoverTop = Lighter(b, 0.20), hoverBottom = Lighter(b, 0.03);
            Color activeTop = Darker(b, 0.26), activeBottom = Darker(b, 0.13);
            Color trayTop = Darker(b, 0.14), trayBottom = Darker(b, 0.26);
            Color flashTop = Lighter(accent, 0.12), flashBottom = Darker(accent, 0.18);

            return new Dictionary<string, BrushSpec>
            {
                ["button.background"] = BrushSpec.Gradient(90, faceTop, faceBottom),
                ["button.border"] = BrushSpec.Solid(Darker(b, 0.45)),
                ["button.foreground"] = BrushSpec.Solid(TextFor(face)),

                ["buttonHover.background"] = BrushSpec.Gradient(90, hoverTop, hoverBottom),
                ["buttonHover.border"] = BrushSpec.Solid(Darker(b, 0.35)),

                ["buttonActive.background"] = BrushSpec.Gradient(90, activeTop, activeBottom),
                ["buttonActive.border"] = BrushSpec.Solid(Darker(b, 0.55)),
                ["buttonActive.foreground"] = BrushSpec.Solid(Readable(accent, Mix(activeTop, activeBottom))),

                ["buttonFlashing.background"] = BrushSpec.Gradient(90, flashTop, flashBottom),
                ["buttonFlashing.border"] = BrushSpec.Solid(Darker(accent, 0.50)),
                ["buttonFlashing.foreground"] = BrushSpec.Solid(TextFor(Mix(flashTop, flashBottom))),

                ["tray.background"] = BrushSpec.Gradient(90, trayTop, trayBottom),
                ["tray.border"] = BrushSpec.Solid(Darker(b, 0.50)),

                ["clock.foreground"] = BrushSpec.Solid(Readable(accent, Mix(trayTop, trayBottom))),

                ["bevel.highlight"] = BrushSpec.Solid(Lighter(b, 0.35)),
                ["bevel.light"] = BrushSpec.Solid(Lighter(b, 0.18)),
                ["bevel.shadow"] = BrushSpec.Solid(Darker(b, 0.40)),
                ["bevel.darkShadow"] = BrushSpec.Solid(Darker(b, 0.65)),
            };
        }

        // The accent if it reads well on the background, otherwise plain light or dark text.
        private static Color Readable(Color wanted, Color background)
        {
            return Contrast(wanted, background) >= 3.0 ? wanted : TextFor(background);
        }

        // Near-white or near-black text, whichever contrasts more (WCAG ratio).
        public static Color TextFor(Color background)
        {
            Color light = Color.FromRgb(0xf4, 0xf2, 0xee), dark = Color.FromRgb(0x10, 0x10, 0x10);
            return Contrast(light, background) >= Contrast(dark, background) ? light : dark;
        }

        public static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private static double Luminance(Color c)
        {
            static double channel(byte v)
            {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * channel(c.R) + 0.7152 * channel(c.G) + 0.0722 * channel(c.B);
        }

        // Lighten / darken by moving HSL lightness a fraction of the way to white / black, so the
        // hue stays put.
        public static Color Lighter(Color c, double amount)
        {
            ToHsl(c, out double h, out double s, out double l);
            return FromHsl(h, s, l + (1 - l) * amount, c.A);
        }

        public static Color Darker(Color c, double amount)
        {
            ToHsl(c, out double h, out double s, out double l);
            return FromHsl(h, s, l * (1 - amount), c.A);
        }

        private static Color Mix(Color a, Color b)
        {
            return Color.FromArgb((byte)((a.A + b.A) / 2), (byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));
        }

        public static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;

            if (max == min)
            {
                h = s = 0;
                return;
            }

            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h /= 6;
        }

        public static Color FromHsl(double h, double s, double l, byte alpha = 255)
        {
            l = Math.Clamp(l, 0, 1);
            s = Math.Clamp(s, 0, 1);

            double r, g, b;
            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                static double hue(double p, double q, double t)
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                    if (t < 1.0 / 2) return q;
                    if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                    return p;
                }

                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = hue(p, q, h + 1.0 / 3);
                g = hue(p, q, h);
                b = hue(p, q, h - 1.0 / 3);
            }

            return Color.FromArgb(alpha, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }
    }
}
