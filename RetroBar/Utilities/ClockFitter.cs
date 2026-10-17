using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-008): keeps the taskbar clock tidy with any font.
    //
    // 1. Steady width. Most fonts have digits of different widths, so with seconds showing the
    //    clock (and the tray divider next to it) shuffles left and right every tick. Each clock
    //    line reserves the widest width its text could have with any digits. Measured with a
    //    real (off-screen) TextBlock so whole-pixel text and kerning are included, and if the
    //    text still ever comes out wider, the reservation only ever grows ("ratchet").
    // 2. Time position. Centred (theme look: the time nudges as digits change) or anchored
    //    (left edge fixed, only the changing digits move).
    // 3. Clock lines. Themes such as XP and Vista stack time / weekday / date. The owner can
    //    hide lines (never force extra lines).
    // 4. Fit. When the stacked lines are taller than the taskbar, the theme's gaps and the
    //    font's empty space above/below the letters are squeezed out and the lines are spaced
    //    evenly by their visible ("ink") height. Lines that already fit are left as the theme
    //    draws them.
    //
    // Clock lines are identified by the names every theme uses in its ClockTemplateKey:
    // ClockText, ClockVerticalDayOfWeek, ClockVerticalDate.
    public static class ClockFitter
    {
        private const string TimeName = "ClockText";
        private const string DayName = "ClockVerticalDayOfWeek";
        private const string DateName = "ClockVerticalDate";

        // Smallest gap (px) kept between the letters of neighbouring lines when squeezing.
        private const double MinInkGap = 2;

        private class Original
        {
            public object MinWidthLocal, VisibilityLocal, MarginLocal, TextAlignmentLocal, HorizontalAlignmentLocal;
            public bool MinWidthSet, VisibilitySet, MarginSet, AlignmentSet;
            public Thickness ThemeMargin;

            // Steady width cache: the text with digits masked + font, and the width found for it.
            public string WidthKey;
            public double Width;
        }

        private class ClockState
        {
            public bool FitQueued;
            public Size LastConstraint;
            public EventHandler LayoutHandler;
        }

        private static readonly DependencyProperty OriginalProperty =
            DependencyProperty.RegisterAttached("UxuiClockOriginal", typeof(Original), typeof(ClockFitter), new PropertyMetadata(null));

        private static readonly DependencyProperty StateProperty =
            DependencyProperty.RegisterAttached("UxuiClockState", typeof(ClockState), typeof(ClockFitter), new PropertyMetadata(null));

        // WPF keeps the size each element was last offered in Measure but doesn't make it public.
        // The clock is offered the taskbar's real height even when its lines spill past it (its
        // ActualHeight and layout slot grow with the spill), so this is the honest "room we have".
        private static readonly PropertyInfo PreviousConstraintProperty =
            typeof(UIElement).GetProperty("PreviousConstraint", BindingFlags.Instance | BindingFlags.NonPublic);

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

            Original original = GetOriginal(textBlock);

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
            double steadyWidth = SteadyWidth(textBlock, original);

            // Ratchet: if the text still came out wider than reserved, reserve that from now on.
            if (original.MinWidthSet && textBlock.IsMeasureValid)
            {
                double textWidth = textBlock.DesiredSize.Width - textBlock.Margin.Left - textBlock.Margin.Right;
                if (textWidth > textBlock.MinWidth + 0.5)
                {
                    original.Width = Math.Max(original.Width, Math.Ceiling(textWidth));
                    steadyWidth = Math.Max(steadyWidth, original.Width);
                }
            }

            if (steadyWidth > 0 && (!original.MinWidthSet || textBlock.MinWidth != steadyWidth))
            {
                textBlock.MinWidth = steadyWidth;
                original.MinWidthSet = true;
            }

            // Time position
            bool anchor = textBlock.Name == TimeName && Settings.Instance.ClockTimeAlignment == ClockTimeAlignmentOption.Anchored;

            if (anchor && !original.AlignmentSet)
            {
                // A box exactly the reserved width, centred like the theme; the text starts at its left.
                textBlock.HorizontalAlignment = HorizontalAlignment.Center;
                textBlock.TextAlignment = TextAlignment.Left;
                original.AlignmentSet = true;
            }
            else if (!anchor && original.AlignmentSet)
            {
                RestoreValue(textBlock, FrameworkElement.HorizontalAlignmentProperty, original.HorizontalAlignmentLocal);
                RestoreValue(textBlock, TextBlock.TextAlignmentProperty, original.TextAlignmentLocal);
                original.AlignmentSet = false;
            }

            // Fit (vertical), done for the whole clock once layout has settled.
            if (textBlock.TemplatedParent is FrameworkElement clock)
            {
                QueueFit(clock);
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
            if (original.MarginSet) RestoreValue(textBlock, FrameworkElement.MarginProperty, original.MarginLocal);
            if (original.AlignmentSet)
            {
                RestoreValue(textBlock, FrameworkElement.HorizontalAlignmentProperty, original.HorizontalAlignmentLocal);
                RestoreValue(textBlock, TextBlock.TextAlignmentProperty, original.TextAlignmentLocal);
            }
            textBlock.ClearValue(OriginalProperty);

            if (textBlock.TemplatedParent is FrameworkElement clock)
            {
                ClockState state = (ClockState)clock.GetValue(StateProperty);
                if (state != null)
                {
                    state.LastConstraint = Size.Empty; // re-fit on the next layout pass
                }
            }
        }

        private static Original GetOriginal(TextBlock textBlock)
        {
            Original original = (Original)textBlock.GetValue(OriginalProperty);

            if (original == null)
            {
                original = new Original
                {
                    MinWidthLocal = textBlock.ReadLocalValue(FrameworkElement.MinWidthProperty),
                    VisibilityLocal = textBlock.ReadLocalValue(UIElement.VisibilityProperty),
                    MarginLocal = textBlock.ReadLocalValue(FrameworkElement.MarginProperty),
                    TextAlignmentLocal = textBlock.ReadLocalValue(TextBlock.TextAlignmentProperty),
                    HorizontalAlignmentLocal = textBlock.ReadLocalValue(FrameworkElement.HorizontalAlignmentProperty),
                };
                textBlock.SetValue(OriginalProperty, original);
            }

            return original;
        }

        #region Steady width

        private static TextBlock _probe;

        // Widest width the current text could take with any digits in it.
        private static double SteadyWidth(TextBlock textBlock, Original original)
        {
            string text = textBlock.Text;

            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            try
            {
                var mask = new StringBuilder(text.Length);
                foreach (char c in text)
                {
                    mask.Append(char.IsDigit(c) ? '#' : c);
                }

                string key = string.Join("|", mask, textBlock.FontFamily?.Source, textBlock.FontSize, textBlock.FontWeight,
                                         textBlock.FontStyle, textBlock.FontStretch, TextOptions.GetTextFormattingMode(textBlock),
                                         textBlock.Padding);

                if (original.WidthKey == key)
                {
                    return original.Width;
                }

                // Start from the widest single digit everywhere, then try every digit at each
                // position in turn (catches kerning pairs and whole-pixel rounding).
                char widestDigit = '0';
                double widestDigitWidth = 0;
                for (char digit = '0'; digit <= '9'; digit++)
                {
                    double width = MeasureWidth(textBlock, digit.ToString());
                    if (width > widestDigitWidth)
                    {
                        widestDigitWidth = width;
                        widestDigit = digit;
                    }
                }

                char[] chars = text.ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (char.IsDigit(chars[i])) chars[i] = widestDigit;
                }

                double best = MeasureWidth(textBlock, new string(chars));

                for (int i = 0; i < chars.Length; i++)
                {
                    if (!char.IsDigit(text[i])) continue;

                    char keep = chars[i];
                    for (char digit = '0'; digit <= '9'; digit++)
                    {
                        if (digit == keep) continue;
                        chars[i] = digit;
                        double width = MeasureWidth(textBlock, new string(chars));
                        if (width > best)
                        {
                            best = width;
                            keep = digit;
                        }
                    }
                    chars[i] = keep;
                }

                original.WidthKey = key;
                original.Width = Math.Ceiling(best + textBlock.Padding.Left + textBlock.Padding.Right);
                return original.Width;
            }
            catch (Exception)
            {
                return 0; // never let measuring break the clock
            }
        }

        private static double MeasureWidth(TextBlock source, string text)
        {
            if (_probe == null)
            {
                _probe = new TextBlock();
            }

            _probe.FontFamily = source.FontFamily;
            _probe.FontSize = source.FontSize;
            _probe.FontWeight = source.FontWeight;
            _probe.FontStyle = source.FontStyle;
            _probe.FontStretch = source.FontStretch;
            _probe.UseLayoutRounding = source.UseLayoutRounding;
            Typography.SetNumeralAlignment(_probe, Typography.GetNumeralAlignment(source));
            TextOptions.SetTextFormattingMode(_probe, TextOptions.GetTextFormattingMode(source));
            TextOptions.SetTextHintingMode(_probe, TextOptions.GetTextHintingMode(source));
            _probe.Text = text;
            _probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return _probe.DesiredSize.Width;
        }

        #endregion

        #region Fit

        private static void QueueFit(FrameworkElement clock)
        {
            ClockState state = (ClockState)clock.GetValue(StateProperty);

            if (state == null)
            {
                state = new ClockState { LastConstraint = Size.Empty };
                clock.SetValue(StateProperty, state);

                // The room available changes with the taskbar (rows, row height adjust, scale),
                // which doesn't always resize the clock itself, so check after each layout pass.
                // Cheap: nothing happens unless the offered size changed.
                state.LayoutHandler = (s, e) =>
                {
                    if (clock.IsLoaded && GetConstraint(clock) != state.LastConstraint)
                    {
                        Fit(clock, state);
                    }
                };
                clock.LayoutUpdated += state.LayoutHandler;
                clock.Unloaded += Clock_Unloaded;
            }

            if (state.FitQueued)
            {
                return;
            }

            state.FitQueued = true;
            clock.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                state.FitQueued = false;
                if (clock.IsLoaded)
                {
                    Fit(clock, state);
                }
            }));
        }

        private static void Clock_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement clock)
            {
                ClockState state = (ClockState)clock.GetValue(StateProperty);
                if (state != null)
                {
                    clock.LayoutUpdated -= state.LayoutHandler;
                }
                clock.Unloaded -= Clock_Unloaded;
                clock.ClearValue(StateProperty);
            }
        }

        private static Size GetConstraint(FrameworkElement clock)
        {
            try
            {
                if (PreviousConstraintProperty?.GetValue(clock) is Size size)
                {
                    return size;
                }
            }
            catch (Exception) { }

            return Size.Empty;
        }

        private static void Fit(FrameworkElement clock, ClockState state)
        {
            Size constraint = GetConstraint(clock);
            state.LastConstraint = constraint;

            // The template is either one TextBlock or a panel holding only clock lines.
            var lines = new List<TextBlock>();
            DependencyObject root = VisualTreeHelper.GetChildrenCount(clock) > 0 ? VisualTreeHelper.GetChild(clock, 0) : null;
            double rootMargin = 0;

            if (root is TextBlock single && IsClockLine(single))
            {
                lines.Add(single);
            }
            else if (root is Panel panel)
            {
                rootMargin = panel.Margin.Top + panel.Margin.Bottom;
                foreach (UIElement child in panel.Children)
                {
                    if (child is TextBlock line && IsClockLine(line))
                    {
                        lines.Add(line);
                    }
                    else
                    {
                        return; // unknown layout; leave the theme alone
                    }
                }
            }
            else
            {
                return;
            }

            var visible = new List<TextBlock>();
            foreach (TextBlock line in lines)
            {
                Original original = GetOriginal(line);
                if (!original.MarginSet)
                {
                    original.ThemeMargin = line.Margin; // still the theme's own value
                }

                if (line.Visibility == Visibility.Visible)
                {
                    visible.Add(line);
                }
                else
                {
                    Unfit(line, original);
                }
            }

            // Margin evening: the theme's margins, or evened / removed outer margins, are the
            // starting point for everything below.
            ClockMarginOption marginMode = EffectiveMarginMode(clock);
            Thickness[] baseMargins = BaseMargins(visible, marginMode);
            bool trimmed = marginMode == ClockMarginOption.Even || marginMode == ClockMarginOption.None;
            void settle()
            {
                for (int i = 0; i < visible.Count; i++)
                {
                    if (trimmed) SetMargin(visible[i], baseMargins[i]);
                    else Unfit(visible[i], GetOriginal(visible[i]));
                }
            }

            double available = constraint.Height - clock.Margin.Top - clock.Margin.Bottom - rootMargin;

            if (!Settings.Instance.ClockFitLines || visible.Count == 0 ||
                double.IsNaN(available) || double.IsInfinity(available) || available <= 0)
            {
                settle();
                return;
            }

            var metrics = new List<LineMetrics>();
            double natural = 0;
            double inkTotal = 0;

            foreach (TextBlock line in visible)
            {
                LineMetrics m = Measure(line);
                if (m == null)
                {
                    settle();
                    return;
                }

                Thickness theme = baseMargins[visible.IndexOf(line)];
                natural += m.BoxHeight + theme.Top + theme.Bottom;
                inkTotal += m.InkBottom - m.InkTop;
                metrics.Add(m);
            }

            if (natural <= available + 0.5)
            {
                // Fits as the theme draws it.
                settle();
                return;
            }

            // Space evenly by ink: each line gets its letters plus an equal share of the room
            // left over (half above, half below), so gaps between lines are equal.
            int count = visible.Count;
            double share = Math.Floor((available - inkTotal) / count);
            if (share < MinInkGap)
            {
                share = MinInkGap; // still too tall; as tight as it goes
            }

            double used = 0;
            var margins = new Thickness[count];

            for (int i = 0; i < count; i++)
            {
                LineMetrics m = metrics[i];
                Thickness theme = baseMargins[i];
                double height = Math.Round(m.InkBottom - m.InkTop + share);
                double above = Math.Floor(share / 2);
                double top = Math.Round(above - m.InkTop);
                double bottom = height - top - m.BoxHeight;
                margins[i] = new Thickness(theme.Left, top, theme.Right, bottom);
                used += height;
            }

            // Keep the centred block on whole pixels.
            double slack = available - used;
            if (slack > 0)
            {
                double odd = slack - 2 * Math.Floor(slack / 2);
                if (odd > 0)
                {
                    Thickness last = margins[count - 1];
                    margins[count - 1] = new Thickness(last.Left, last.Top, last.Right, last.Bottom + odd);
                }
            }

            for (int i = 0; i < count; i++)
            {
                TextBlock line = visible[i];
                Original original = GetOriginal(line);

                if (!original.MarginSet || line.Margin != margins[i])
                {
                    line.Margin = margins[i];
                    original.MarginSet = true;
                }
            }
        }

        // Turned clocks (Automatic) and the Even / None modes get evened margins.
        private static ClockMarginOption EffectiveMarginMode(FrameworkElement clock)
        {
            ClockMarginOption mode = Settings.Instance.ClockMarginMode;
            if (mode != ClockMarginOption.Automatic)
            {
                return mode;
            }

            bool turned = VisualTreeHelper.GetParent(clock) is Border host && host.LayoutTransform is RotateTransform;
            return turned ? ClockMarginOption.Even : ClockMarginOption.Theme;
        }

        // The margins each visible line starts from. Even: left = right, and the block's outer top
        // and bottom (first line's top, last line's bottom) equal, so a turned clock sits centred
        // across and along the bar instead of leaning toward the side the theme pads. None: those
        // outer margins removed. Gaps between lines are the theme's.
        private static Thickness[] BaseMargins(List<TextBlock> visible, ClockMarginOption mode)
        {
            var margins = new Thickness[visible.Count];
            for (int i = 0; i < visible.Count; i++)
            {
                margins[i] = GetOriginal(visible[i]).ThemeMargin;
            }

            if ((mode != ClockMarginOption.Even && mode != ClockMarginOption.None) || margins.Length == 0)
            {
                return margins;
            }

            for (int i = 0; i < margins.Length; i++)
            {
                double side = mode == ClockMarginOption.None ? 0 : (margins[i].Left + margins[i].Right) / 2;
                margins[i] = new Thickness(side, margins[i].Top, side, margins[i].Bottom);
            }

            int last = margins.Length - 1;
            double outer = mode == ClockMarginOption.None ? 0 : (margins[0].Top + margins[last].Bottom) / 2;
            margins[0] = new Thickness(margins[0].Left, outer, margins[0].Right, margins[0].Bottom);
            margins[last] = new Thickness(margins[last].Left, margins[last].Top, margins[last].Right, outer);
            return margins;
        }

        private static void SetMargin(TextBlock line, Thickness margin)
        {
            Original original = GetOriginal(line);
            if (!original.MarginSet || line.Margin != margin)
            {
                line.Margin = margin;
                original.MarginSet = true;
            }
        }

        private static void Unfit(TextBlock line, Original original)
        {
            if (original.MarginSet)
            {
                RestoreValue(line, FrameworkElement.MarginProperty, original.MarginLocal);
                original.MarginSet = false;
            }
        }

        private class LineMetrics
        {
            public double BoxHeight; // layout height of the TextBlock (without margins)
            public double InkTop;    // first drawn pixel row, from the top of the box
            public double InkBottom; // last drawn pixel row, from the top of the box
        }

        private static LineMetrics Measure(TextBlock line)
        {
            try
            {
                string text = string.IsNullOrEmpty(line.Text) ? "0" : line.Text;
                var typeface = new Typeface(line.FontFamily, line.FontStyle, line.FontWeight, line.FontStretch);
                double pixelsPerDip = VisualTreeHelper.GetDpi(line).PixelsPerDip;
                var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, line.FlowDirection, typeface,
                                                  line.FontSize, Brushes.Black, null,
                                                  TextOptions.GetTextFormattingMode(line), pixelsPerDip);

                double height = formatted.Height;
                double inkBottom = formatted.Height + formatted.OverhangAfter;
                double inkTop = inkBottom - formatted.Extent;
                int lineCount = text.Split('\n').Length;

                if (line.LineStackingStrategy == LineStackingStrategy.BlockLineHeight &&
                    !double.IsNaN(line.LineHeight) && line.LineHeight > 0)
                {
                    // Set line height: WPF places the baseline at the same proportion of the
                    // line as the font's natural baseline.
                    double naturalLine = height / lineCount;
                    double blockLine = line.LineHeight;
                    double naturalBase = formatted.Baseline;
                    double blockBase = blockLine * naturalBase / naturalLine;

                    inkTop = inkTop - naturalBase + blockBase;
                    inkBottom = inkBottom - ((lineCount - 1) * naturalLine + naturalBase) + ((lineCount - 1) * blockLine + blockBase);
                    height = blockLine * lineCount;
                }

                if (formatted.Extent <= 0)
                {
                    inkTop = 0;
                    inkBottom = height;
                }

                return new LineMetrics
                {
                    BoxHeight = height + line.Padding.Top + line.Padding.Bottom,
                    InkTop = inkTop + line.Padding.Top,
                    InkBottom = inkBottom + line.Padding.Top,
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

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
