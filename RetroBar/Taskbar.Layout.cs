using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RetroBar.Utilities;

namespace RetroBar
{
    // UXUI-Extreme (UX-012): layout options from the Layout tab — vertical / resized Start
    // button, Show desktop button beside or under it, and a vertical clock. Applied after
    // layout settles (the Start button is sized from the taskbar's real height).
    public partial class Taskbar
    {
        private static readonly string[] LayoutSettings =
        {
            nameof(Settings.StartButtonOrientation),
            nameof(Settings.StartButtonLengthMode),
            nameof(Settings.StartButtonLengthPx),
            nameof(Settings.StartButtonThicknessMode),
            nameof(Settings.StartButtonThicknessPx),
            nameof(Settings.StartButtonAnchor),
            nameof(Settings.StartButtonEdgeGap),
            nameof(Settings.StartButtonIconAlign),
            nameof(Settings.ShowDesktopCustomMargin),
            nameof(Settings.ShowDesktopMargin),
            nameof(Settings.StartButtonShowIcon),
            nameof(Settings.StartButtonShowText),
            nameof(Settings.StartButtonStretchMode),
            nameof(Settings.StartButtonIcon), // UX-015
            nameof(Settings.StartButtonImage),
            nameof(Settings.StartButtonImageStates),
            nameof(Settings.ShowDesktopPosition),
            nameof(Settings.ShowDesktopButton),
            nameof(Settings.ClockOrientation),
            nameof(Settings.QuickLaunchLength),
            nameof(Settings.QuickLaunchFirst),
            nameof(Settings.ShowQuickLaunch),
            nameof(Settings.QuickLaunchButtonFill),
            nameof(Settings.ClockCustomMargin),
            nameof(Settings.ClockMargin),
            nameof(Settings.Theme),
            nameof(Settings.TaskbarScale),
            nameof(Settings.ScalingMode),
            nameof(Settings.Edge),
        };

        private const double StartAreaGap = 4; // px at 100%
        private bool _layoutPending;
        private double _layoutBarHeight = -1;

        private void InitLayoutOptions()
        {
            UpdateShowDesktopPlacement();

            Settings.Instance.PropertyChanged += LayoutSettings_PropertyChanged;
            PropertyChanged += LayoutTaskbar_PropertyChanged;
            StyleLayer.Changed += LayoutStyleLayer_Changed; // UX-010 recolouring changes the Start image
            Loaded += (s, e) => ScheduleLayoutOptions();
            Closed += (s, e) =>
            {
                Settings.Instance.PropertyChanged -= LayoutSettings_PropertyChanged;
                PropertyChanged -= LayoutTaskbar_PropertyChanged;
                StyleLayer.Changed -= LayoutStyleLayer_Changed;
            };

            // The Start button fills the taskbar's height, so follow changes to it (rows, scale, lock).
            if (StartArea.Parent is FrameworkElement bar)
            {
                bar.SizeChanged += (s, e) =>
                {
                    if (Math.Abs(e.NewSize.Height - _layoutBarHeight) > 0.5)
                    {
                        ScheduleLayoutOptions();
                    }
                };
            }
        }

        private void LayoutSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (Array.IndexOf(LayoutSettings, e.PropertyName) >= 0)
            {
                ScheduleLayoutOptions();
            }
        }

        private void LayoutStyleLayer_Changed(object sender, EventArgs e)
        {
            ScheduleLayoutOptions();
        }

