namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-006): one place that answers "how is the taskbar scaled right now?"
    //
    // Native (default): the theme is rescaled as it loads (ThemeScaler) and text is set to its
    //   real size, so nothing is stretched. TransformScale = 1, ThemeScale = TaskbarScale.
    // Stretch (upstream behaviour): the theme loads at 100% and the whole taskbar is stretched
    //   by a LayoutTransform. TransformScale = TaskbarScale, ThemeScale = 1.
    public static class ScaleModel
    {
        /// <summary>Native sizing is selected and the scale is not 100%.</summary>
        public static bool IsNative =>
            Settings.Instance.ScalingMode == ScalingModeOption.Native && Settings.Instance.TaskbarScale != 1.0;

        /// <summary>Scale applied by the taskbar's LayoutTransform, and by code that multiplies theme sizes.</summary>
        public static double TransformScale => IsNative ? 1.0 : Settings.Instance.TaskbarScale;

        /// <summary>Scale baked into theme sizes when the theme XAML is loaded.</summary>
        public static double ThemeScale => IsNative ? Settings.Instance.TaskbarScale : 1.0;
    }
}
