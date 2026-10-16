using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-013): border thickness per part, as a whole-number multiplier of the
    // theme's own borders (0 = none, 1 = theme, up to 4), in a top-level "borders" object:
    //   "borders": { "bar": 2, "button": 0, "start": 3 }
    // Applied when the theme loads (ThemeBorders), not by the style layer's brushes.
    public partial class StyleDocument
    {
        public const double MaxBorderFactor = 4;

        // The parts borders can be set for, in the order the Colours tab lists them.
        public static readonly string[] BorderParts = { "bar", "tray", "button", "buttonActive", "buttonFlashing", "clock", "start" };

        public readonly Dictionary<string, double> Borders = new Dictionary<string, double>();

        public double BorderFactor(string part) => Borders.TryGetValue(part, out double factor) ? factor : 1;

        public void SetBorderFactor(string part, double factor)
        {
            if (factor == 1)
            {
                Borders.Remove(part);
            }
            else
            {
                Borders[part] = factor;
            }
        }

        private void ReadBorders(JsonElement root, Action<string> warn)
        {
            if (!root.TryGetProperty("borders", out JsonElement borders))
            {
                return;
            }

            if (borders.ValueKind != JsonValueKind.Object)
            {
                warn?.Invoke("style \"borders\" must be an object such as { \"bar\": 2 }; skipped.");
                return;
            }

            foreach (JsonProperty border in borders.EnumerateObject())
            {
                if (Array.IndexOf(BorderParts, border.Name) < 0)
                {
                    warn?.Invoke($"border part \"{border.Name}\" is unknown, skipped. Known: {string.Join(", ", BorderParts)}.");
                    continue;
                }

                if (border.Value.ValueKind != JsonValueKind.Number || !border.Value.TryGetDouble(out double factor) ||
                    !double.IsFinite(factor) || factor < 0 || factor > MaxBorderFactor || factor != Math.Round(factor))
                {
                    warn?.Invoke($"border for \"{border.Name}\" skipped: use a whole number from 0 (none) to {MaxBorderFactor}.");
                    continue;
                }

                SetBorderFactor(border.Name, factor);
            }
        }

        private void WriteBorders(Utf8JsonWriter writer)
        {
            if (Borders.Count == 0)
            {
                return;
            }

            writer.WriteStartObject("borders");
            foreach (string part in BorderParts.Where(Borders.ContainsKey))
            {
                writer.WriteNumber(part, Borders[part]);
            }
            writer.WriteEndObject();
        }

        // Same borders as another style? (Changing them needs the theme reloaded.)
        public bool SameBorders(StyleDocument other)
        {
            var mine = Borders;
            var theirs = other?.Borders ?? new Dictionary<string, double>();
            return mine.Count == theirs.Count && mine.All(b => theirs.TryGetValue(b.Key, out double v) && v == b.Value);
        }
    }
}
