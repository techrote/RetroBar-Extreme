using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: a texture as one line of text, the same structure a style file stores for a
    // texture property. This is what the copy / paste field, the texture palette, presets and
    // preset files all use, so a texture can travel between any of them unchanged.
    public static class TextureConfig
    {
        private static readonly JsonDocumentOptions Options = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        public static string ToText(TextureSpec spec, bool indented = false)
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = indented }))
            {
                spec.Write(writer);
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        // Parses a texture config. Colours missing from the text (a config that only lists
        // parameters) come from `colours`. Throws FormatException with a readable reason.
        public static TextureSpec FromText(string text, TextureSpec colours = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException("nothing to read");
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(text, Options);
                return FromElement(document.RootElement, colours);
            }
            catch (JsonException e)
            {
                throw new FormatException("not valid JSON: " + e.Message);
            }
        }

        public static TextureSpec FromElement(JsonElement element, TextureSpec colours = null)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("expected a texture object such as { \"texture\": \"brushed\", \"lut\": [\"#000\", \"#fff\"] }");
            }
            try
            {
                return TextureSpec.Parse(element, colours);
            }
            catch (KeyNotFoundException)
            {
                throw new FormatException("a texture needs a \"texture\" name such as \"brushed\"");
            }
        }

        // Presets in a file: [ { "name": "...", "texture": "...", ... }, ... ] (the same objects as
        // above, with a name), or a single such object.
        public static List<TextureSpec> ReadFile(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json, Options);
                JsonElement root = document.RootElement;
                var list = new List<TextureSpec>();
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in root.EnumerateArray())
                    {
                        list.Add(FromElement(item));
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("presets", out JsonElement inner) && inner.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in inner.EnumerateArray())
                    {
                        list.Add(FromElement(item));
                    }
                }
                else
                {
                    list.Add(FromElement(root));
                }
                return list;
            }
            catch (JsonException e)
            {
                throw new FormatException("not valid JSON: " + e.Message);
            }
        }

        public static string WriteFile(IEnumerable<TextureSpec> specs)
        {
            var sb = new StringBuilder("[").AppendLine();
            string[] items = specs.Select(s => "  " + ToText(s)).ToArray();
            sb.Append(string.Join("," + Environment.NewLine, items));
            sb.AppendLine().AppendLine("]");
            return sb.ToString();
        }
    }
}
