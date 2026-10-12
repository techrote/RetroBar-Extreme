using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-008): keeps the taskbar clock tidy with any font.
    //
    // 1. Steady width. Most fonts have digits of different widths, so with seconds showing the
    //    clock (and the tray divider next to it) shuffles left and right every tick. Each clock
    //    line reserves the width its text would have if every digit were the font's widest digit.
    // 2. Clock lines. Themes such as XP and Vista stack time / weekday / date. With a big font
    //    there isn't room for all three, so the owner can hide lines (never force extra lines).
    //
    // Clock lines are identified by the names every theme uses in its ClockTemplateKey:
    // ClockText, ClockVerticalDayOfWeek, ClockVerticalDate.
    public static class ClockFitter
    {
        private const string TimeName = "ClockText";
        private const string DayName = "ClockVerticalDayOfWeek";
        private const string DateName = "ClockVerticalDate";

        private class Original
        {
            public object MinWidthLocal, VisibilityLocal;
            public bool MinWidthSet, VisibilitySet;
        }

        private static readonly DependencyProperty OriginalProperty =
            DependencyProperty.RegisterAttached("UxuiClockOriginal", typeof(Original), typeof(ClockFitter), new PropertyMetadata(null));

        public static bool IsClockLine(TextBlock textBlock)
        {
            string name = textBlock.Name;
            return name == TimeName || name == DayName || name == DateName;
        }

        public static void Apply(TextBlock textBlock)
        {
            if (!IsClockLine(textBlock))
            {
                return;
            }

            Original original = (Original)textBlock.GetValue(OriginalProperty);

            if (original == null)
            {
                original = new Original
                {
                    MinWidthLocal = textBlock.ReadLocalValue(FrameworkElement.MinWidthProperty),
                    VisibilityLocal = textBlock.ReadLocalValue(UIElement.VisibilityProperty),
                };
                textBlock.SetValue(OriginalProperty, original);
            }

            // Lines to hide. Only ever hides; Theme default leaves the theme's own rules alone.
            bool hide = (textBlock.Name == DateName && Settings.Instance.ClockLines != ClockLinesOption.ThemeDefault) ||
                        (textBlock.Name == DayName && Settings.Instance.ClockLines == ClockLinesOption.TimeOnly);

            if (hide)
            {
                if (!original.VisibilitySet || textBlock.Visibility != Visibility.Collapsed)
                {
                    textBlock.Visibility = Visibility.Collapsed;
                    original.VisibilitySet = true;
                }
            }
            else if (original.VisibilitySet)
            {
                RestoreValue(textBlock, UIElement.VisibilityProperty, original.VisibilityLocal);
                original.VisibilitySet = false;
            }

            // Steady width
            double steadyWidth = SteadyWidth(textBlock);

            if (steadyWidth > 0 && (!original.MinWidthSet || textBlock.MinWidth != steadyWidth))
            {
                textBlock.MinWidth = steadyWidth;
                original.MinWidthSet = true;
            }
        }

        public static void Restore(TextBlock textBlock)
        {
            Original original = (Original)textBlock.GetValue(OriginalProperty);

            if (original == null)
            {
                return;
            }

            if (original.MinWidthSet) RestoreValue(textBlock, FrameworkElement.MinWidthProperty, original.MinWidthLocal);
            if (original.VisibilitySet) RestoreValue(textBlock, UIElement.VisibilityProperty, original.VisibilityLocal);
            textBlock.ClearValue(OriginalProperty);
        }

        // Width of the current text with every digit swapped for the font's widest digit.
        private static double SteadyWidth(TextBlock textBlock)
        {
            string text = textBlock.Text;

            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            try
            {
                double pixelsPerDip = VisualTreeHelper.GetDpi(textBlock).PixelsPerDip;
                var typeface = new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch);

                char widestDigit = '0';
                double widestDigitWidth = 0;
                for (char digit = '0'; digit <= '9'; digit++)
                {
                    double width = Measure(digit.ToString(), typeface, textBlock.FontSize, pixelsPerDip);
                    if (width > widestDigitWidth)
                    {
                        widestDigitWidth = width;
                        widestDigit = digit;
                    }
                }

                var widest = new StringBuilder(text.Length);
                foreach (char c in text)
                {
                    widest.Append(char.IsDigit(c) ? widestDigit : c);
                }

                double textWidth = Measure(widest.ToString(), typeface, textBlock.FontSize, pixelsPerDip);
                return Math.Ceiling(textWidth + textBlock.Padding.Left + textBlock.Padding.Right);
            }
            catch (Exception)
            {
                return 0; // never let measuring break the clock
            }
        }

        private static double Measure(string text, Typeface typeface, double fontSize, double pixelsPerDip)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                              typeface, fontSize, Brushes.Black, pixelsPerDip);
            return formatted.WidthIncludingTrailingWhitespace;
        }

        private static void RestoreValue(DependencyObject target, DependencyProperty property, object original)
        {
            if (original == null || original == DependencyProperty.UnsetValue || original is System.Windows.Data.BindingExpressionBase)
            {
                target.ClearValue(property);
            }
            else
            {
                target.SetValue(property, original);
            }
        }
    }
}
