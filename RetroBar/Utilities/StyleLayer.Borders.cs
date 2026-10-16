using System;
using System.IO;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-013): per-part border thickness from the style file. Unlike brushes,
    // borders are baked into the theme as it loads (ThemeBorders), so the theme loader reads
    // them first, and a style change that alters them asks for the theme to be reloaded.
    public static partial class StyleLayer
    {
        // Raised when Style.json's borders differ from what the loaded theme was built with.
        public static event EventHandler ThemeReloadNeeded;

        // Borders the current theme was built with.
        public static StyleDocument LoadedBorders { get; private set; } = new StyleDocument();

        public static bool HasBorders => LoadedBorders.Borders.Count > 0;

        // Called by the theme loader before it loads a theme.
        public static StyleDocument ReadBordersForTheme()
        {
            LoadedBorders = ReadBorders();
            return LoadedBorders;
        }

        private static StyleDocument ReadBorders()
        {
            try
            {
                // Warnings are logged once, when the style layer itself reads the file.
                return File.Exists(StylePath) ? StyleDocument.Load(StylePath, _ => { }) : new StyleDocument();
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException)
            {
                return LoadedBorders; // unreadable just now: keep what the theme has
            }
        }

        private static void CheckBorders()
        {
            if (!ReadBorders().SameBorders(LoadedBorders))
            {
                ThemeReloadNeeded?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
