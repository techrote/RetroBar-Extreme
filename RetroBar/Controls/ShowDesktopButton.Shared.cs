using System;
using System.Diagnostics;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using RetroBar.Utilities;

namespace RetroBar.Controls
{
    // UXUI-Extreme: the Show desktop actions without a button, for the quick launch area's own Show desktop icon.
    public partial class ShowDesktopButton
    {
        internal static void ToggleDesktopNow()
        {
            NativeMethods.SendMessage(WindowHelper.FindWindowsTray(IntPtr.Zero),
                (int)NativeMethods.WM.COMMAND, (IntPtr)TOGGLE_DESKTOP, IntPtr.Zero);
        }

        internal static void PeekNow(uint shouldPeek)
        {
            if (Settings.Instance.PeekAtDesktop && NativeMethods.DwmIsCompositionEnabled())
            {
                IntPtr taskbar = Process.GetCurrentProcess().MainWindowHandle;
                if (EnvironmentHelper.IsWindows81OrBetter)
                {
                    NativeMethods.DwmActivateLivePreview(shouldPeek, taskbar, IntPtr.Zero, NativeMethods.AeroPeekType.Desktop, IntPtr.Zero);
                }
                else
                {
                    NativeMethods.DwmActivateLivePreview(shouldPeek, taskbar, IntPtr.Zero, NativeMethods.AeroPeekType.Desktop);
                }
            }
        }
    }
}
