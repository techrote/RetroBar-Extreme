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
    // UXUI-Extreme (UX-002): crisp pixel text when the taskbar is scaled.
    //
    // RetroBar scales the whole taskbar with one LayoutTransform. With font smoothing off,
    // text is rasterised at 100% and then stretched by that transform, so at in-between
    // scales (105%-195%) some pixel rows/columns are doubled and others aren't.
    //
    // Instead, for every TextBlock on a taskbar we:
    //   1. multiply its font size by the scale, so the font is rendered at its real final
    //      size (optionally rounded to whole pixels, so the font's own pixel hinting is used);
    //   2. give it a counter-transform of 1/scale, which cancels the taskbar's stretch for
    //      that text only. Boxes, borders and icons keep scaling exactly as before.
    //
    // Original values are remembered and restored whenever the mode, scale, theme or font
    // smoothing changes, so switching back to "Off" is exactly stock behaviour.
    public static class PixelTextScaler
    {
        private static readonly DependencyProperty OriginalFontSizeProperty =
            DependencyProperty.RegisterAttached("OriginalFontSize", typeof(object), typeof(PixelTextScaler), new PropertyMetadata(null));

        private static readonly DependencyProperty OriginalLayoutTransformProperty =
            DependencyProperty.RegisterAttached("OriginalLayoutTransform", typeof(object), typeof(PixelTextScaler), new PropertyMetadata(null));

        private static readonly DependencyProperty BaseFontSizeProperty =
            DependencyProperty.RegisterAttached("BaseFontSize", typeof(double), typeof(PixelTextScaler), new PropertyMetadata(double.NaN));

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
            if (e.PropertyName == nameof(Settings.PixelTextMode) ||
                e.PropertyName == nameof(Settings.TaskbarScale) ||
                e.PropertyName == nameof(Settings.AllowFontSmoothing) ||
                e.PropertyName == nameof(Settings.Theme))
            {
                ReapplyAll();
            }
        }

        private static bool IsActive(out double scale)
        {
            scale = Settings.Instance.TaskbarScale;
            return Settings.Instance.PixelTextMode != PixelTextOption.Off &&
                   !Settings.Instance.AllowFontSmoothing &&
                   scale > 1.0;
        }

        private static void Apply(TextBlock textBlock)
        {
            if (Window.GetWindow(textBlock) is not Taskbar)
            {
                return;
            }

            if (!IsActive(out double scale))
            {
                Restore(textBlock);
                return;
            }

            double baseSize = (double)textBlock.GetValue(BaseFontSizeProperty);

            if (double.IsNaN(baseSize))
            {
                // First time: remember what the theme/template set, so we can put it back.
                textBlock.SetValue(OriginalFontSizeProperty, textBlock.ReadLocalValue(TextBlock.FontSizeProperty));
                textBlock.SetValue(OriginalLayoutTransformProperty, textBlock.ReadLocalValue(FrameworkElement.LayoutTransformProperty));
                baseSize = textBlock.FontSize;
                textBlock.SetValue(BaseFontSizeProperty, baseSize);
            }

            double targetSize = baseSize * scale;

            if (Settings.Instance.PixelTextMode == PixelTextOption.NativeWholePixels)
            {
                targetSize = Math.Max(1.0, Math.Round(targetSize));
            }

            textBlock.FontSize = targetSize;
            textBlock.LayoutTransform = new ScaleTransform(1.0 / scale, 1.0 / scale);
        }

        private static void Restore(TextBlock textBlock)
        {
            if (double.IsNaN((double)textBlock.GetValue(BaseFontSizeProperty)))
            {
                return; // never changed by us
            }

            RestoreValue(textBlock, TextBlock.FontSizeProperty, textBlock.GetValue(OriginalFontSizeProperty));
            RestoreValue(textBlock, FrameworkElement.LayoutTransformProperty, textBlock.GetValue(OriginalLayoutTransformProperty));

            textBlock.ClearValue(OriginalFontSizeProperty);
            textBlock.ClearValue(OriginalLayoutTransformProperty);
            textBlock.ClearValue(BaseFontSizeProperty);
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
            // out, so the base font size is read from the current theme rather than our own value.
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