        private void LayoutTaskbar_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Orientation) || e.PropertyName == "Rows")
            {
                ScheduleLayoutOptions();
            }
        }

        private void ScheduleLayoutOptions()
        {
            if (_layoutPending)
            {
                return;
            }

            _layoutPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _layoutPending = false;
                ApplyLayoutOptions();
            }));
        }

        private double _quickLaunchDragLength;

        // Called while the grip is dragged: sets the length live, remembered when the drag ends.
        public void ResizeQuickLaunch(double delta)
        {
            if (QuickLaunchToolbar.Visibility != Visibility.Visible)
            {
                return;
            }

            bool horizontal = Orientation == Orientation.Horizontal;
            if (_quickLaunchDragLength <= 0)
            {
                _quickLaunchDragLength = horizontal ? QuickLaunchToolbar.ActualWidth : QuickLaunchToolbar.ActualHeight;
            }

            _quickLaunchDragLength = Math.Clamp(_quickLaunchDragLength + delta, 16, 2000); // thumb deltas are in layout units, like Width
            if (horizontal) QuickLaunchToolbar.Width = _quickLaunchDragLength;
            else QuickLaunchToolbar.Height = _quickLaunchDragLength;
        }

        public void FinishQuickLaunchResize()
        {
            if (_quickLaunchDragLength > 0)
            {
                Settings.Instance.QuickLaunchLength = Math.Round(_quickLaunchDragLength);
            }
            _quickLaunchDragLength = 0;
        }

        // The quick launch area before or after the Start button area (which keeps the Show desktop button).
        private void ApplySectionOrder(bool quickFirst)
        {
            if (!(StartArea.Parent is DockPanel panel))
            {
                return;
            }

            int start = panel.Children.IndexOf(StartArea), quick = panel.Children.IndexOf(QuickLaunchToolbar);
            if (start < 0 || quick < 0 || (quick < start) == quickFirst)
            {
                return;
            }

            panel.Children.Remove(QuickLaunchToolbar);
            panel.Children.Insert(quickFirst ? panel.Children.IndexOf(StartArea) : panel.Children.IndexOf(StartArea) + 1, QuickLaunchToolbar);
        }

        private void UpdateShowDesktopPlacement()
        {
            var s = Settings.Instance;
            bool atEnd = s.ShowDesktopPosition == ShowDesktopPositionOption.TaskbarEnd;
            // Beside, or inside, the quick launch area (when that is showing; otherwise it stays beside Start).
            bool besideQuick = s.ShowDesktopPosition == ShowDesktopPositionOption.BesideQuickLaunch && s.ShowQuickLaunch;
            bool inQuick = s.ShowDesktopPosition == ShowDesktopPositionOption.InQuickLaunch && s.ShowQuickLaunch;
            ShowDesktopButtonTray.Visibility = s.ShowDesktopButton && atEnd ? Visibility.Visible : Visibility.Collapsed;
            ShowDesktopButtonStart.Visibility = s.ShowDesktopButton && !atEnd && !besideQuick && !inQuick ? Visibility.Visible : Visibility.Collapsed;
            QuickLaunchToolbar.SetDesktopCell(s.ShowDesktopButton && besideQuick, ShowDesktopButtonStart.TasksService);
            QuickLaunchToolbar.SetDesktopItem(s.ShowDesktopButton && inQuick);
        }

        private void ApplyLayoutOptions()
        {
            try
            {
                var s = Settings.Instance;
                bool horizontal = Orientation == Orientation.Horizontal;

                UpdateShowDesktopPlacement();
                ApplySectionOrder(s.QuickLaunchFirst);
                UpdateLayout();
                TextureAlign.Apply(this); // nested theme borders share one texture phase

                // Beside = along the taskbar, under = across it.
                bool under = s.ShowDesktopPosition == ShowDesktopPositionOption.UnderStart &&
                             ShowDesktopButtonStart.Visibility == Visibility.Visible;
                StartArea.Orientation = horizontal == under ? Orientation.Vertical : Orientation.Horizontal;

                StartArea.VerticalAlignment = !horizontal ? VerticalAlignment.Stretch : s.StartButtonAnchor switch
                {
                    VerticalAnchorOption.Top => VerticalAlignment.Top,
                    VerticalAnchorOption.Bottom => VerticalAlignment.Bottom,
                    _ => VerticalAlignment.Center,
                };

                // Keep a custom Start button off the border of the edge it is anchored to.
                bool customStart = s.StartButtonOrientation != ElementOrientationOption.Horizontal ||
                                   s.StartButtonLengthMode == SizeModeOption.Custom ||
                                   s.StartButtonThicknessMode == SizeModeOption.Custom ||
                                   !string.IsNullOrEmpty(s.StartButtonImage);
                double edgeGap = customStart && horizontal ? Math.Round(s.StartButtonEdgeGap * (ScaleModel.IsNative ? s.TaskbarScale : 1.0)) : 0;
                StartArea.Margin = s.StartButtonAnchor switch
                {
                    VerticalAnchorOption.Top => new Thickness(0, edgeGap, 0, 0),
                    VerticalAnchorOption.Bottom => new Thickness(0, 0, 0, edgeGap),
                    _ => new Thickness(0),
                };

                double barHeight = (StartArea.Parent as FrameworkElement)?.ActualHeight ?? 0;
                _layoutBarHeight = barHeight;

                // A small gap between the Start button and a tucked Show desktop button.
                // By default the gap equals the Start button's gap to the taskbar edge; custom margins replace it.
                double marginUnit = ScaleModel.IsNative ? s.TaskbarScale : 1.0;
                double gap = ShowDesktopButtonStart.Visibility == Visibility.Visible ? Math.Round(s.StartButtonEdgeGap * marginUnit) : 0;
                if (s.ShowDesktopCustomMargin && RetroBar.Controls.NotifyIconList.TryParseMargin(s.ShowDesktopMargin, out Thickness deskMargin))
                {
                    Thickness scaled = new Thickness(Math.Round(deskMargin.Left * marginUnit), Math.Round(deskMargin.Top * marginUnit),
                                                     Math.Round(deskMargin.Right * marginUnit), Math.Round(deskMargin.Bottom * marginUnit));
                    ShowDesktopButtonStart.Margin = scaled;
                    ShowDesktopButtonTray.Margin = scaled;
                    gap = ShowDesktopButtonStart.Visibility == Visibility.Visible
                        ? (StartArea.Orientation == Orientation.Vertical ? scaled.Top : scaled.Left) : 0;
                }
                else
                {
                    ShowDesktopButtonStart.Margin = StartArea.Orientation == Orientation.Vertical ? new Thickness(0, gap, 0, 0) : new Thickness(gap, 0, 0, 0);
                    ShowDesktopButtonTray.ClearValue(MarginProperty);
                }

                double available = barHeight;
                available -= StartArea.Margin.Top + StartArea.Margin.Bottom; // the gap to the edge is not button room
                if (under && horizontal)
                {
                    // Its measured height includes the gap above it.
                    ShowDesktopButtonStart.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    available -= ShowDesktopButtonStart.DesiredSize.Height;
                }

                StartArea.UseLayoutRounding = true; // a turned or resized Start button must sit on whole pixels
                StartArea.SnapsToDevicePixels = true;
                StartButton.ApplyLayout(available);

                if (horizontal && s.ClockOrientation != ElementOrientationOption.Horizontal)
                {
                    ClockHost.LayoutTransform = new RotateTransform(s.ClockOrientation == ElementOrientationOption.VerticalUp ? -90 : 90);
                }
                else
                {
                    ClockHost.ClearValue(LayoutTransformProperty);
                }

                // Quick launch length, as set by its grip.
                if (s.QuickLaunchLength > 0)
                {
                    if (horizontal)
                    {
                        QuickLaunchToolbar.Width = s.QuickLaunchLength;
                        QuickLaunchToolbar.ClearValue(HeightProperty);
                    }
                    else
                    {
                        QuickLaunchToolbar.Height = s.QuickLaunchLength;
                        QuickLaunchToolbar.ClearValue(WidthProperty);
                    }
                }
                else
                {
                    QuickLaunchToolbar.ClearValue(WidthProperty);
                    QuickLaunchToolbar.ClearValue(HeightProperty);
                }

                // A turned clock's size is rarely a whole number of pixels, so centring it in the
                // bar put it on half pixels: off-centre by one, and blurred. Rounding keeps it on whole ones.
                ClockHost.UseLayoutRounding = true;
                ClockHost.SnapsToDevicePixels = true;

                if (s.ClockCustomMargin && RetroBar.Controls.NotifyIconList.TryParseMargin(s.ClockMargin, out Thickness clockMargin))
                {
                    double unit = ScaleModel.IsNative ? s.TaskbarScale : 1.0;
                    ClockHost.Margin = new Thickness(Math.Round(clockMargin.Left * unit), Math.Round(clockMargin.Top * unit),
                                                     Math.Round(clockMargin.Right * unit), Math.Round(clockMargin.Bottom * unit));
                }
                else
                {
                    ClockHost.ClearValue(MarginProperty);
                }
            }
            catch (Exception ex)
            {
                ManagedShell.Common.Logging.ShellLogger.Warning($"UXUI-Extreme: layout options not applied: {ex.Message}");
            }
        }
    }
}
