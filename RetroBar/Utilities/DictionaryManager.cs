using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace RetroBar.Utilities
{
    public class DictionaryManager : IDisposable
    {
        private const string DICT_DEFAULT = "System";
        private const string DICT_EXT = "xaml";

        private const string LANG_DEFAULT = DICT_DEFAULT;
        private const string LANG_FALLBACK = "English";
        private const string LANG_FOLDER = "Languages";
        private const string LANG_EXT = DICT_EXT;

        public const string THEME_DEFAULT = DICT_DEFAULT;
        private const string THEME_FOLDER = "Themes";
        private const string THEME_EXT = DICT_EXT;

        public DictionaryManager()
        {
            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        public void SetThemeFromSettings()
        {
            StyleLayer.ReadBordersForTheme(); // UXUI-Extreme (UX-013): borders are baked in as the theme loads

            SetTheme(THEME_DEFAULT);

            if (Settings.Instance.Theme.StartsWith(THEME_DEFAULT))
            {
                SetSystemThemeParams();
            }
            else
            {
                ClearSystemThemeParams();
            }

            if (Settings.Instance.Theme != THEME_DEFAULT)
            {
                SetTheme(Settings.Instance.Theme);
            }

            StyleLayer.Apply(); // UXUI-Extreme (UX-003): custom colours go over the theme just loaded
        }

        private void SetSystemThemeParams()
        {
            Application.Current.Resources["GlobalFontFamily"] = SystemFonts.CaptionFontFamily;
        }

        private void ClearSystemThemeParams()
        {
            Application.Current.Resources.Remove("GlobalFontFamily");
        }

        private void SetTheme(string theme)
        {
            SetDictionary(theme, THEME_FOLDER, THEME_DEFAULT, THEME_EXT, 0);
        }

        private static Collection<ResourceDictionary> GetMergedDictionaries()
        {
            return Application.Current.Resources.MergedDictionaries;
        }

        private static ResourceDictionary GetActualThemeDictionary()
        {
            foreach (ResourceDictionary rd in GetMergedDictionaries()
                .Where(rd => rd.Source != null && rd.Source.ToString().Contains($"{THEME_FOLDER}/"))) // UXUI-Extreme (UX-006): scaled themes have no Source
            {
                return rd;
            }

            return null;
        }

        private void ClearPreviousThemes()
        {
            if (GetActualThemeDictionary() != null)
            {
                _ = GetMergedDictionaries().Remove(GetActualThemeDictionary());
            }

            // UXUI-Extreme (UX-006): scaled themes have no Source, so they are tracked separately.
            // When native sizing is in use, also drop any stretch-mode (Source-loaded) theme
            // dictionaries so switching modes never leaves an old theme merged.
            foreach (ResourceDictionary scaled in _scaledThemeDictionaries)
            {
                GetMergedDictionaries().Remove(scaled);
            }
            _scaledThemeDictionaries.Clear();

            if (ScaleModel.IsNative || StyleLayer.HasBorders) // UX-013: rewritten in stretch mode too
            {
                ResourceDictionary stale;
                while ((stale = GetActualThemeDictionary()) != null)
                {
                    GetMergedDictionaries().Remove(stale);
                }
            }
        }

        // UXUI-Extreme (UX-006): native sizing — load the theme with its sizes rewritten for the
        // current scale (ThemeScaler). Returns false (caller falls back to a normal load) on any
        // problem, so RetroBar always starts.
        private readonly List<ResourceDictionary> _scaledThemeDictionaries = new List<ResourceDictionary>();
        private const string SYSTEM_THEME_RESOURCE = "RetroBar.Themes.System.xaml";

        private bool TryAddScaledTheme(string dictionary, string dictDefault, string dictFilePath)
        {
            double scale = ScaleModel.ThemeScale;
            StyleDocument borders = StyleLayer.LoadedBorders; // UXUI-Extreme (UX-013): per part, from Style.json

            if (scale == 1.0 && !StyleLayer.HasBorders)
            {
                return false;
            }

            try
            {
                string xaml;

                if (dictionary == dictDefault)
                {
                    using Stream stream = typeof(DictionaryManager).Assembly.GetManifestResourceStream(SYSTEM_THEME_RESOURCE);
                    if (stream == null)
                    {
                        ManagedShell.Common.Logging.ShellLogger.Warning($"UXUI-Extreme: embedded {SYSTEM_THEME_RESOURCE} not found; using stretch scaling.");
                        return false;
                    }
                    using StreamReader reader = new StreamReader(stream);
                    xaml = reader.ReadToEnd();
                }
                else
                {
                    xaml = File.ReadAllText(dictFilePath);
                }

                // UX-013: pull in included base themes so the rewrites reach their templates too, and
                // parse relative to the theme file so its images and anything not inlined still resolve.
                string folder = dictionary == dictDefault ? null : Path.GetDirectoryName(Path.GetFullPath(dictFilePath));
                string scaledXaml = ThemeBorders.InlineIncluded(xaml, folder);
                if (scale != 1.0)
                {
                    scaledXaml = ThemeScaler.Scale(scaledXaml, scale);
                }
                if (StyleLayer.HasBorders)
                {
                    scaledXaml = ThemeBorders.ScaleBorders(scaledXaml, borders.BorderFactor);
                }
                var context = new System.Windows.Markup.ParserContext();
                if (folder != null)
                {
                    context.BaseUri = new Uri(Path.GetFullPath(dictFilePath), UriKind.Absolute);
                }
                var scaledDictionary = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(scaledXaml, context);
                GetMergedDictionaries().Add(scaledDictionary);
                _scaledThemeDictionaries.Add(scaledDictionary);
                return true;
            }
            catch (Exception e)
            {
                ManagedShell.Common.Logging.ShellLogger.Error($"UXUI-Extreme: could not load scaled theme '{dictionary}', using stretch scaling: {e.Message} {e.InnerException?.Message}");
                return false;
            }
        }

        public void SetLanguageFromSettings()
        {
            SetLanguage(LANG_FALLBACK);
            if (Settings.Instance.Language == LANG_DEFAULT)
            {
                var currentUICulture = System.Globalization.CultureInfo.CurrentUICulture;
                string systemLanguageParent = currentUICulture.Parent.NativeName;
                string systemLanguage = currentUICulture.NativeName;
                ManagedShell.Common.Logging.ShellLogger.Info
                    ($"Loading system language (if available): {systemLanguageParent}, {systemLanguage}");
                SetLanguage(systemLanguageParent);
                SetLanguage(systemLanguage);
            }
            else
            {
                SetLanguage(Settings.Instance.Language);
            }
        }

        private void SetLanguage(string language)
        {
            SetDictionary(language, LANG_FOLDER, LANG_FALLBACK, LANG_EXT, 1);
        }

        private void SetDictionary(string dictionary, string dictFolder, string dictDefault, string dictExtension, int dictType)
        {
            string dictFilePath;

            if (dictionary == dictDefault)
            {
                if (dictType == 0)
                {
                    ClearPreviousThemes();
                }
                dictFilePath = Path.ChangeExtension(Path.Combine(dictFolder, dictDefault), dictExtension);
            }
            else
            {
                // Built-in dictionary
                dictFilePath = Path.ChangeExtension(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dictFolder, dictionary),
                                                    dictExtension);

                if (!File.Exists(dictFilePath))
                {
                    // Installed dictionary in AppData directory
                    dictFilePath = Path.ChangeExtension(dictFolder.InLocalAppData(dictionary), dictExtension);

                    if (!File.Exists(dictFilePath))
                    {
                        // Custom dictionary in app directory
                        dictFilePath =
                            Path.ChangeExtension(Path.Combine(Path.GetDirectoryName(ExePath.GetExecutablePath()), dictFolder, dictionary),
                                                 dictExtension);

                        if (!File.Exists(dictFilePath))
                        {
                            return;
                        }
                    }
                }
            }

            // UXUI-Extreme (UX-006): native sizing for themes
            // UXUI-Extreme (UX-013): also when borders are thickened or removed, in either scaling mode
            if (dictType == 0 && (ScaleModel.IsNative || StyleLayer.HasBorders) && TryAddScaledTheme(dictionary, dictDefault, dictFilePath))
            {
                return;
            }

            try
            {
                GetMergedDictionaries().Add(new ResourceDictionary()
                {
                    Source = new Uri(dictFilePath, UriKind.RelativeOrAbsolute)
                });
            }
            catch (Exception e)
            {
                ManagedShell.Common.Logging.ShellLogger.Error($"Error loading dictionaries: {e.Message} {e.InnerException?.Message}");
            }
        }

        public string GetThemeInstallDir()
        {
            return THEME_FOLDER.InLocalAppData();
        }

        public List<string> GetThemes()
        {
            return GetDictionaries(THEME_DEFAULT, THEME_FOLDER, THEME_EXT);
        }

        public List<string> GetLanguages()
        {
            List<string> languages = new List<string> { LANG_DEFAULT };
            languages.AddRange(GetDictionaries(LANG_FALLBACK, LANG_FOLDER, LANG_EXT));
            return languages;
        }

        private List<string> GetDictionaries(string dictDefault, string dictFolder, string dictExtension)
        {
            List<string> dictionaries = new List<string> { dictDefault };

            // Built-in dictionaries
            dictionaries.AddFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dictFolder), dictExtension);

            // Installed AppData dictionaries
            dictionaries.AddFrom(dictFolder.InLocalAppData(), dictExtension);

            // Same-folder dictionaries
            // Because RetroBar is published as a single-file app, it gets extracted to a temp directory, so custom dictionaries won't be there.
            // Get the executable path to find the custom dictionaries directory when not a debug build.
            dictionaries.AddFrom(Path.Combine(Path.GetDirectoryName(ExePath.GetExecutablePath()), dictFolder), dictExtension, true);

            return dictionaries;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.Language))
            {
                SetLanguageFromSettings();
            }
            if (e.PropertyName == nameof(Settings.Theme))
            {
                SetThemeFromSettings();
            }
        }

        public void Dispose()
        {
            Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
        }
    }
}