using System;
using System.IO;
using System.Windows;
using ManagedShell.Common.Logging;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-011): one broken asset must not discard unrelated brush/Start settings.
    public static partial class StyleLayer
    {
        private static FileSystemWatcher _textureWatcher;

        public static int ApplyBrushOverrides(ResourceDictionary dictionary, StyleDocument style,
            StyleMap.Family family, Action<string> warn)
        {
            int applied = 0;
            foreach (var part in style.Parts)
                foreach (var property in part.Value)
                {
                    string name = $"{part.Key}.{property.Key}";
                    try
                    {
                        // Stage a whole property before publishing, including the Clear-role
                        // keys and the classic TaskButton style. No half-applied bad texture.
                        var pending = new ResourceDictionary();
                        int count = ApplyProperty(pending, family, name, property.Value, style.Palette);
                        foreach (object key in pending.Keys) dictionary[key] = pending[key];
                        applied += count;
                    }
                    catch (Exception e)
                    {
                        warn?.Invoke($"style property \"{name}\" not applied; using theme default: {e.Message}");
                    }
                }
            return applied;
        }

        private static void InitializeTextureWatching()
        {
            if (_textureWatcher != null) return;
            try
            {
                Directory.CreateDirectory(LutTextures.TexturesFolder);
                _textureWatcher = new FileSystemWatcher(LutTextures.TexturesFolder, "*.png")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                _textureWatcher.Changed += Watcher_Event;
                _textureWatcher.Created += Watcher_Event;
                _textureWatcher.Deleted += Watcher_Event;
                _textureWatcher.Renamed += Watcher_Event;
                _textureWatcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                ShellLogger.Warning($"UXUI-Extreme: texture file watching is off; reapply the style after changing a PNG: {e.Message}");
            }
        }
    }
}
