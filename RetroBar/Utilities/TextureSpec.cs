using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-011): a texture is greyscale data plus an evenly spaced colour LUT.
    // Palette references remain references on disk; they are resolved when the layer is built.
    public sealed class TextureSpec
    {
        public const double MinScale = 0.25, MaxScale = 8, MinContrast = 0, MaxContrast = 4;
        public string Name = "brushed";
        public List<Color> Lut = new List<Color> { Colors.Black, Colors.White };
        public bool UsePalette;
        public double Scale = 1;
        public double Contrast = 1;

        public TextureSpec Clone() => new TextureSpec
        {
            Name = Name, Lut = Lut?.ToList(), UsePalette = UsePalette, Scale = Scale, Contrast = Contrast,
        };

        public void Validate()
        {
            if (!IsValidName(Name))
                throw new FormatException("texture must be a built-in name or a plain .png filename in the Textures folder");
            CheckRange(Scale, MinScale, MaxScale, "scale");
            CheckRange(Contrast, MinContrast, MaxContrast, "contrast");
            if (!UsePalette && (Lut == null || Lut.Count < 2 || Lut.Count > 6))
                throw new FormatException("a texture LUT needs 2 to 6 colours");
        }

        public Color[] ResolveLut(IEnumerable<Color?> palette)
        {
            Validate();
            if (!UsePalette) return Lut.ToArray();
            Color[] colours = (palette ?? Enumerable.Empty<Color?>()).Take(StyleDocument.PALETTE_SIZE)
                .Where(c => c.HasValue).Select(c => c.Value).ToArray();
            if (colours.Length < 2)
                throw new FormatException("the texture needs at least two occupied palette slots");
            return colours;
        }

        public static bool IsValidName(string name)
        {
            if (LutTextures.BuiltInNames.Contains(name)) return true;
            return !string.IsNullOrWhiteSpace(name) && name.Length <= 120 &&
                name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && name.Length > 4 &&
                !name.StartsWith(".", StringComparison.Ordinal) && name == name.Trim() &&
                name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                name.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && !Path.IsPathRooted(name);
        }

        private static void CheckRange(double number, double min, double max, string name)
        {
            if (!double.IsFinite(number) || number < min || number > max)
                throw new FormatException($"texture {name} must be a finite number from {min} to {max}");
        }

        public static TextureSpec Parse(JsonElement value)
        {
            var spec = new TextureSpec();
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (property.Name != "texture" && property.Name != "lut" && property.Name != "scale" && property.Name != "contrast")
                    throw new FormatException($"unknown texture property \"{property.Name}\"");
            }
            JsonElement name = value.GetProperty("texture");
            if (name.ValueKind != JsonValueKind.String)
                throw new FormatException("texture name must be a string");
            spec.Name = name.GetString();
            if (!value.TryGetProperty("lut", out JsonElement lut))
                throw new FormatException("a texture needs a lut array or \"palette\"");
            if (lut.ValueKind == JsonValueKind.String && lut.GetString() == "palette") spec.UsePalette = true;
            else if (lut.ValueKind == JsonValueKind.Array && lut.GetArrayLength() >= 2 && lut.GetArrayLength() <= 6)
            {
                spec.Lut.Clear();
                foreach (JsonElement colour in lut.EnumerateArray())
                {
                    if (colour.ValueKind != JsonValueKind.String)
                        throw new FormatException("LUT colours must be colour strings");
                    spec.Lut.Add(BrushSpec.ParseColor(colour.GetString()));
                }
            }
            else throw new FormatException("lut must be 2 to 6 colour strings, or \"palette\"");
            spec.Scale = ReadNumber(value, "scale", 1);
            spec.Contrast = ReadNumber(value, "contrast", 1);
            spec.Validate();
            return spec;
        }

        private static double ReadNumber(JsonElement value, string name, double fallback)
        {
            if (!value.TryGetProperty(name, out JsonElement number)) return fallback;
            if (number.ValueKind != JsonValueKind.Number || !number.TryGetDouble(out double result))
                throw new FormatException($"texture {name} must be a number");
            return result;
        }

        public void Write(Utf8JsonWriter writer)
        {
            Validate();
            writer.WriteStartObject();
            writer.WriteString("texture", Name);
            if (UsePalette) writer.WriteString("lut", "palette");
            else
            {
                writer.WriteStartArray("lut");
                foreach (Color c in Lut) writer.WriteStringValue(BrushSpec.FormatColor(c));
                writer.WriteEndArray();
            }
            writer.WriteNumber("scale", Scale);
            writer.WriteNumber("contrast", Contrast);
            writer.WriteEndObject();
        }

        public override string ToString() => $"{Name} ({(UsePalette ? "palette" : string.Join(" → ", Lut.Select(BrushSpec.FormatColor)))})";
    }
}
