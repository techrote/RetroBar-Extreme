using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-010): optional top-level "start": { "recolour": "#8c24ff" }.
    public partial class StyleDocument
    {
        private Color? _startRecolour;
        public Color? StartRecolour
        {
            get => _startRecolour;
            // Target alpha has no meaning; coverage always comes from the original sprite.
            set => _startRecolour = value is Color c ? Color.FromRgb(c.R, c.G, c.B) : null;
        }

        private static IEnumerable<JsonProperty> ReadParts(JsonElement root)
        {
            if (root.TryGetProperty("parts", out JsonElement parts))
            {
                if (parts.ValueKind != JsonValueKind.Object) throw new FormatException("no \"parts\" object");
                return parts.EnumerateObject();
            }
            // A Start-only style is useful; old brush-only styles remain unchanged.
            if (root.TryGetProperty("start", out JsonElement start) && start.ValueKind == JsonValueKind.Object)
                return Array.Empty<JsonProperty>();
            throw new FormatException("no \"parts\" or \"start\" object");
        }

        private void ReadStart(JsonElement root, Action<string> warn)
        {
            if (!root.TryGetProperty("start", out JsonElement start)) return;
            if (start.ValueKind != JsonValueKind.Object)
            {
                warn?.Invoke("style part \"start\" must be an object; skipped.");
                return;
            }
            foreach (JsonProperty property in start.EnumerateObject())
            {
                if (property.Name != "recolour")
                {
                    warn?.Invoke($"style property \"start.{property.Name}\" is unknown; skipped. Use \"recolour\".");
                    continue;
                }
                if (property.Value.ValueKind == JsonValueKind.Null) { StartRecolour = null; continue; }
                if (property.Value.ValueKind == JsonValueKind.String &&
                    BrushSpec.TryParseColor(property.Value.GetString(), out Color colour))
                    StartRecolour = colour;
                else
                    warn?.Invoke("style value for \"start.recolour\" skipped: expected a colour such as \"#8c24ff\" (not a gradient).");
            }
        }

        private void WriteStart(Utf8JsonWriter writer)
        {
            if (StartRecolour is not Color colour) return;
            writer.WriteStartObject("start");
            writer.WriteString("recolour", BrushSpec.FormatColor(colour));
            writer.WriteEndObject();
        }
    }
}
