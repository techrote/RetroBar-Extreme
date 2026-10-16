using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ManagedShell.Common.Logging;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-003): the style layer. Custom colours and gradients over any theme.
    //
    // A style file (%LOCALAPPDATA%\RetroBar\Style.json) names parts of the taskbar and the
    // brush for each; StyleMap turns those into the current theme's resource keys, and the
    // resulting dictionary is merged after the theme so it wins. Unset parts keep the theme's
    // look. The file is watched, so saving, adding or deleting it updates the taskbar live.
    //
    // A missing file means no layer. A broken file, or a bad value in it, is logged as a
    // warning and skipped: the style layer must never stop RetroBar starting.
    //
    // Example:
    //   {
    //     "name": "Ratrod",
    //     "parts": {
    //       "bar":    { "background": { "angle": 90, "stops": [ "#4a5056", "#2b2f33" ] } },
    //       "button": { "background": "#3a3f44", "foreground": "#e8e4dc" },
    //       "clock":  { "foreground": "#ff8c2a" }
    //     }
    //   }
    //
    // A brush is a colour ("#rgb", "#rrggbb", "#aarrggbb" or a colour name) or a linear
    // gradient: { "angle": degrees (0 = left to right, 90 = top to bottom, the default),
    // "stops": [ "#colour", ... ] } with stops spaced evenly, or
    // [ { "color": "#colour", "offset": 0.0-1.0 }, ... ].
    public static class StyleLayer
    {
        public const string FILE_NAME = "Style.json";

        public static string StylePath => FILE_NAME.InLocalAppData();

        // Raised after the layer changes, for things that cache theme styles (task buttons).
        public static event EventHandler Changed;

        private static ResourceDictionary _dictionary;
        private static FileSystemWatcher _watcher;
        private static DispatcherTimer _reloadTimer;

        private static readonly JsonDocumentOptions JsonOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        public static void Initialize()
        {
            if (_watcher != null)
            {
                return;
            }

            try
            {
                string folder = Path.GetDirectoryName(StylePath);
                Directory.CreateDirectory(folder);

                _reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                _reloadTimer.Tick += (s, e) =>
                {
                    _reloadTimer.Stop();
                    Apply();
                };

                _watcher = new FileSystemWatcher(folder, FILE_NAME)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                };
                _watcher.Changed += Watcher_Event;
                _watcher.Created += Watcher_Event;
                _watcher.Deleted += Watcher_Event;
                _watcher.Renamed += Watcher_Event;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                ShellLogger.Warning($"UXUI-Extreme: style file watching is off, restart RetroBar to apply style changes: {e.Message}");
            }
        }

        private static void Watcher_Event(object sender, FileSystemEventArgs e)
        {
            // Editors save in bursts (truncate, write, rename); wait for it to settle.
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _reloadTimer.Stop();
                _reloadTimer.Start();
            });
        }

        // Rebuilds the layer for the current theme. Called after every theme load.
        public static void Apply()
        {
            try
            {
                Remove();

                ResourceDictionary dictionary = Build();
                if (dictionary != null)
                {
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                    _dictionary = dictionary;
                }
            }
            catch (Exception e)
            {
                Remove();
                ShellLogger.Warning($"UXUI-Extreme: style not applied, using the theme alone: {e.Message}");
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void Remove()
        {
            if (_dictionary != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(_dictionary);
                _dictionary = null;
            }
        }

        private static ResourceDictionary Build()
        {
            if (!File.Exists(StylePath))
            {
                return null;
            }

            string json;
            try
            {
                // Shared read so a file still open in an editor can be read.
                using var stream = new FileStream(StylePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }
            catch (IOException e)
            {
                ShellLogger.Warning($"UXUI-Extreme: could not read {StylePath}, trying again shortly: {e.Message}");
                _reloadTimer?.Start();
                return null;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json, JsonOptions);
            }
            catch (JsonException e)
            {
                ShellLogger.Warning($"UXUI-Extreme: {StylePath} is not valid JSON, using the theme alone: {e.Message}");
                return null;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    !document.RootElement.TryGetProperty("parts", out JsonElement parts) ||
                    parts.ValueKind != JsonValueKind.Object)
                {
                    ShellLogger.Warning($"UXUI-Extreme: {StylePath} has no \"parts\" object, using the theme alone.");
                    return null;
                }

                StyleMap.Family family = StyleMap.Detect();
                var dictionary = new ResourceDictionary();
                int applied = 0;

                foreach (JsonProperty part in parts.EnumerateObject())
                {
                    if (!StyleMap.PartProperties.TryGetValue(part.Name, out string[] properties) || part.Value.ValueKind != JsonValueKind.Object)
                    {
                        ShellLogger.Warning($"UXUI-Extreme: style part \"{part.Name}\" is unknown, skipped. Known parts: {string.Join(", ", StyleMap.Parts)}.");
                        continue;
                    }

                    foreach (JsonProperty property in part.Value.EnumerateObject())
                    {
                        string name = $"{part.Name}.{property.Name}";

                        if (Array.IndexOf(properties, property.Name) < 0)
                        {
                            ShellLogger.Warning($"UXUI-Extreme: style property \"{name}\" is unknown, skipped. \"{part.Name}\" has: {string.Join(", ", properties)}.");
                            continue;
                        }

                        Fill fill;
                        try
                        {
                            fill = Fill.Parse(property.Value);
                        }
                        catch (Exception e)
                        {
                            ShellLogger.Warning($"UXUI-Extreme: style value for \"{name}\" skipped: {e.Message}");
                            continue;
                        }

                        applied += ApplyProperty(dictionary, family, name, fill);
                    }
                }

                string styleName = document.RootElement.TryGetProperty("name", out JsonElement n) && n.ValueKind == JsonValueKind.String ? n.GetString() : FILE_NAME;
                ShellLogger.Info($"UXUI-Extreme: style \"{styleName}\" applied over the {family} theme family ({applied} resources).");
                return dictionary;
            }
        }

        private static int ApplyProperty(ResourceDictionary dictionary, StyleMap.Family family, string name, Fill fill)
        {
            int count = 0;

            if (name == StyleMap.TaskButtonStyleBackground && family != StyleMap.Family.Modern &&
                Application.Current.TryFindResource("TaskButton") is Style baseStyle)
            {
                var style = new Style(typeof(Button), baseStyle);
                style.Setters.Add(new Setter(Control.BackgroundProperty, fill.ToBrush(0)));
                style.Seal();
                dictionary["TaskButton"] = style;
                count++;
            }

            foreach (StyleMap.Target target in StyleMap.Targets(family, name))
            {
                // Modern themes don't all define every key; only replace the ones in use.
                if (family == StyleMap.Family.Modern && Application.Current.TryFindResource(target.Key) == null)
                {
                    continue;
                }

                dictionary[target.Key] = target.Role switch
                {
                    StyleMap.Role.Clear => Brushes.Transparent,
                    StyleMap.Role.FillVertical => fill.ToBrush(-90),
                    _ => fill.ToBrush(0),
                };
                count++;
            }

            if (count == 0)
            {
                ShellLogger.Info($"UXUI-Extreme: style property \"{name}\" isn't used by the {family} theme family.");
            }

            return count;
        }

        // A colour or linear gradient from a style file.
        private class Fill
        {
            private Color _color;
            private GradientStopCollection _stops;
            private double _angle = 90;

            public static Fill Parse(JsonElement value)
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return new Fill { _color = ParseColor(value.GetString()) };
                }

                if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("stops", out JsonElement stops) || stops.ValueKind != JsonValueKind.Array)
                {
                    throw new FormatException("expected a colour such as \"#3a3f44\", or a gradient { \"angle\": 90, \"stops\": [ \"#colour\", \"#colour\" ] }");
                }

                var fill = new Fill { _stops = new GradientStopCollection() };

                if (value.TryGetProperty("angle", out JsonElement angle))
                {
                    fill._angle = angle.GetDouble();
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
                        fill._stops.Add(new GradientStop(ParseColor(stop.GetString()), evenOffset));
                    }
                    else if (stop.ValueKind == JsonValueKind.Object && stop.TryGetProperty("color", out JsonElement color))
                    {
                        double offset = stop.TryGetProperty("offset", out JsonElement o) ? o.GetDouble() : evenOffset;
                        fill._stops.Add(new GradientStop(ParseColor(color.GetString()), Math.Clamp(offset, 0, 1)));
                    }
                    else
                    {
                        throw new FormatException("a gradient stop is a colour, or { \"color\": \"#colour\", \"offset\": 0.5 }");
                    }

                    i++;
                }

                fill._stops.Freeze();
                return fill;
            }

            private static Color ParseColor(string text)
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

            // rotate: extra degrees, for backgrounds drawn on a vertical taskbar.
            public Brush ToBrush(double rotate)
            {
                Brush brush;

                if (_stops == null)
                {
                    brush = new SolidColorBrush(_color);
                }
                else
                {
                    // Centre the gradient line in the box so every angle spans it end to end.
                    double radians = (_angle + rotate) * Math.PI / 180;
                    double dx = Math.Cos(radians) / 2, dy = Math.Sin(radians) / 2;
                    brush = new LinearGradientBrush(_stops, new Point(0.5 - dx, 0.5 - dy), new Point(0.5 + dx, 0.5 + dy));
                }

                brush.Freeze();
                return brush;
            }
        }
    }
}
