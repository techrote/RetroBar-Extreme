using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: owns how every piece of text on a taskbar is drawn.
    //
    // UX-002 — sharp scaled text. RetroBar scales the whole taskbar with one LayoutTransform.
    // With font smoothing off, text is rasterised at 100% and then stretched, so at in-between
    // scales some pixel rows/columns are doubled and others aren't. When enabled, each TextBlock
    // gets its font size multiplied by the scale (optionally rounded to whole pixels) plus a
    // counter-transform of 1/scale, so the text is drawn at its real final size while boxes,
    // borders and icons keep scaling as before.
    //
    // UX-005 — font settings over any theme: family, size (px at 100%), weight, italic.
    //
    // The first time a TextBlock is touched, its original values are recorded. Whenever a
    // relevant setting or the theme changes, everything is restored first and then re-applied,
    // so "Off" / "Theme default" is exactly stock behaviour.
    public static class TaskbarTextStyler
    {
        private class Original
        {
            public object FontFamilyLocal, FontSizeLocal, FontWeightLocal, FontStyleLocal, LayoutTransformLocal;
            public double BaseFontSize;
            public bool ThemeBold;
            // Which properties we currently override, so we only restore what we changed.
            public bool FamilySet, SizeSet, WeightSet, StyleSet, TransformSet;
        }

        private static readonly DependencyProperty OriginalProperty =
            DependencyProperty.RegisterAttached("UxuiOriginal", typeof(Original), typeof(TaskbarTextStyler), new PropertyMetadata(null));

        private static readonly HashSet<string> WatchedSettings = new HashSet<string>
        {
            nameof(Settings.PixelTextMode),
            nameof(Settings.TaskbarScale),
            nameof(Settings.AllowFontSmoothing),
            nameof(Settings.Theme),
            nameof(Settings.FontOverrideFamily),
            nameof(Settings.FontOverrideSize),
            nameof(Settings.FontOverrideWeight),
            nameof(Settings.FontOverrideItalic),
            nameof(Settings.ScalingMode),
        };

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            // WPF only sends Loaded to elements that registered their own Loaded handler, so a
            // class handler alone misses text inside newly created task buttons. SizeChanged is
            // raised for every element on its first layout (and when its text changes size), so
            // listen to both. Apply() is idempotent, so repeat calls are cheap no-ops.
            EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.LoadedEvent, new RoutedEventHandler(TextBlock_Event));
            EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.SizeChangedEvent, new SizeChangedEventHandler(TextBlock_Event));
            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        private static void TextBlock_Event(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                Apply(textBlock);
            }
        }

        private static void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (WatchedSettings.Contains(e.PropertyName))
            {
                ReapplyAll();
            }
        }

        private static bool IsPixelTextActive(out double scale)
        {
            scale = Settings.Instance.TaskbarScale;
            return !ScaleModel.IsNative && // UX-006: native sizing needs no counter-scale
                   Settings.Instance.PixelTextMode != PixelTextOption.Off &&
                   !Settings.Instance.AllowFontSmoothing &&
                   scale > 1.0;
        }

        private static bool IsFontOverrideActive()
        {
            var s = Settings.Instance;
            return !string.IsNullOrWhiteSpace(s.FontOverrideFamily) ||
                   s.FontOverrideSize > 0 ||
                   s.FontOverrideWeight > 0 ||
                   s.FontOverrideItalic;
        }

        private static void Apply(TextBlock textBlock)
        {
            bool pixelText = IsPixelTextActive(out double scale);
            Original original = (Original)textBlock.GetValue(OriginalProperty);

            bool nativeSizing = ScaleModel.IsNative; // UX-006

            if (!pixelText && !nativeSizing && !IsFontOverrideActive())
            {
                if (original != null)
                {
                    Restore(textBlock);
                }
                return;
            }

            if (Window.GetWindow(textBlock) is not Taskbar)
            {
                return;
            }

            if (original == null)
            {
                original = new Original
                {
                    FontFamilyLocal = textBlock.ReadLocalValue(TextBlock.FontFamilyProperty),
                    FontSizeLocal = textBlock.ReadLocalValue(TextBlock.FontSizeProperty),
                    FontWeightLocal = textBlock.ReadLocalValue(TextBlock.FontWeightProperty),
                    FontStyleLocal = textBlock.ReadLocalValue(TextBlock.FontStyleProperty),
                    LayoutTransformLocal = textBlock.ReadLocalValue(FrameworkElement.LayoutTransformProperty),
                    BaseFontSize = textBlock.FontSize,
                    ThemeBold = textBlock.FontWeight.ToOpenTypeWeight() >= FontWeights.SemiBold.ToOpenTypeWeight(),
                };
                textBlock.SetValue(OriginalProperty, original);
            }

            var s = Settings.Instance;

            // Family
            if (!string.IsNullOrWhiteSpace(s.FontOverrideFamily))
            {
                if (!original.FamilySet || textBlock.FontFamily.Source != s.FontOverrideFamily)
                {
                    textBlock.FontFamily = new FontFamily(s.FontOverrideFamily);
                    original.FamilySet = true;
                }
            }
            else if (original.FamilySet)
            {
                RestoreValue(textBlock, TextBlock.FontFamilyProperty, original.FontFamilyLocal);
                original.FamilySet = false;
            }

            // Weight: text the theme made bold (e.g. "Start") stays at least bold.
            if (s.FontOverrideWeight > 0)
            {
                int weight = Math.Clamp(s.FontOverrideWeight, 1, 999);
                if (original.ThemeBold)
                {
                    weight = Math.Max(weight, FontWeights.Bold.ToOpenTypeWeight());
                }
                FontWeight fontWeight = FontWeight.FromOpenTypeWeight(weight);
                if (!original.WeightSet || textBlock.FontWeight != fontWeight)
                {
                    textBlock.FontWeight = fontWeight;
                    original.WeightSet = true;
                }
            }
            else if (original.WeightSet)
            {
                RestoreValue(textBlock, TextBlock.FontWeightProperty, original.FontWeightLocal);
                original.WeightSet = false;
            }

            // Italic
            if (s.FontOverrideItalic)
            {
                if (!original.StyleSet || textBlock.FontStyle != FontStyles.Italic)
                {
                    textBlock.FontStyle = FontStyles.Italic;
                    original.StyleSet = true;
                }
            }
            else if (original.StyleSet)
            {
                RestoreValue(textBlock, TextBlock.FontStyleProperty, original.FontStyleLocal);
                original.StyleSet = false;
            }

            // Size, and the UX-002 counter-scale
            double baseSize = s.FontOverrideSize > 0 ? s.FontOverrideSize : original.BaseFontSize;
            double? targetSize = null;
            double? counterScale = null;

            if (nativeSizing)
            {
                // UX-006: the theme is loaded at real size, so text just gets its real size too:
                // whole pixels, no transform.
                targetSize = Math.Max(1.0, Math.Round(baseSize * Settings.Instance.TaskbarScale));
            }
            else if (pixelText)
            {
                double size = baseSize * scale;
                if (s.PixelTextMode == PixelTextOption.NativeWholePixels)
                {
                    size = Math.Max(1.0, Math.Round(size));
                }
                targetSize = size;
                counterScale = 1.0 / scale;
            }
            else if (s.FontOverrideSize > 0)
            {
                targetSize = baseSize;
            }

            if (targetSize.HasValue)
            {
                if (!original.SizeSet || textBlock.FontSize != targetSize.Value)
                {
                    textBlock.FontSize = targetSize.Value;
                    original.SizeSet = true;
                }
            }
            else if (original.SizeSet)
            {
                RestoreValue(textBlock, TextBlock.FontSizeProperty, original.FontSizeLocal);
                original.SizeSet = false;
            }

            if (counterScale.HasValue)
            {
                bool alreadySet = original.TransformSet &&
                                  textBlock.LayoutTransform is ScaleTransform current &&
                                  current.ScaleX == counterScale.Value;
                if (!alreadySet)
                {
                    textBlock.LayoutTransform = new ScaleTransform(counterScale.Value, counterScale.Value);
                    original.TransformSet = true;
                }
            }
            else if (original.TransformSet)
            {
                RestoreValue(textBlock, FrameworkElement.LayoutTransformProperty, original.LayoutTransformLocal);
                original.TransformSet = false;
            }
        }

        /// <summary>
        /// Final on-screen text size in px for a given size at 100%, matching what Apply() does.
        /// </summary>
        public static double OnScreenFontSize(double sizeAt100)
        {
            double scale = Settings.Instance.TaskbarScale;
            double size = sizeAt100 * scale;

            if (ScaleModel.IsNative ||
                (IsPixelTextActive(out _) && Settings.Instance.PixelTextMode == PixelTextOption.NativeWholePixels))
            {
                size = Math.Max(1.0, Math.Round(size));
            }

            return size;
        }

        /// <summary>
        /// How much taller each taskbar row must be (in the same units as Taskbar.DesiredRowHeight,
        /// i.e. already including TaskbarScale) so text in the chosen font fits. 0 when the font
        /// is no taller than the theme's. Only grows; never shrinks a theme's rows.
        /// </summary>
        public static double ExtraRowHeight()
        {
            var s = Settings.Instance;

            if (string.IsNullOrWhiteSpace(s.FontOverrideFamily) && s.FontOverrideSize <= 0)
            {
                return 0;
            }

            FontFamily themeFamily = Application.Current.TryFindResource("GlobalFontFamily") as FontFamily ?? SystemFonts.CaptionFontFamily;
            double themeSize = Application.Current.TryFindResource("GlobalFontSize") as double? ?? 11.0;
            FontFamily family = string.IsNullOrWhiteSpace(s.FontOverrideFamily) ? themeFamily : new FontFamily(s.FontOverrideFamily);
            double size = s.FontOverrideSize > 0 ? s.FontOverrideSize : themeSize;

            double themeLine = themeFamily.LineSpacing * themeSize * s.TaskbarScale;
            double newLine = family.LineSpacing * OnScreenFontSize(size);

            return Math.Max(0, Math.Ceiling(newLine - themeLine));
        }

        private static void Restore(TextBlock textBlock)
        {
            Original original = (Original)textBlock.GetValue(OriginalProperty);

            if (original == null)
            {
                return; // never changed by us
            }

            if (original.FamilySet) RestoreValue(textBlock, TextBlock.FontFamilyProperty, original.FontFamilyLocal);
            if (original.SizeSet) RestoreValue(textBlock, TextBlock.FontSizeProperty, original.FontSizeLocal);
            if (original.WeightSet) RestoreValue(textBlock, TextBlock.FontWeightProperty, original.FontWeightLocal);
            if (original.StyleSet) RestoreValue(textBlock, TextBlock.FontStyleProperty, original.FontStyleLocal);
            if (original.TransformSet) RestoreValue(textBlock, FrameworkElement.LayoutTransformProperty, original.LayoutTransformLocal);
            textBlock.ClearValue(OriginalProperty);
        }

        private static void RestoreValue(DependencyObject target, DependencyProperty property, object original)
        {
            if (original is BindingExpressionBase expression)
            {
                BindingOperations.SetBinding(target, property, expression.ParentBindingBase);
            }
            else if (original == null || original == DependencyProperty.UnsetValue)
            {
                target.ClearValue(property);
            }
            else
            {
                target.SetValue(property, original);
            }
        }

        private static void ReapplyAll()
        {
            var textBlocks = new List<TextBlock>();

            foreach (Window window in Application.Current.Windows)
            {
                if (window is Taskbar)
                {
                    CollectTextBlocks(window, textBlocks);
                }
            }

            // Put everything back first, then re-apply once the new theme/scale has been laid
            // out, so base values are read from the current theme rather than our own values.
            foreach (TextBlock textBlock in textBlocks)
            {
                Restore(textBlock);
            }

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                foreach (TextBlock textBlock in textBlocks)
                {
                    if (textBlock.IsLoaded)
                    {
                        Apply(textBlock);
                    }
                }
            }));
        }

        private static void CollectTextBlocks(DependencyObject parent, List<TextBlock> results)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is TextBlock textBlock)
                {
                    results.Add(textBlock);
                }

                CollectTextBlocks(child, results);
            }
        }
    }
}
