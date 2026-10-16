using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-003, UX-004): a style file in memory. Read by the style layer, edited by
    // the Colours tab in Properties, saved as Style.json (the active style) or as a named style
    // in %LOCALAPPDATA%\RetroBar\Styles\. The file format is described in StyleLayer.
    public partial class StyleDocument // UXUI-Extreme (UX-010): Start image settings in StyleDocument.Start.cs.
    {
        public string Name = "";

        // part -> property -> brush, e.g. "button" -> "background" -> #3a3f44
        public readonly Dictionary<string, Dictionary<string, BrushSpec>> Parts = new Dictionary<string, Dictionary<string, BrushSpec>>();

        public static string StylesFolder => "Styles".InLocalAppData();

        private static readonly JsonDocumentOptions JsonOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        // UX-009: the floating palette in the editor, kept with the style. Null = empty slot.
        public const int PALETTE_SIZE = 12;
        public readonly Color?[] Palette = new Color?[PALETTE_SIZE];

        public bool IsEmpty => !StartRecolour.HasValue && Parts.Values.All(p => p.Count == 0) && Palette.All(c => !c.HasValue);

        public BrushSpec Get(string part, string property)
        {
            return Parts.TryGetValue(part, out var properties) && properties.TryGetValue(property, out BrushSpec spec) ? spec : null;
        }

        public void Set(string part, string property, BrushSpec spec)
        {
            if (spec == null)
            {
                if (Parts.TryGetValue(part, out var existing))
                {
                    existing.Remove(property);
                    if (existing.Count == 0)
                    {
                        Parts.Remove(part);
                    }
                }
                return;
            }

            if (!Parts.TryGetValue(part, out var properties))
            {
                Parts[part] = properties = new Dictionary<string, BrushSpec>();
            }
            properties[property] = spec;
        }

        public StyleDocument Clone()
        {
            var copy = new StyleDocument { Name = Name, StartRecolour = StartRecolour };
            Array.Copy(Palette, copy.Palette, PALETTE_SIZE);
            foreach (var part in Parts)
            {
                foreach (var property in part.Value)
                {
                    copy.Set(part.Key, property.Key, property.Value.Clone());
                }
            }
            return copy;
        }

        // Reads a style file. Throws IOException if it can't be read and FormatException if it
        // isn't a style at all; bad values inside are reported through warn and skipped.
        public static StyleDocument Load(string path, Action<string> warn)
        {
            string json;
            // Shared read so a file still open in an editor can be read.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                json = reader.ReadToEnd();
            }

            return Parse(json, warn);
        }

        public static StyleDocument Parse(string json, Action<string> warn)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json, JsonOptions);
            }
            catch (JsonException e)
            {
                throw new FormatException($"not valid JSON: {e.Message}");
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException("expected a style object");
                }
                // UXUI-Extreme (UX-010): also allow a Start-only style without brush parts.
                IEnumerable<JsonProperty> parts = ReadParts(root);

                var style = new StyleDocument();
                style.ReadStart(root, warn);
                if (root.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String)
                {
                    style.Name = name.GetString();
                }

                foreach (JsonProperty part in parts)
                {
                    if (!StyleMap.PartProperties.TryGetValue(part.Name, out string[] properties) || part.Value.ValueKind != JsonValueKind.Object)
                    {
                        warn($"style part \"{part.Name}\" is unknown, skipped. Known parts: {string.Join(", ", StyleMap.Parts)}.");
                        continue;
                    }

                    foreach (JsonProperty property in part.Value.EnumerateObject())
                    {
                        string fullName = $"{part.Name}.{property.Name}";

                        if (Array.IndexOf(properties, property.Name) < 0)
                        {
                            warn($"style property \"{fullName}\" is unknown, skipped. \"{part.Name}\" has: {string.Join(", ", properties)}.");
                            continue;
                        }

                        try
                        {
                            style.Set(part.Name, property.Name, BrushSpec.Parse(property.Value));
                        }
                        catch (Exception e)
                        {
                            warn($"style value for \"{fullName}\" skipped: {e.Message}");
                        }
                    }
                }

                // UXUI-Extreme (UX-011): palette references resolve after the complete style is read.
                if (root.TryGetProperty("palette", out JsonElement palette) && palette.ValueKind == JsonValueKind.Array)
                {
                    int slot = 0;
                    foreach (JsonElement entry in palette.EnumerateArray())
                    {
                        if (slot >= PALETTE_SIZE)
                        {
                            break;
                        }

                        if (entry.ValueKind == JsonValueKind.String && BrushSpec.TryParseColor(entry.GetString(), out Color colour))
                        {
                            style.Palette[slot] = colour;
                        }
                        slot++;
                    }
                }

                return style;
            }
        }

        public string ToJson()
        {
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("name", Name ?? "");
                writer.WriteStartObject("parts");

                // Write parts and properties in the documented order so files diff nicely.
                foreach (string part in StyleMap.Parts.Where(Parts.ContainsKey))
                {
                    writer.WriteStartObject(part);
                    foreach (string property in StyleMap.PartProperties[part].Where(Parts[part].ContainsKey))
                    {
                        writer.WritePropertyName(property);
                        Parts[part][property].Write(writer);
                    }
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                WriteStart(writer); // UXUI-Extreme (UX-010): omitted when the theme image is unchanged.

                // UX-009: palette slots, empty ones as null so positions are kept.
                if (Palette.Any(c => c.HasValue))
                {
                    writer.WriteStartArray("palette");
                    foreach (Color? colour in Palette)
                    {
                        if (colour is Color c)
                        {
                            writer.WriteStringValue(BrushSpec.FormatColor(c));
                        }
                        else
                        {
                            writer.WriteNullValue();
                        }
                    }
                    writer.WriteEndArray();
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray()) + Environment.NewLine;
        }

        // Writes via a temporary file so the style layer's watcher never reads a half-written file.
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, ToJson(), new UTF8Encoding(false));
            File.Move(temp, path, true);
        }

        // Named styles

        public static List<string> SavedNames()
        {
            if (!Directory.Exists(StylesFolder))
            {
                return new List<string>();
            }

            return Directory.GetFiles(StylesFolder, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static string SavedPath(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return Path.Combine(StylesFolder, name.Trim() + ".json");
        }
    }

    // A solid colour, linear gradient, or LUT texture (UXUI-Extreme: UX-011).
    public class BrushSpec
    {
        public Color Color = Colors.Gray;

        // null for a solid colour.
        public List<GradientStop> Stops;

        // Degrees: 0 = left to right, 90 = top to bottom.
        public double Angle = 90;

        // UXUI-Extreme (UX-011): separate data, never disguised as a gradient.
        public TextureSpec Texture;
        public bool IsTexture => Texture != null;
        public bool IsGradient => !IsTexture && Stops != null;

        public static BrushSpec Solid(Color color) => new BrushSpec { Color = color };

        public static BrushSpec Gradient(double angle, params Color[] colors)
        {
            var spec = new BrushSpec { Angle = angle, Stops = new List<GradientStop>() };
            for (int i = 0; i < colors.Length; i++)
            {
                spec.Stops.Add(new GradientStop(colors[i], colors.Length == 1 ? 0 : (double)i / (colors.Length - 1)));
            }
            return spec;
        }

        public BrushSpec Clone()
        {
            return new BrushSpec
            {
                Color = Color,
                Angle = Angle,
                Stops = Stops?.Select(s => new GradientStop(s.Color, s.Offset)).ToList(),
                Texture = Texture?.Clone(), // UXUI-Extreme (UX-011): LUT list is independently editable.
            };
        }

        // From a theme brush, so the editor can start from what the theme shows now.
        public static BrushSpec FromBrush(Brush brush)
        {
            switch (brush)
            {
                case SolidColorBrush solid:
                    return Solid(solid.Color);
                // Absolute (pixel) gradients keep their direction; offsets are along the line either way.
                case LinearGradientBrush linear when linear.GradientStops.Count > 0 && linear.StartPoint != linear.EndPoint:
                    double angle = Math.Atan2(linear.EndPoint.Y - linear.StartPoint.Y, linear.EndPoint.X - linear.StartPoint.X) * 180 / Math.PI;
                    return new BrushSpec
                    {
                        Angle = Math.Round((angle + 360) % 360),
                        Stops = linear.GradientStops.OrderBy(s => s.Offset).Select(s => new GradientStop(s.Color, Math.Clamp(s.Offset, 0, 1))).ToList(),
                    };
                case GradientBrush gradient when gradient.GradientStops.Count > 0:
                    return Solid(gradient.GradientStops.OrderBy(s => s.Offset).First().Color);
                default:
                    return null;
            }
        }

        public static BrushSpec Parse(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                return Solid(ParseColor(value.GetString()));
            }

            // UXUI-Extreme (UX-011): parse without touching the filesystem or resolving palette order.
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("texture", out _))
            {
                return new BrushSpec { Texture = TextureSpec.Parse(value) };
            }

            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("stops", out JsonElement stops) || stops.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("expected a colour such as \"#3a3f44\", a gradient, or a texture with a LUT");
            }

            var spec = new BrushSpec { Stops = new List<GradientStop>() };

            if (value.TryGetProperty("angle", out JsonElement angle))
            {
                if (angle.ValueKind != JsonValueKind.Number)
                {
                    throw new FormatException("a gradient's \"angle\" is a number of degrees, such as 90");
                }

                spec.Angle = angle.GetDouble();
            }

            int count = stops.GetArrayLength();
            if (count < 1)
            {
                throw new FormatException("a gradient needs at least one stop");
            }

            int i = 0;
            foreach (JsonElement stop in stops.EnumerateArray())
            {
                double evenOffset = count == 1 ? 0 : (double)i / (count - 1);

                if (stop.ValueKind == JsonValueKind.String)
                {
                    spec.Stops.Add(new GradientStop(ParseColor(stop.GetString()), evenOffset));
                }
                else if (stop.ValueKind == JsonValueKind.Object && stop.TryGetProperty("color", out JsonElement color))
                {
                    double offset = evenOffset;
                    if (stop.TryGetProperty("offset", out JsonElement o))
                    {
                        offset = o.ValueKind == JsonValueKind.Number ? o.GetDouble()
                            : throw new FormatException("a gradient stop's \"offset\" is a number from 0 to 1");
                    }
                    spec.Stops.Add(new GradientStop(ParseColor(color.ValueKind == JsonValueKind.String ? color.GetString() : color.GetRawText()), Math.Clamp(offset, 0, 1)));
                }
                else
                {
                    throw new FormatException("a gradient stop is a colour, or { \"color\": \"#colour\", \"offset\": 0.5 }");
                }

                i++;
            }

            return spec;
        }

        public static Color ParseColor(string text)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(text?.Trim());
            }
            catch (Exception)
            {
                throw new FormatException($"\"{text}\" is not a colour; use \"#rrggbb\", \"#aarrggbb\" or a colour name");
            }
        }

        public static bool TryParseColor(string text, out Color color)
        {
            try
            {
                color = ParseColor(text);
                return true;
            }
            catch (FormatException)
            {
                color = default;
                return false;
            }
        }

        public static string FormatColor(Color c)
        {
            return c.A == 255 ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : $"#{c.A:x2}{c.R:x2}{c.G:x2}{c.B:x2}";
        }

        public void Write(Utf8JsonWriter writer)
        {
            if (IsTexture) // UXUI-Extreme (UX-011)
            {
                Texture.Write(writer);
                return;
            }
            if (!IsGradient)
            {
                writer.WriteStringValue(FormatColor(Color));
                return;
            }

            writer.WriteStartObject();
            writer.WriteNumber("angle", Math.Round(Angle, 2));
            writer.WriteStartArray("stops");

            var ordered = Stops.OrderBy(s => s.Offset).ToList();
            bool even = ordered.Select((s, i) => Math.Abs(s.Offset - (ordered.Count == 1 ? 0 : (double)i / (ordered.Count - 1))) < 0.001).All(e => e);
            foreach (GradientStop stop in ordered)
            {
                if (even)
                {
                    writer.WriteStringValue(FormatColor(stop.Color));
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteString("color", FormatColor(stop.Color));
                    writer.WriteNumber("offset", Math.Round(stop.Offset, 3));
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        // rotate: extra degrees, for backgrounds drawn on a vertical taskbar.
        public Brush ToBrush(double rotate = 0, IEnumerable<Color?> palette = null)
        {
            if (IsTexture) return LutTextures.Create(Texture, palette, rotate + Texture.Angle); // UXUI-Extreme (UX-011)
            Brush brush;

            if (!IsGradient)
            {
                brush = new SolidColorBrush(Color);
            }
            else
            {
                // Centre the gradient line in the box so every angle spans it end to end.
                double radians = (Angle + rotate) * Math.PI / 180;
                double dx = Math.Cos(radians) / 2, dy = Math.Sin(radians) / 2;
                var stops = new GradientStopCollection(Stops.Select(s => new GradientStop(s.Color, s.Offset)));
                brush = new LinearGradientBrush(stops, new Point(0.5 - dx, 0.5 - dy), new Point(0.5 + dx, 0.5 + dy));
            }

            brush.Freeze();
            return brush;
        }

        public override string ToString()
        {
            if (IsTexture) return Texture.ToString(); // UXUI-Extreme (UX-011)
            return IsGradient
                ? string.Join(" → ", Stops.OrderBy(s => s.Offset).Select(s => FormatColor(s.Color))) + $", {Angle.ToString(CultureInfo.CurrentCulture)}°"
                : FormatColor(Color);
        }
    }
}
