using System;
using System.IO;
using System.Linq;
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
    // UXUI-Extreme (UX-011): textures use { "texture": "brushed", "lut": ["#222", "#888"] }.
    public static partial class StyleLayer // UXUI-Extreme (UX-010): original-resource lookup in StyleLayer.Start.cs.
    {
        public const string FILE_NAME = "Style.json";

        public static string StylePath => FILE_NAME.InLocalAppData();

        // Raised after the layer changes, for things that cache theme styles (task buttons).
        public static event EventHandler Changed;

        private static ResourceDictionary _dictionary;
        private static FileSystemWatcher _watcher;
        private static DispatcherTimer _reloadTimer;

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
            if (_reloadTimer != null) InitializeTextureWatching(); // UXUI-Extreme (UX-011)
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
            CheckBorders(); // UXUI-Extreme (UX-013)
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

            StyleDocument style;
            try
            {
                style = StyleDocument.Load(StylePath, warning => ShellLogger.Warning($"UXUI-Extreme: {warning}"));
            }
            catch (IOException e)
            {
                ShellLogger.Warning($"UXUI-Extreme: could not read {StylePath}, trying again shortly: {e.Message}");
                _reloadTimer?.Start();
                return null;
            }
            catch (FormatException e)
            {
                ShellLogger.Warning($"UXUI-Extreme: {StylePath} is {e.Message}, using the theme alone.");
                return null;
            }

            StyleMap.Family family = StyleMap.Detect();
            var dictionary = new ResourceDictionary();
            // UXUI-Extreme (UX-011): resolve palette references, isolating failures by property.
            int applied = ApplyBrushOverrides(dictionary, style, family,
                warning => ShellLogger.Warning($"UXUI-Extreme: {warning}"));

            // UXUI-Extreme (UX-010): bitmap overrides sit beside the brush overrides.
            applied += StartButtonColourways.Apply(dictionary, style.StartRecolour, ThemeResource,
                warning => ShellLogger.Warning($"UXUI-Extreme: {warning}"));

            ShellLogger.Info($"UXUI-Extreme: style \"{(string.IsNullOrEmpty(style.Name) ? FILE_NAME : style.Name)}\" applied over the {family} theme family ({applied} resources).");
            return dictionary;
        }

        private static int ApplyProperty(ResourceDictionary dictionary, StyleMap.Family family, string name, BrushSpec fill, Color?[] palette)
        {
            int count = 0;
            // UXUI-Extreme (UX-011): a property shares one immutable brush per orientation.
            Brush horizontal = null, vertical = null;
            Brush normal() => horizontal ??= fill.ToBrush(palette: palette);

            if (name == StyleMap.TaskButtonStyleBackground && family != StyleMap.Family.Modern &&
                Application.Current.TryFindResource("TaskButton") is Style baseStyle)
            {
                var style = new Style(typeof(Button), baseStyle);
                style.Setters.Add(new Setter(Control.BackgroundProperty, normal()));
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

                // UXUI-Extreme (UX-011): even an overlay-only custom theme must validate
                // its texture before clearing anything. No Fill target may exist here.
                if (target.Role == StyleMap.Role.Clear && fill.IsTexture) _ = normal();

                dictionary[target.Key] = target.Role switch
                {
                    StyleMap.Role.Clear => Brushes.Transparent,
                    StyleMap.Role.FillVertical => vertical ??= fill.ToBrush(-90, palette),
                    _ => normal(),
                };
                count++;
            }

            if (count == 0)
            {
                ShellLogger.Info($"UXUI-Extreme: style property \"{name}\" isn't used by the {family} theme family.");
            }

            return count;
        }

        // UX-004: what the theme alone draws a part with (ignoring the layer), so the style
        // editor can start from it. Null if the part isn't drawn by a brush in this theme.
        public static Brush ThemeBrush(string part, string property)
        {
            string name = $"{part}.{property}";
            StyleMap.Family family = StyleMap.Detect();

            var keys = StyleMap.Targets(family, name).Where(t => t.Role == StyleMap.Role.Fill).Select(t => t.Key).ToList();
            if (name == StyleMap.TaskButtonStyleBackground && family != StyleMap.Family.Modern)
            {
                keys.Insert(0, "ButtonFace");
            }

            var dictionaries = Application.Current.Resources.MergedDictionaries;
            foreach (string key in keys)
            {
                for (int i = dictionaries.Count - 1; i >= 0; i--)
                {
                    if (dictionaries[i] != _dictionary && dictionaries[i][key] is Brush brush)
                    {
                        return brush;
                    }
                }
            }

            return null;
        }

        // UX-004: whether the current theme draws anything with a part property, so the editor
        // can say when a setting has no effect here.
        public static bool IsUsed(string part, string property)
        {
            string name = $"{part}.{property}";
            StyleMap.Family family = StyleMap.Detect();

            if (name == StyleMap.TaskButtonStyleBackground && family != StyleMap.Family.Modern)
            {
                return true;
            }

            return StyleMap.Targets(family, name).Any(t => family != StyleMap.Family.Modern || ThemeHasKey(t.Key));
        }

        private static bool ThemeHasKey(string key)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            for (int i = dictionaries.Count - 1; i >= 0; i--)
            {
                if (dictionaries[i] != _dictionary && dictionaries[i][key] != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
