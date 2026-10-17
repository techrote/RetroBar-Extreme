using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-017): the example styles (styles\*.json in the repository) are built into
    // the executable, so every build lists them under Saved styles. A saved style of the same
    // name takes their place; deleting it brings the example back.
    public partial class StyleDocument
    {
        private const string ExamplePrefix = "RetroBar.ExampleStyles.";

        private static List<string> _exampleNames;

        public static List<string> ExampleNames() => _exampleNames ??= typeof(StyleDocument).Assembly
            .GetManifestResourceNames()
            .Where(n => n.StartsWith(ExamplePrefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(n => n.Substring(ExamplePrefix.Length, n.Length - ExamplePrefix.Length - ".json".Length))
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // The Saved styles list: your saved styles, then the examples you haven't replaced.
        public static List<string> ListedNames()
        {
            List<string> saved = SavedNames();
            return saved.Concat(ExampleNames().Where(n => !saved.Contains(n, StringComparer.OrdinalIgnoreCase))).ToList();
        }

        // True when name is a built-in example that no saved style replaces.
        public static bool IsExample(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                   !File.Exists(SavedPath(name)) &&
                   ExampleNames().Contains(name.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        // The example's JSON, or null.
        public static string ExampleJson(string name)
        {
            string match = ExampleNames().FirstOrDefault(n => string.Equals(n, name?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                return null;
            }

            using Stream stream = typeof(StyleDocument).Assembly.GetManifestResourceStream(ExamplePrefix + match + ".json");
            if (stream == null)
            {
                return null;
            }
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
