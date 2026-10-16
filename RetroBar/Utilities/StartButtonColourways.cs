using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-010): operate on original theme sprites, never on our overrides.
    // HSL lightness and source alpha survive; this is not a flat-colour replacement.
    public static class StartButtonColourways
    {
        public enum SpriteKind { Background, Vista, Longhorn }

        public static readonly IReadOnlyList<string> ResourceKeys = Array.AsReadOnly(new[]
        {
            "StartButtonImage", "StartButtonImageLarge", "StartButtonImageLargeScaled",
            "StartButtonImageBottom", "StartButtonImageBottomScaled",
            "StartButtonImageTop", "StartButtonImageTopScaled",
        });

        // Weak source keys release old themes. Each source/kind retains only its LAST target,
        // not an unbounded history of colours visited while editing. UI-thread use only.
        private static readonly ConditionalWeakTable<BitmapSource, SourceCache> Cache = new();
        private const int MaxPixels = 1024 * 1024;
        private const int MaxSamples = 32768;

        private sealed class SourceCache
        {
            public readonly Dictionary<SpriteKind, Analysis> Items = new();
        }

        private sealed class Analysis
        {
            public byte[] Pixels;
            public Face[] Faces;
            public Color? LastTarget, LastSourceColour;
            public BitmapSource LastResult;
        }

        private sealed class Face
        {
            public Color[] Palette = Array.Empty<Color>();
            public double Hue, Saturation;
            public bool Neutral;
        }

        public static SpriteKind KindFor(string key, BitmapSource source)
        {
            if (key != "StartButtonImage") return SpriteKind.Vista;
            // The built-in Longhorn sprite has an embedded logo; XP's icon/text are separate.
            return source.PixelWidth == 78 && source.PixelHeight == 84
                ? SpriteKind.Longhorn : SpriteKind.Background;
        }

        public static bool HasImage(Func<string, object> themeResource)
        {
            foreach (string key in ResourceKeys)
            {
                try { if (themeResource(key) is BitmapSource source && source.PixelWidth > 0) return true; }
                catch (Exception) { /* A missing/lazy custom image must not break the editor. */ }
            }
            return false;
        }

        public static int Apply(ResourceDictionary destination, Color? target,
            Func<string, object> themeResource, Action<string> warn)
        {
            if (!target.HasValue) return 0;
            int applied = 0;
            // Use one reference colour for all orb variants: independently analysing the clipped
            // halves produces a visible hue/saturation seam when the floating button is joined.
            Color? orbColour = null;
            foreach (string key in new[] { "StartButtonImageLarge", "StartButtonImageLargeScaled" })
            {
                try
                {
                    if (themeResource(key) is BitmapSource orb)
                    {
                        Color[] palette = Extract(orb, SpriteKind.Vista);
                        if (palette.Length > 0) { orbColour = palette[0]; break; }
                    }
                }
                catch (Exception e) { warn?.Invoke($"Start button reference {key} unavailable: {e.Message}"); }
            }
            foreach (string key in ResourceKeys)
            {
                try
                {
                    if (themeResource(key) is not BitmapSource source) continue;
                    SpriteKind kind = KindFor(key, source);
                    destination[key] = Recolour(source, target.Value, kind, kind == SpriteKind.Vista ? orbColour : null);
                    applied++;
                }
                catch (Exception e)
                {
                    // A bad/custom image must not discard otherwise valid brush overrides.
                    warn?.Invoke($"Start button resource {key} kept unchanged: {e.Message}");
                }
            }
            return applied;
        }

        // Prefer the complete, unscaled orb, not its clipped top/bottom or scaled duplicate.
        public static Color[] Extract(Func<string, object> themeResource, Action<string> warn = null)
        {
            foreach (string key in new[] { "StartButtonImageLarge", "StartButtonImage", "StartButtonImageLargeScaled" }
                .Concat(ResourceKeys).Distinct())
            {
                try
                {
                    if (themeResource(key) is not BitmapSource source) continue;
                    Color[] palette = Extract(source, KindFor(key, source));
                    if (palette.Length > 0) return palette;
                }
                catch (Exception e)
                {
                    warn?.Invoke($"Start button palette unavailable for {key}: {e.Message}");
                }
            }
            return Array.Empty<Color>();
        }

        // Palette order: most populous cluster, highlight, shadow (duplicates omitted).
        public static Color[] Extract(BitmapSource source, SpriteKind kind = SpriteKind.Background)
        {
            return (Color[])GetAnalysis(source, kind).Faces[0].Palette.Clone();
        }

        public static BitmapSource Recolour(BitmapSource source, Color target, SpriteKind kind = SpriteKind.Background,
            Color? sourceColour = null)
        {
            Analysis a = GetAnalysis(source, kind);
            // A target's alpha is not opacity: keep each pixel's original coverage instead.
            target = Color.FromRgb(target.R, target.G, target.B);
            if (a.LastTarget == target && a.LastSourceColour == sourceColour && a.LastResult != null) return a.LastResult;
            if (a.Faces.All(face => face.Palette.Length == 0) && !sourceColour.HasValue) return source;
            Face reference = null;
            if (sourceColour is Color colour)
            {
                reference = new Face();
                Colourways.ToHsl(colour, out reference.Hue, out reference.Saturation, out _);
                reference.Neutral = reference.Saturation < 0.12;
            }

            Colourways.ToHsl(target, out double th, out double ts, out _);
            byte[] pixels = (byte[])a.Pixels.Clone();
            int width = source.PixelWidth, frameHeight = FrameHeight(source, kind);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                int pixel = i / 4;
                if (pixels[i + 3] == 0) continue;
                // Royale Noir changes its face hue from blue-black to green on hover/press.
                // Mask each non-orb state against its OWN original hue, then map to the same
                // target. Vista variants instead share the complete orb reference at their join.
                Face face = reference ?? a.Faces[kind == SpriteKind.Vista ? 0 : pixel / (width * frameHeight)];
                if (reference == null && face.Palette.Length == 0) continue;
                double sourceHue = face.Hue, sourceSaturation = face.Saturation;
                double shift = SignedHue(th - sourceHue);
                Color c = Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]);
                Colourways.ToHsl(c, out double h, out double s, out double l);
                if (l <= 0.001 || l >= 0.999) continue; // pure black/white, including white labels
                // Longhorn's neutral background is visible between the panes; do not leave an
                // unrecoloured grey rectangle around its flag when tinting the face.
                if (IsLogo(kind, pixel % width, pixel / width % frameHeight, width, frameHeight)
                    && (kind != SpriteKind.Longhorn || s >= 0.025)) continue;

                double weight, saturation;
                if (face.Neutral)
                {
                    // Neutral Longhorn faces need a tint, not a hue shift of undefined grey.
                    weight = 1 - Smooth((s - 0.12) / 0.10);
                    h = th;
                    saturation = ts * (1 - Math.Pow(2 * l - 1, 4));
                }
                else
                {
                    // Soft hue/saturation shoulders avoid a hard boundary at antialiased edges.
                    weight = (1 - Smooth((Math.Abs(SignedHue(h - sourceHue)) - 0.11) / 0.06))
                        * Smooth((s - 0.02) / 0.10);
                    h = (h + shift * weight + 1) % 1;
                    saturation = Math.Clamp(s * ts / Math.Max(sourceSaturation, 0.02), 0, 1);
                }
                if (weight <= 0) continue;
                Color mapped = Colourways.FromHsl(h, s + (saturation - s) * weight, l);
                pixels[i] = mapped.B;
                pixels[i + 1] = mapped.G;
                pixels[i + 2] = mapped.R;
            }

            var result = BitmapSource.Create(width, source.PixelHeight, source.DpiX, source.DpiY,
                PixelFormats.Bgra32, null, pixels, width * 4);
            result.Freeze();
            a.LastTarget = target;
            a.LastSourceColour = sourceColour;
            a.LastResult = result;
            return result;
        }

        private static double SignedHue(double h) => h - Math.Floor(h + 0.5);
        private static double Smooth(double v)
        {
            v = Math.Clamp(v, 0, 1);
            return v * v * (3 - 2 * v);
        }

        private static int FrameHeight(BitmapSource source, SpriteKind kind)
        {
            if (kind != SpriteKind.Background && source.PixelHeight % 3 != 0)
                throw new ArgumentException("Embedded-logo sprites must contain three vertically stacked states.");
            return source.PixelHeight % 3 == 0 ? source.PixelHeight / 3 : source.PixelHeight;
        }

        // Authored silhouettes in normalized sprite coordinates. Hue alone cannot distinguish
        // Vista's blue flag pane from its blue orb. Include the logo's antialiased outline.
        // Top/bottom variants use the SAME coordinates, avoiding a different mask at the join.
        private static readonly Point[] VistaLogo =
        {
            new(23, 10), new(29, 10), new(33, 13), new(37, 14), new(43, 14),
            new(41, 22), new(40, 25), new(39, 31), new(36, 34), new(31, 34),
            new(27, 31), new(24, 30), new(21, 30), new(17, 32), new(18, 27), new(20, 21), new(21, 15),
        };

        private static bool IsLogo(SpriteKind kind, int x, int y, int width, int frameHeight)
        {
            if (kind == SpriteKind.Background) return false;
            if (kind == SpriteKind.Longhorn)
            {
                double lx = (x + 0.5) * 78 / width, ly = (y + 0.5) * 28 / frameHeight;
                return lx >= 23 && lx <= 46 && ly >= 2 && ly <= 25;
            }
            double px = (x + 0.5) * 60 / width, py = (y + 0.5) * 46 / frameHeight;
            bool inside = false;
            for (int i = 0, j = VistaLogo.Length - 1; i < VistaLogo.Length; j = i++)
            {
                Point a = VistaLogo[i], b = VistaLogo[j];
                if ((a.Y > py) != (b.Y > py) && px < (b.X - a.X) * (py - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        private static Analysis GetAnalysis(BitmapSource source, SpriteKind kind)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if ((long)source.PixelWidth * source.PixelHeight > MaxPixels)
                throw new ArgumentException("Start button image exceeds the 1-megapixel processing limit.");
            SourceCache cache = Cache.GetValue(source, bitmap =>
            {
                var entry = new SourceCache();
                // Mutable custom resources invalidate their analysis rather than showing stale
                // recolours. This handler belongs to the source; it does not keep it alive.
                if (!bitmap.IsFrozen) bitmap.Changed += (s, e) => entry.Items.Clear();
                return entry;
            });
            if (!cache.Items.TryGetValue(kind, out Analysis analysis))
            {
                analysis = Analyse(source, kind);
                cache.Items.Add(kind, analysis);
            }
            return analysis;
        }

        private static Analysis Analyse(BitmapSource source, SpriteKind kind)
        {
            int width = source.PixelWidth, frameHeight = FrameHeight(source, kind);
            // Work in straight BGRA, not premultiplied channels (which create dark fringes).
            BitmapSource straight = source.Format == PixelFormats.Bgra32 ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[checked(width * source.PixelHeight * 4)];
            straight.CopyPixels(pixels, width * 4, 0);
            // Extraction exposes only Faces[0]. Recolouring also analyses other states when
            // their face hues can differ; keep the overall sampling budget bounded.
            int count = kind == SpriteKind.Vista ? 1 : source.PixelHeight / frameHeight;
            var faces = new Face[count];
            for (int state = 0; state < count; state++)
                faces[state] = AnalyseFace(pixels, width, frameHeight, state, kind, MaxSamples / count);
            return new Analysis { Pixels = pixels, Faces = faces };
        }

        private static Face AnalyseFace(byte[] pixels, int width, int frameHeight, int state,
            SpriteKind kind, int sampleBudget)
        {
            var samples = new List<Color>();
            double[] histogram = new double[36];
            int colourful = 0;
            int step = Math.Max(1, (width * frameHeight + sampleBudget - 1) / sampleBudget);
            for (int p = 0; p < width * frameHeight; p += step)
            {
                int i = (state * width * frameHeight + p) * 4;
                if (pixels[i + 3] < 224 || IsLogo(kind, p % width, p / width, width, frameHeight)) continue;
                Color c = Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]);
                Colourways.ToHsl(c, out double h, out double s, out double l);
                if (l < 0.02 || l > 0.97) continue;
                samples.Add(c);
                if (s >= 0.12)
                {
                    histogram[Math.Min(35, (int)(h * 36))] += s;
                    colourful++;
                }
            }
            var analysis = new Face();
            if (samples.Count == 0) return analysis;
            analysis.Neutral = colourful < Math.Max(8, samples.Count / 20);
            int dominant = Array.IndexOf(histogram, histogram.Max());
            double hue = (dominant + 0.5) / 36;
            Color[] candidates = samples.Where(c =>
            {
                Colourways.ToHsl(c, out double h, out double s, out _);
                return analysis.Neutral ? s < 0.12 : s >= 0.08 && Math.Abs(SignedHue(h - hue)) <= 0.11;
            }).ToArray();
            analysis.Palette = Cluster(candidates);
            if (analysis.Palette.Length > 0)
                Colourways.ToHsl(analysis.Palette[0], out analysis.Hue, out analysis.Saturation, out _);
            return analysis;
        }

        private static double Lightness(Color c) => (Math.Max(c.R, Math.Max(c.G, c.B)) + Math.Min(c.R, Math.Min(c.G, c.B))) / 510.0;

        // Deterministic three-centre RGB k-means, seeded by lightness quantiles. No RNG, no
        // dependency on iteration order of a dictionary, and bounded work for custom images.
        private static Color[] Cluster(Color[] samples)
        {
            if (samples.Length == 0) return Array.Empty<Color>();
            Color[] ordered = samples.OrderBy(Lightness).ToArray();
            Color[] centres = { ordered[ordered.Length / 5], ordered[ordered.Length / 2], ordered[ordered.Length * 4 / 5] };
            int[] counts = new int[3];
            for (int iteration = 0; iteration < 8; iteration++)
            {
                var r = new long[3]; var g = new long[3]; var b = new long[3];
                Array.Clear(counts, 0, counts.Length);
                foreach (Color c in samples)
                {
                    int best = 0, distance = int.MaxValue;
                    for (int k = 0; k < 3; k++)
                    {
                        int dr = c.R - centres[k].R, dg = c.G - centres[k].G, db = c.B - centres[k].B;
                        int d = dr * dr + dg * dg + db * db;
                        if (d < distance) { best = k; distance = d; }
                    }
                    counts[best]++; r[best] += c.R; g[best] += c.G; b[best] += c.B;
                }
                for (int k = 0; k < 3; k++)
                    if (counts[k] > 0)
                        centres[k] = Color.FromRgb((byte)Math.Round((double)r[k] / counts[k]),
                            (byte)Math.Round((double)g[k] / counts[k]), (byte)Math.Round((double)b[k] / counts[k]));
            }
            int primary = Array.IndexOf(counts, counts.Max());
            var populated = centres.Where((c, i) => counts[i] > 0).OrderBy(Lightness).ToArray();
            return new[] { centres[primary], populated.Last(), populated.First() }.Distinct().ToArray();
        }
    }
}
