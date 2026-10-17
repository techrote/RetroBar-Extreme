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

            ImportSizes(path, Path.GetFileNameWithoutExtension(name));

            lock (Cache)
            {
                foreach (string key in Cache.Keys.Where(k => k.StartsWith(Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    Cache.Remove(key);
                }
            }
            return name;
        }

        // Icon sets: a PNG in a folder named like "16px", with sibling "24px", "32px"... folders
        // holding the same file name, brings every size along (as "name@24.png" etc.), so the
        // sharpest one can be used for the taskbar's scale.
        private static void ImportSizes(string path, string storedStem)
        {
            try
            {
                string folder = Path.GetDirectoryName(path);
                string parent = Path.GetDirectoryName(folder);
                if (parent == null || SizeOf(Path.GetFileName(folder)) == 0)
                {
                    return;
                }

                foreach (string sibling in Directory.GetDirectories(parent))
                {
                    int size = SizeOf(Path.GetFileName(sibling));
                    string candidate = Path.Combine(sibling, Path.GetFileName(path));
                    if (size > 0 && File.Exists(candidate) && new FileInfo(candidate).Length <= MaxBytes)
                    {
                        Decode(File.ReadAllBytes(candidate)); // only valid PNGs
                        File.Copy(candidate, Path.Combine(Folder, $"{storedStem}@{size}.png"), true);
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException || e is NotSupportedException)
            {
                ShellLogger.Warning($"UXUI-Extreme: other sizes of icon \"{storedStem}\" not imported: {e.Message}");
            }
        }

        // "24px" -> 24, anything else -> 0
        private static int SizeOf(string folderName)
        {
            if (folderName != null && folderName.EndsWith("px", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(folderName.Substring(0, folderName.Length - 2), out int size) && size > 0 && size <= MaxDimension)
            {
                return size;
            }
            return 0;
        }

        // The stored icon in the size closest to (at least) pixelSize when an icon set was
        // imported; otherwise the single stored PNG.
        public static BitmapSource Load(string name, double pixelSize)
        {
            if (string.IsNullOrWhiteSpace(name) || pixelSize <= 0 || !Directory.Exists(Folder))
            {
                return Load(name);
            }

            string stem = Path.GetFileNameWithoutExtension(name);
            var sizes = Directory.GetFiles(Folder, stem + "@*.png")
                .Select(f => (file: Path.GetFileName(f), size: int.TryParse(Path.GetFileNameWithoutExtension(f).Substring(stem.Length + 1), out int s) ? s : 0))
                .Where(v => v.size > 0)
                .OrderBy(v => v.size)
                .ToList();
            if (sizes.Count == 0)
            {
                return Load(name);
            }

            var best = sizes.FirstOrDefault(v => v.size >= Math.Round(pixelSize));
            return Load(best.file ?? sizes.Last().file) ?? Load(name);
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

        // How many device pixels an icon image is drawn at (theme size, taskbar scale and DPI).
        public static double DevicePixels(System.Windows.FrameworkElement image)
        {
            double width = (double)image.GetValue(System.Windows.FrameworkElement.WidthProperty);
            if (double.IsNaN(width) || width <= 0)
            {
                width = 16;
            }
            double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(image).DpiScaleX;
            return width * dpi * ScaleModel.TransformScale;
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

        // Notification area icons are remembered by the identifier the show/hide behaviour uses.
        public static string TrayIcon(string identifier) =>
            string.IsNullOrEmpty(identifier) ? null : Settings.Instance.TrayIconReplacements?.FirstOrDefault(i => i.Identifier == identifier)?.Icon;

        public static void SetTrayIcon(string identifier, string title, string icon)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return;
            }

            var list = (Settings.Instance.TrayIconReplacements ?? new List<TrayIconReplacementSetting>())
                .Where(i => i.Identifier != identifier)
                .Select(i => new TrayIconReplacementSetting { Identifier = i.Identifier, Title = i.Title, Icon = i.Icon })
                .ToList();
            if (!string.IsNullOrEmpty(icon))
            {
                list.Add(new TrayIconReplacementSetting { Identifier = identifier, Title = title ?? "", Icon = icon });
            }
            Settings.Instance.TrayIconReplacements = list;
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
