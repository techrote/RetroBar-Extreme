using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: texture parameter presets. Five come built in for every pattern; your own are
    // kept in %LOCALAPPDATA%\RetroBar\TexturePresets.json. A preset is a texture config (see
    // TextureConfig) with a name, so presets import, export and copy / paste in the same form.
    // Applying a preset sets the pattern and its parameters and keeps the colours you already have.
    public static class TexturePresets
    {
        public static string UserFile => "TexturePresets.json".InLocalAppData();

        private static readonly Dictionary<string, List<TextureSpec>> BuiltIn = Build();

        public static IReadOnlyList<TextureSpec> ForPattern(string pattern) =>
            BuiltIn.TryGetValue(pattern ?? "", out var list) ? list : (IReadOnlyList<TextureSpec>)Array.Empty<TextureSpec>();

        public static int BuiltInCount => BuiltIn.Values.Sum(l => l.Count);

        public static IEnumerable<string> Patterns => BuiltIn.Keys;

        // ---- your own presets ---------------------------------------------------------------
        public static List<TextureSpec> LoadUser()
        {
            try
            {
                return File.Exists(UserFile) ? TextureConfig.ReadFile(File.ReadAllText(UserFile)) : new List<TextureSpec>();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException)
            {
                return new List<TextureSpec>();
            }
        }

        public static void SaveUser(IEnumerable<TextureSpec> presets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserFile));
            string temp = UserFile + ".tmp";
            File.WriteAllText(temp, TextureConfig.WriteFile(presets), new UTF8Encoding(false));
            File.Move(temp, UserFile, true);
        }

        // Adds or replaces (by name) one of your presets.
        public static void Store(TextureSpec preset, string name)
        {
            TextureSpec copy = preset.Clone();
            copy.Label = name.Trim();
            var list = LoadUser();
            list.RemoveAll(p => string.Equals(p.Label, copy.Label, StringComparison.CurrentCultureIgnoreCase));
            list.Add(copy);
            SaveUser(list.OrderBy(p => p.Label, StringComparer.CurrentCultureIgnoreCase));
        }

        public static void Delete(string name)
        {
            var list = LoadUser();
            if (list.RemoveAll(p => string.Equals(p.Label, name, StringComparison.CurrentCultureIgnoreCase)) > 0)
            {
                SaveUser(list);
            }
        }

        // Merges presets from a file; returns how many were added or replaced. Unnamed ones are named
        // after the file.
        public static int Import(string path)
        {
            List<TextureSpec> incoming = TextureConfig.ReadFile(File.ReadAllText(path));
            string fallback = Path.GetFileNameWithoutExtension(path);
            int count = 0;
            for (int i = 0; i < incoming.Count; i++)
            {
                string name = string.IsNullOrWhiteSpace(incoming[i].Label) ? (incoming.Count == 1 ? fallback : $"{fallback} {i + 1}") : incoming[i].Label;
                Store(incoming[i], name);
                count++;
            }
            return count;
        }

        public static void Export(string path, IEnumerable<TextureSpec> presets)
        {
            File.WriteAllText(path, TextureConfig.WriteFile(presets), new UTF8Encoding(false));
        }

        // ---- the built-in five per pattern -------------------------------------------------------
        private static Dictionary<string, List<TextureSpec>> Build()
        {
            var all = new Dictionary<string, List<TextureSpec>>();

            void add(string pattern, string label, Action<TextureSpec> set)
            {
                var spec = new TextureSpec { Name = pattern, Label = label };
                set(spec);
                if (!all.TryGetValue(pattern, out var list)) all[pattern] = list = new List<TextureSpec>();
                list.Add(spec);
            }

            add("brushed", "Fine steel", t => { t.Strength = 0.6; t.Contrast = 1.2; });
            add("brushed", "Coarse brushed", t => { t.Strength = 1.6; t.Scale = 2; });
            add("brushed", "Wide strokes", t => { t.Frequency = 2; });
            add("brushed", "Satin", t => { t.Strength = 0.3; t.Contrast = 0.8; });
            add("brushed", "Deep scratches", t => { t.Frequency = 3; t.Strength = 2; t.Contrast = 1.5; t.Sharpen = 1; });

            add("carbon", "Classic weave", t => { });
            add("carbon", "Fine weave", t => { t.Frequency = 16; });
            add("carbon", "Chunky weave", t => { t.Frequency = 4; t.Scale = 1.5; });
            add("carbon", "Soft carbon", t => { t.Contrast = 0.7; t.Saturation = 0.8; });
            add("carbon", "Crisp pixels", t => { t.Scale = 2; t.Resample = "nearest"; t.Sharpen = 1; });

            add("noise", "Film grain", t => { t.Strength = 0.5; });
            add("noise", "Coarse grain", t => { t.Frequency = 32; });
            add("noise", "Chunky pixels", t => { t.Frequency = 16; t.Scale = 2; t.Resample = "nearest"; });
            add("noise", "Soft static", t => { t.Strength = 0.7; t.Scale = 2; t.Resample = "gaussian"; });
            add("noise", "Dither", t => { t.Contrast = 3; });

            add("scanlines", "CRT classic", t => { });
            add("scanlines", "CRT fine", t => { t.Frequency = 32; });
            add("scanlines", "Wide bars", t => { t.Frequency = 8; });
            add("scanlines", "Faint lines", t => { t.Frequency = 32; t.Contrast = 0.5; });
            add("scanlines", "Chunky lines", t => { t.Frequency = 4; t.Scale = 2; t.Resample = "nearest"; });

            add("checker", "Classic checks", t => { });
            add("checker", "Tiny checks", t => { t.Frequency = 32; });
            add("checker", "Large checks", t => { t.Frequency = 4; });
            add("checker", "Faint checks", t => { t.Contrast = 0.4; });
            add("checker", "Hard checks", t => { t.Scale = 2; t.Contrast = 3; t.Resample = "nearest"; });

            add("diamond", "Diamond plate", t => { });
            add("diamond", "Small diamonds", t => { t.Frequency = 8; });
            add("diamond", "Big diamonds", t => { t.Frequency = 2; });
            add("diamond", "Soft plate", t => { t.Contrast = 0.6; t.Scale = 2; t.Resample = "gaussian"; });
            add("diamond", "Sharp plate", t => { t.Scale = 2; t.Resample = "lanczos"; t.Sharpen = 1.5; });

            add("clouds", "Soft clouds", t => { });
            add("clouds", "Wispy", t => { t.Octaves = 6; t.Frequency = 2; t.Strength = 0.8; });
            add("clouds", "Billowy", t => { t.Octaves = 3; t.Frequency = 3; t.Strength = 1.4; });
            add("clouds", "Fog", t => { t.Octaves = 2; t.Frequency = 2; t.Contrast = 0.6; });
            add("clouds", "Storm", t => { t.Octaves = 5; t.Contrast = 2; });

            add("cells", "Pebbles", t => { });
            add("cells", "Fine cells", t => { t.Frequency = 12; });
            add("cells", "Big cells", t => { t.Frequency = 3; });
            add("cells", "Scales", t => { t.Frequency = 10; t.Strength = 1.6; });
            add("cells", "Foam", t => { t.Frequency = 8; t.Contrast = 1.8; });

            add("marble", "Classic veins", t => { });
            add("marble", "Wild veins", t => { t.Frequency = 3; t.Strength = 2; });
            add("marble", "Fine veins", t => { t.Frequency = 4; t.Octaves = 6; t.Strength = 0.6; });
            add("marble", "Calm marble", t => { t.Frequency = 1; t.Octaves = 2; t.Strength = 0.5; });
            add("marble", "Swirl", t => { t.Frequency = 3; t.Octaves = 5; t.Strength = 1.5; });

            add("weave", "Plain weave", t => { });
            add("weave", "Fine weave", t => { t.Frequency = 16; });
            add("weave", "Basket weave", t => { t.Frequency = 4; });
            add("weave", "Rough cloth", t => { t.Strength = 2; });
            add("weave", "Linen", t => { t.Frequency = 12; t.Strength = 0.5; });

            add("bricks", "Standard bricks", t => { });
            add("bricks", "Small bricks", t => { t.Frequency = 16; });
            add("bricks", "Large bricks", t => { t.Frequency = 4; });
            add("bricks", "Mixed bricks", t => { t.Strength = 2; });
            add("bricks", "Clean bricks", t => { t.Strength = 0.2; });

            add("dots", "Halftone", t => { });
            add("dots", "Fine dots", t => { t.Frequency = 16; });
            add("dots", "Big dots", t => { t.Frequency = 4; t.Strength = 1.4; });
            add("dots", "Pinholes", t => { t.Frequency = 12; t.Strength = 0.3; });
            add("dots", "Polka", t => { t.Frequency = 6; t.Strength = 1.6; });

            add("waves", "Ripples", t => { });
            add("waves", "Fine ripples", t => { t.Frequency = 4; t.Octaves = 3; });
            add("waves", "Choppy", t => { t.Frequency = 3; t.Octaves = 4; t.Strength = 1.5; });
            add("waves", "Gentle swell", t => { t.Frequency = 1; t.Octaves = 2; t.Strength = 0.6; });
            add("waves", "Interference", t => { t.Frequency = 6; t.Octaves = 1; });

            return all;
        }
    }
}
