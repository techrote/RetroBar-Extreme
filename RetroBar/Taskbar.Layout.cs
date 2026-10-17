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
            nameof(Settings.StartButtonShowIcon),
            nameof(Settings.StartButtonShowText),
            nameof(Settings.StartButtonStretchMode),
            nameof(Settings.StartButtonIcon), // UX-015
            nameof(Settings.ShowDesktopPosition),
            nameof(Settings.ShowDesktopButton),
            nameof(Settings.ClockOrientation),
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

        private void UpdateShowDesktopPlacement()
        {
            var s = Settings.Instance;
            bool atEnd = s.ShowDesktopPosition == ShowDesktopPositionOption.TaskbarEnd;
            ShowDesktopButtonTray.Visibility = s.ShowDesktopButton && atEnd ? Visibility.Visible : Visibility.Collapsed;
            ShowDesktopButtonStart.Visibility = s.ShowDesktopButton && !atEnd ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyLayoutOptions()
        {
            try
            {
                var s = Settings.Instance;
                bool horizontal = Orientation == Orientation.Horizontal;

                UpdateShowDesktopPlacement();

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

                double barHeight = (StartArea.Parent as FrameworkElement)?.ActualHeight ?? 0;
                _layoutBarHeight = barHeight;

                // A small gap between the Start button and a tucked Show desktop button.
                double gap = ShowDesktopButtonStart.Visibility == Visibility.Visible ? Math.Round(StartAreaGap * (ScaleModel.IsNative ? s.TaskbarScale : 1.0)) : 0;
                ShowDesktopButtonStart.Margin = StartArea.Orientation == Orientation.Vertical ? new Thickness(0, gap, 0, 0) : new Thickness(gap, 0, 0, 0);

                double available = barHeight;
                if (under && horizontal)
                {
                    // Its measured height includes the gap above it.
                    ShowDesktopButtonStart.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    available -= ShowDesktopButtonStart.DesiredSize.Height;
                }

                StartButton.ApplyLayout(available);

                if (horizontal && s.ClockOrientation != ElementOrientationOption.Horizontal)
                {
                    ClockHost.LayoutTransform = new RotateTransform(s.ClockOrientation == ElementOrientationOption.VerticalUp ? -90 : 90);
                }
                else
                {
                    ClockHost.ClearValue(LayoutTransformProperty);
                }
            }
            catch (Exception ex)
            {
                ManagedShell.Common.Logging.ShellLogger.Warning($"UXUI-Extreme: layout options not applied: {ex.Message}");
            }
        }
    }
}
