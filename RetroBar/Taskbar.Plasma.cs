using RetroBar.Utilities;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RetroBar
{
    public partial class Taskbar
    {
        private DispatcherTimer _plasmaTimer;
        private readonly Stopwatch _plasmaClock = new Stopwatch();
        private WriteableBitmap _plasmaBitmap;
        private byte[] _plasmaPixels;
        private bool _plasmaDisposed;

        private void InitializePlasmaBackground()
        {
            _plasmaTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
            _plasmaTimer.Tick += PlasmaTimer_Tick;
            Loaded += (s, e) => RefreshPlasmaBackground();
            Unloaded += (s, e) => StopPlasmaBackground();
            IsVisibleChanged += (s, e) => RefreshPlasmaBackground();
            Closed += (s, e) =>
            {
                StopPlasmaBackground();
                _plasmaTimer.Tick -= PlasmaTimer_Tick;
                _plasmaDisposed = true;
            };
            RenderOptions.SetBitmapScalingMode(PlasmaBackgroundSurface, BitmapScalingMode.LowQuality);
        }

        private void RefreshPlasmaBackground()
        {
            if (_plasmaDisposed) return;
            if (!Settings.Instance.PlasmaBackgroundEnabled || !IsLoaded || !IsVisible)
            {
                StopPlasmaBackground();
                return;
            }
            _plasmaTimer.Interval = TimeSpan.FromSeconds(1.0 / Settings.Instance.PlasmaFrameRate);
            if (!_plasmaTimer.IsEnabled)
            {
                _plasmaClock.Restart();
                _plasmaTimer.Start();
                RenderPlasmaFrame();
            }
        }

        private void StopPlasmaBackground()
        {
            _plasmaTimer?.Stop();
            _plasmaClock.Reset();
            _plasmaBitmap = null;
            _plasmaPixels = null;
            if (PlasmaBackgroundSurface != null)
                PlasmaBackgroundSurface.Background = null;
        }

        private void PlasmaTimer_Tick(object sender, EventArgs e)
        {
            if (!Settings.Instance.PlasmaBackgroundEnabled || !IsVisible)
            {
                StopPlasmaBackground();
                return;
            }
            RenderPlasmaFrame();
        }

        private void RenderPlasmaFrame()
        {
            var dpi = VisualTreeHelper.GetDpi(PlasmaBackgroundSurface);
            var size = PlasmaRenderer.RecommendedSize(
                PlasmaBackgroundSurface.ActualWidth * dpi.DpiScaleX,
                PlasmaBackgroundSurface.ActualHeight * dpi.DpiScaleY,
                Settings.Instance.PlasmaResolutionDivisor);
            if (size.Width == 0 || size.Height == 0) return;
            if (_plasmaBitmap == null || _plasmaBitmap.PixelWidth != size.Width
                || _plasmaBitmap.PixelHeight != size.Height)
            {
                _plasmaBitmap = new WriteableBitmap(size.Width, size.Height, 96, 96, PixelFormats.Bgra32, null);
                _plasmaPixels = new byte[size.Width * size.Height * 4];
                PlasmaBackgroundSurface.Background = new ImageBrush(_plasmaBitmap)
                {
                    Stretch = Stretch.Fill, TileMode = TileMode.None,
                };
            }
            // Keep the wave's spatial wavelength fixed when the user changes
            // the resolution divisor. Plasma coordinates use 16 device pixels/cell.
            double sampleX = PlasmaBackgroundSurface.ActualWidth * dpi.DpiScaleX / (16.0 * size.Width);
            double sampleY = PlasmaBackgroundSurface.ActualHeight * dpi.DpiScaleY / (16.0 * size.Height);
            PlasmaRenderer.Render(_plasmaPixels, size.Width, size.Height,
                _plasmaClock.Elapsed.TotalSeconds, sampleX, sampleY);
            _plasmaBitmap.WritePixels(new Int32Rect(0, 0, size.Width, size.Height),
                _plasmaPixels, size.Width * 4, 0);
        }
    }
}
