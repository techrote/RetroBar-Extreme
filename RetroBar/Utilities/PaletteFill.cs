using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: colours to fill a palette's empty slots with: the complements of the colours
    // already there first, then analogous (neighbouring) hues, triads, and lighter / darker / muted
    // relatives. A colour too close to one already in the palette is skipped.
    public static class PaletteFill
    {
        // (hue shift in turns, lightness change, saturation scale), nearest relatives first.
        private static readonly (double h, double l, double s)[] Relatives =
        {
            (0.5, 0, 1), (0.083, 0, 1), (-0.083, 0, 1), (0.333, 0, 1), (-0.333, 0, 1), (0, 0.18, 1), (0, -0.18, 1),
            (0.5, 0.12, 0.8), (0.5, -0.12, 0.8), (0.083, 0.15, 0.7), (-0.083, -0.15, 0.7), (0, 0.3, 0.5),
            (0.25, 0, 0.9), (-0.25, 0, 0.9), (0.167, 0.1, 0.8), (-0.167, -0.1, 0.8),
        };

        // Up to `count` new colours for a palette that holds `existing`.
        public static List<Color> Generate(IReadOnlyList<Color> existing, int count)
        {
            var result = new List<Color>();
            if (existing == null || existing.Count == 0 || count <= 0)
            {
                return result;
            }

            var have = existing.ToList();
            foreach (double threshold in new[] { 44.0, 30.0, 16.0 })
            {
                for (int step = 0; step < Relatives.Length && result.Count < count; step++)
                {
                    foreach (Color seed in existing)
                    {
                        if (result.Count >= count)
                        {
                            break;
                        }

                        Color candidate = Relative(seed, Relatives[step]);
                        if (have.All(c => Distance(c, candidate) >= threshold))
                        {
                            have.Add(candidate);
                            result.Add(candidate);
                        }
                    }
                }
            }
            return result;
        }

        private static Color Relative(Color c, (double h, double l, double s) r)
        {
            ToHsl(c, out double h, out double s, out double l);
            h = ((h + r.h) % 1 + 1) % 1;
            l = Math.Clamp(l + r.l, 0.06, 0.94);
            s = Math.Clamp(s * r.s, 0, 1);
            return FromHsl(h, s, l);
        }

        public static double Distance(Color a, Color b)
        {
            double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            double d = max - min;
            if (d < 1e-9)
            {
                h = 0;
                s = 0;
                return;
            }
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
            else if (max == g) h = ((b - r) / d + 2) / 6;
            else h = ((r - g) / d + 4) / 6;
        }

        private static Color FromHsl(double h, double s, double l)
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            double channel(double t)
            {
                t = ((t % 1) + 1) % 1;
                if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                if (t < 0.5) return q;
                if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                return p;
            }
            return Color.FromRgb((byte)Math.Round(channel(h + 1.0 / 3) * 255), (byte)Math.Round(channel(h) * 255), (byte)Math.Round(channel(h - 1.0 / 3) * 255));
        }
    }
}
