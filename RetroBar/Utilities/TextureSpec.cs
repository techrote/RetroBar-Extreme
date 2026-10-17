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
        public const double MinAngle = 0, MaxAngle = 360; // UXUI-Extreme (UX-013)
        public string Name = "brushed";
        public List<Color> Lut = new List<Color> { Colors.Black, Colors.White };
        public bool UsePalette;
        public double Scale = 1;
        public double Contrast = 1;
        public double Angle = 0; // UXUI-Extreme (UX-013): degrees, added to any vertical-taskbar turn

        // UXUI-Extreme: generator parameters (0 = the generator's own default; which ones a
        // pattern uses is in TextureGenerators.Uses), tone adjustments, and resampling.
        public const double MinBrightness = -1, MaxBrightness = 1, MinSaturation = 0, MaxSaturation = 3;
        public const double MinWhite = 0.1, MaxWhite = 1, MaxSharpen = 4, MinRadius = 0.3, MaxRadius = 4;
        public const double MinStrength = 0, MaxStrength = 3;
        public const int MaxSeed = 9999, MaxOctaves = 8, MaxFrequency = 64;
        public int Seed = 0;
        public int Octaves = 0;
        public int Frequency = 0;
        public double Strength = 1;
        public double Brightness = 0;   // added to the coloured result, -1..1
        public double Saturation = 1;   // 0 = grey, 1 = as the LUT, up to 3
        public double WhiteLevel = 1;   // grey level that maps to the LUT's last colour; lower brightens highlights
        public string Resample = TextureResampler.Smooth; // how the tile is scaled (see TextureResampler)
        public double Sharpen = 0;      // unsharp-mask amount applied to the scaled tile
        public double SharpenRadius = 1; // px
        public string Label = "";        // a preset's name; carried in presets and shared configs, never drawn

        public TextureSpec Clone() => new TextureSpec
        {
            Name = Name, Lut = Lut?.ToList(), UsePalette = UsePalette, Scale = Scale, Contrast = Contrast, Angle = Angle,
            Seed = Seed, Octaves = Octaves, Frequency = Frequency, Strength = Strength,
            Brightness = Brightness, Saturation = Saturation, WhiteLevel = WhiteLevel,
            Resample = Resample, Sharpen = Sharpen, SharpenRadius = SharpenRadius, Label = Label,
        };

        // Takes every setting of a preset, keeping this texture's own colours.
        public void ApplyParameters(TextureSpec preset)
        {
            Name = preset.Name; Scale = preset.Scale; Contrast = preset.Contrast; Angle = preset.Angle;
            Seed = preset.Seed; Octaves = preset.Octaves; Frequency = preset.Frequency; Strength = preset.Strength;
            Brightness = preset.Brightness; Saturation = preset.Saturation; WhiteLevel = preset.WhiteLevel;
            Resample = preset.Resample; Sharpen = preset.Sharpen; SharpenRadius = preset.SharpenRadius;
        }

        public void Validate()
        {
            if (!IsValidName(Name))
                throw new FormatException("texture must be a built-in name or a plain .png filename in the Textures folder");
            CheckRange(Scale, MinScale, MaxScale, "scale");
            CheckRange(Contrast, MinContrast, MaxContrast, "contrast");
            CheckRange(Angle, MinAngle, MaxAngle, "angle");
            CheckRange(Seed, 0, MaxSeed, "seed");
            CheckRange(Octaves, 0, MaxOctaves, "octaves");
            CheckRange(Frequency, 0, MaxFrequency, "frequency");
            CheckRange(Strength, MinStrength, MaxStrength, "strength");
            CheckRange(Brightness, MinBrightness, MaxBrightness, "brightness");
            CheckRange(Saturation, MinSaturation, MaxSaturation, "saturation");
            CheckRange(WhiteLevel, MinWhite, MaxWhite, "white level");
            CheckRange(Sharpen, 0, MaxSharpen, "sharpen");
            CheckRange(SharpenRadius, MinRadius, MaxRadius, "sharpen radius");
            if (!TextureResampler.IsValid(Resample))
                throw new FormatException("unknown texture resample method");
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
                name.IndexOfAny(new[] { '/', '\\', ':' }) < 0 && !Path.IsPathRooted(name) && !IsDeviceName(name);
        }

        private static bool IsDeviceName(string name)
        {
            // A .png extension does not stop Windows resolving a reserved DOS device.
            string stem = name.Split('.')[0].TrimEnd().ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$") return true;
            return stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                "0123456789¹²³".IndexOf(stem[3]) >= 0;
        }

        private static void CheckRange(double number, double min, double max, string name)
        {
            if (!double.IsFinite(number) || number < min || number > max)
                throw new FormatException($"texture {name} must be a finite number from {min} to {max}");
        }

        private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            "texture", "lut", "scale", "contrast", "angle",
            "seed", "octaves", "frequency", "strength", "brightness", "saturation", "white", "resample", "sharpen", "sharpenRadius", "name",
        };

        public static TextureSpec Parse(JsonElement value, TextureSpec colours = null)
        {
            var spec = new TextureSpec();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new FormatException($"duplicate texture property \"{property.Name}\"");
                if (!Known.Contains(property.Name))
                    throw new FormatException($"unknown texture property \"{property.Name}\"");
            }
            JsonElement name = value.GetProperty("texture");
            if (name.ValueKind != JsonValueKind.String)
                throw new FormatException("texture name must be a string");
            spec.Name = name.GetString();
            if (!value.TryGetProperty("lut", out JsonElement lut))
            {
                // A config that only lists parameters keeps the colours it is applied to.
                if (colours == null) throw new FormatException("a texture needs a lut array or \"palette\"");
                spec.Lut = colours.Lut?.ToList() ?? spec.Lut;
                spec.UsePalette = colours.UsePalette;
                lut = default;
            }
            else if (lut.ValueKind == JsonValueKind.String && lut.GetString() == "palette") spec.UsePalette = true;
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
            else if (lut.ValueKind != JsonValueKind.Undefined) throw new FormatException("lut must be 2 to 6 colour strings, or \"palette\"");
            spec.Scale = ReadNumber(value, "scale", 1);
            spec.Contrast = ReadNumber(value, "contrast", 1);
            spec.Angle = ReadNumber(value, "angle", 0);
            spec.Seed = (int)ReadNumber(value, "seed", 0);
            spec.Octaves = (int)ReadNumber(value, "octaves", 0);
            spec.Frequency = (int)ReadNumber(value, "frequency", 0);
            spec.Strength = ReadNumber(value, "strength", 1);
            spec.Brightness = ReadNumber(value, "brightness", 0);
            spec.Saturation = ReadNumber(value, "saturation", 1);
            spec.WhiteLevel = ReadNumber(value, "white", 1);
            spec.Sharpen = ReadNumber(value, "sharpen", 0);
            spec.SharpenRadius = ReadNumber(value, "sharpenRadius", 1);
            if (value.TryGetProperty("name", out JsonElement label) && label.ValueKind == JsonValueKind.String)
                spec.Label = label.GetString() ?? "";
            if (value.TryGetProperty("resample", out JsonElement resample))
            {
                if (resample.ValueKind != JsonValueKind.String)
                    throw new FormatException("texture resample must be a string");
                spec.Resample = resample.GetString();
            }
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
            if (!string.IsNullOrEmpty(Label)) writer.WriteString("name", Label);
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
            if (Angle != 0) writer.WriteNumber("angle", Angle); // omitted at 0 so UX-011 files stay as they were
            // Everything below is omitted at its default, so older files and their output are unchanged.
            if (Seed != 0) writer.WriteNumber("seed", Seed);
            if (Octaves != 0) writer.WriteNumber("octaves", Octaves);
            if (Frequency != 0) writer.WriteNumber("frequency", Frequency);
            if (Strength != 1) writer.WriteNumber("strength", Strength);
            if (Brightness != 0) writer.WriteNumber("brightness", Brightness);
            if (Saturation != 1) writer.WriteNumber("saturation", Saturation);
            if (WhiteLevel != 1) writer.WriteNumber("white", WhiteLevel);
            if (Resample != TextureResampler.Smooth) writer.WriteString("resample", Resample);
            if (Sharpen != 0) writer.WriteNumber("sharpen", Sharpen);
            if (SharpenRadius != 1) writer.WriteNumber("sharpenRadius", SharpenRadius);
            writer.WriteEndObject();
        }

        public override string ToString() => $"{Name} ({(UsePalette ? "palette" : string.Join(" → ", Lut.Select(BrushSpec.FormatColor)))})";
    }
}
