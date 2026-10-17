using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-017): the RetroBar cut of 24 font-payload-vault typefaces, built into the
    // executable (extras\fonts\*.ttf as WPF resources under Fonts/), so every build has them
    // without installing anything. Settings store the family name; Resolve() turns a built-in
    // name into its family and leaves any other name to the installed fonts.
    //
    // Knurled Swarf is a true pixel font with one face per size (6-16 px). Windows' text renderer
    // ignores the bitmaps in its universal TTF, so Resolve() picks the face drawn for the text's
    // on-screen size, and PixelSize() moves the text onto exactly that size or a whole multiple.
    public static class BundledFonts
    {
        public const string PixelFamily = "Knurled Swarf";
        private const int PixelMin = 6, PixelMax = 16;

        public static readonly IReadOnlyList<string> Names = new[]
        {
            "Bushing Sans", "Cache Rattle", "Camber Script", "Collet Sans", "Diecast Relay", "Ferrule",
            "Flypress", "Knurled Swarf", "Latchline Mono", "Microslab", "Null Cartridge", "Pebble",
            "Phase Drift", "Pinfeed Grid", "Pixel Cell", "Plinth Mono", "Quantum Step", "Quoin Serif",
            "Railkey Mono", "Raster Forge", "Rivet Condensed", "Sling Arc", "Tallow Condensed", "Torque Plate",
        };

        // The vault's own names for the two faces renamed by the cut.
        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ferrule Regular"] = "Ferrule",
            ["Pebble Bold"] = "Pebble",
        };

        private static readonly Uri BaseUri = new Uri("pack://application:,,,/RetroBar;component/");
        private static readonly Dictionary<string, FontFamily> Cache = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

        public static bool IsBundled(string name) => Canonical(name) != null;

        public static bool IsPixelFont(string name) => string.Equals(Canonical(name), PixelFamily, StringComparison.Ordinal);

        // The built-in family's name for name (aliases included), or null when it isn't one.
        public static string Canonical(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            name = name.Trim();
            if (Aliases.TryGetValue(name, out string alias))
            {
                return alias;
            }
            return Names.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        }

        // A built-in family (for the pixel font, the face for text drawn at devicePixels), or the
        // installed font of that name. Null for an empty name.
        public static FontFamily Resolve(string name, double devicePixels = 12)
        {
            string canonical = Canonical(name);
            if (canonical == null)
            {
                return string.IsNullOrWhiteSpace(name) ? null : new FontFamily(name);
            }

            string family = canonical == PixelFamily ? $"{PixelFamily} {PixelFace(devicePixels)}px" : canonical;
            lock (Cache)
            {
                if (!Cache.TryGetValue(family, out FontFamily result))
                {
                    result = new FontFamily(BaseUri, "./Fonts/#" + family);
                    Cache[family] = result;
                }
                return result;
            }
        }

        // The per-size face for text drawn at devicePixels: that size from 6 to 16 px, and above
        // that the face that divides it (22 px is the 11 px face at 2x).
        public static int PixelFace(double devicePixels)
        {
            int px = (int)Math.Round(devicePixels);
            if (px <= PixelMax)
            {
                return Math.Max(PixelMin, px);
            }
            int times = (int)Math.Ceiling(px / (double)PixelMax);
            return Math.Clamp((int)Math.Round(px / (double)times), PixelMin, PixelMax);
        }

        // The on-screen size the pixel font is drawn at for a requested devicePixels: a whole
        // multiple of its face, so every pixel of the design lands on a screen pixel.
        public static int PixelSize(double devicePixels)
        {
            int face = PixelFace(devicePixels);
            return face * Math.Max(1, (int)Math.Round(devicePixels / face));
        }
    }
}
