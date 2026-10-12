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
        };

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.LoadedEvent, new RoutedEventHandler(TextBlock_Loaded));
            Settings.Instance.PropertyChanged += Settings_PropertyChanged;
        }

        private static void TextBlock_Loaded(object sender, RoutedEventArgs e)
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
            return Settings.Instance.PixelTextMode != PixelTextOption.Off &&
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
            if (Window.GetWindow(textBlock) is not Taskbar)
            {
                return;
            }

            bool pixelText = IsPixelTextActive(out double scale);

            if (!pixelText && !IsFontOverrideActive())
            {
                Restore(textBlock);
                return;
            }

            Original original = (Original)textBlock.GetValue(OriginalProperty);

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
                textBlock.FontFamily = new FontFamily(s.FontOverrideFamily);
            }
            else
            {
                RestoreValue(textBlock, TextBlock.FontFamilyProperty, original.FontFamilyLocal);
            }

            // Weight: text the theme made bold (e.g. "Start") stays at least bold.
            if (s.FontOverrideWeight > 0)
            {
                int weight = Math.Clamp(s.FontOverrideWeight, 1, 999);
                if (original.ThemeBold)
                {
                    weight = Math.Max(weight, FontWeights.Bold.ToOpenTypeWeight());
                }
                textBlock.FontWeight = FontWeight.FromOpenTypeWeight(weight);
            }
            else
            {
                RestoreValue(textBlock, TextBlock.FontWeightProperty, original.FontWeightLocal);
            }

            // Italic
            if (s.FontOverrideItalic)
            {
                textBlock.FontStyle = FontStyles.Italic;
            }
            else
            {
                RestoreValue(textBlock, TextBlock.FontStyleProperty, original.FontStyleLocal);
            }

            // Size, and the UX-002 counter-scale
            double baseSize = s.FontOverrideSize > 0 ? s.FontOverrideSize : original.BaseFontSize;

            if (pixelText)
            {
                double targetSize = baseSize * scale;

                if (s.PixelTextMode == PixelTextOption.NativeWholePixels)
                {
                    targetSize = Math.Max(1.0, Math.Round(targetSize));
                }

                textBlock.FontSize = targetSize;
                textBlock.LayoutTransform = new ScaleTransform(1.0 / scale, 1.0 / scale);
            }
            else
            {
                if (s.FontOverrideSize > 0)
                {
                    textBlock.FontSize = baseSize;
                }
                else
                {
                    RestoreValue(textBlock, TextBlock.FontSizeProperty, original.FontSizeLocal);
                }

                RestoreValue(textBlock, FrameworkElement.LayoutTransformProperty, original.LayoutTransformLocal);
            }
        }

        private static void Restore(TextBlock textBlock)
        {
            Original original = (Original)textBlock.GetValue(OriginalProperty);

            if (original == null)
            {
                return; // never changed by us
            }

            RestoreValue(textBlock, TextBlock.FontFamilyProperty, original.FontFamilyLocal);
            RestoreValue(textBlock, TextBlock.FontSizeProperty, original.FontSizeLocal);
            RestoreValue(textBlock, TextBlock.FontWeightProperty, original.FontWeightLocal);
            RestoreValue(textBlock, TextBlock.FontStyleProperty, original.FontStyleLocal);
            RestoreValue(textBlock, FrameworkElement.LayoutTransformProperty, original.LayoutTransformLocal);
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
