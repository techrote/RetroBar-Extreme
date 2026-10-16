using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using ManagedShell.Common.Logging;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-015): user-supplied PNG icons for the Start button and quick launch items.
    // Chosen files are copied into %LOCALAPPDATA%\RetroBar\Icons\ so they keep working if the
    // original moves; settings store only the copied file's name. A missing or unreadable icon
    // falls back to the theme's / shortcut's own icon.
    public static class CustomIcons
    {
        public const int MaxBytes = 4 * 1024 * 1024, MaxDimension = 512;

        public static string Folder => "Icons".InLocalAppData();

        private static readonly Dictionary<string, BitmapSource> Cache = new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        // Copies a PNG into the icons folder; returns the stored name, or throws with a reason.
        public static string Import(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                throw new IOException("the file doesn't exist");
            }
            if (!string.Equals(info.Extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("choose a .png file");
            }
            if (info.Length > MaxBytes)
            {
                throw new FormatException("the PNG is larger than 4 MB");
            }

            BitmapSource image = Decode(File.ReadAllBytes(path)); // validates before copying
            if (image.PixelWidth > MaxDimension || image.PixelHeight > MaxDimension)
            {
                throw new FormatException($"the PNG is larger than {MaxDimension}×{MaxDimension} pixels");
            }

            Directory.CreateDirectory(Folder);
            string stem = string.Concat(Path.GetFileNameWithoutExtension(path).Where(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) < 0));
            if (string.IsNullOrWhiteSpace(stem))
            {
                stem = "icon";
            }

            string name = stem + ".png";
            for (int i = 2; File.Exists(Path.Combine(Folder, name)) && !SameFile(Path.Combine(Folder, name), path); i++)
            {
                name = $"{stem} ({i}).png";
            }

            string target = Path.Combine(Folder, name);
            if (!SameFile(target, path))
            {
                File.Copy(path, target, true);
            }

            lock (Cache)
            {
                Cache.Remove(name);
            }
            return name;
        }

        // The stored icon, or null (no icon set, or it can't be read).
        public static BitmapSource Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
            {
                return null;
            }

            lock (Cache)
            {
                if (Cache.TryGetValue(name, out BitmapSource cached))
                {
                    return cached;
                }
            }

            try
            {
                string path = Path.Combine(Folder, name);
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > MaxBytes)
                {
                    return null;
                }

                BitmapSource image = Decode(File.ReadAllBytes(path));
                lock (Cache)
                {
                    Cache[name] = image;
                }
                return image;
            }
            catch (Exception e)
            {
                ShellLogger.Warning($"UXUI-Extreme: custom icon \"{name}\" can't be used, keeping the original: {e.Message}");
                return null;
            }
        }

        private static BitmapSource Decode(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, false);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapSource frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }

        private static bool SameFile(string a, string b) =>
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

        // Quick launch items are remembered by shortcut file name (e.g. "firefox.lnk").
        public static string QuickLaunchKey(string itemPath) =>
            string.IsNullOrEmpty(itemPath) ? null : Path.GetFileName(itemPath).ToLowerInvariant();

        public static string QuickLaunchIcon(string itemPath)
        {
            string key = QuickLaunchKey(itemPath);
            return key == null ? null : Settings.Instance.QuickLaunchIcons?.FirstOrDefault(i => i.Item == key)?.Icon;
        }

        public static void SetQuickLaunchIcon(string itemPath, string icon)
        {
            string key = QuickLaunchKey(itemPath);
            if (key == null)
            {
                return;
            }

            var list = (Settings.Instance.QuickLaunchIcons ?? new List<QuickLaunchIconSetting>())
                .Where(i => i.Item != key)
                .Select(i => new QuickLaunchIconSetting { Item = i.Item, Icon = i.Icon })
                .ToList();
            if (!string.IsNullOrEmpty(icon))
            {
                list.Add(new QuickLaunchIconSetting { Item = key, Icon = icon });
            }
            Settings.Instance.QuickLaunchIcons = list;
        }
    }
}
