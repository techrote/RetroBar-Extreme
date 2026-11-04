using System;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-019): updates are manual. Keep the small upstream-facing interface
    // used by App/WindowManager/Taskbar, but never create an HTTP client, timer or settings
    // subscription. In particular, an older settings.json with CheckForUpdates=true must
    // not re-enable background requests or offer a stock RetroBar build over this fork.
    public class Updater : IDisposable
    {
        public const string ReleasesUrl = "https://github.com/techrote/RetroBar-Extreme/releases";
        public static Uri ReleasesUri { get; } = new Uri(ReleasesUrl);

        public bool IsUpdateAvailable => false;

        public readonly string DownloadUrl = ReleasesUrl;

        public void Dispose()
        {
            // There are no background resources to stop or dispose.
        }
    }
}
