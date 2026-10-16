using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RetroBar.Utilities
{
    // UXUI-Extreme: icons on the taskbar are often drawn at a size they weren't made for (32px
    // app icons at a 125% taskbar, a custom 48px PNG in a 16px slot). WPF's default bitmap
    // scaling is a simple linear filter that shimmers and loses detail when shrinking, so every
    // image drawn in a taskbar window gets high-quality scaling. Images drawn 1:1 are unaffected.
    public static class TaskbarImageQuality
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            EventManager.RegisterClassHandler(typeof(Image), FrameworkElement.LoadedEvent, new RoutedEventHandler(Image_Loaded));
        }

        private static void Image_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Image image && Window.GetWindow(image) is Taskbar &&
                RenderOptions.GetBitmapScalingMode(image) == BitmapScalingMode.Unspecified)
            {
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            }
        }
    }
}
