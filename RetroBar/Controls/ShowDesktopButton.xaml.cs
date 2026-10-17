using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using ManagedShell.WindowsTasks;
using RetroBar.Utilities;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RetroBar.Controls
{
    /// <summary>
    /// Interaction logic for ShowDesktopButton.xaml
    /// </summary>
    public partial class ShowDesktopButton : UserControl
    {
        private const int TOGGLE_DESKTOP = 407;
        private IntPtr taskbarHandle = Process.GetCurrentProcess().MainWindowHandle;
        private bool isWindows81OrBetter = EnvironmentHelper.IsWindows81OrBetter;
        private bool isLoaded;
        private DelayedActivationHandler dragHandler;

        public static DependencyProperty TasksServiceProperty = DependencyProperty.Register(nameof(TasksService), typeof(TasksService), typeof(ShowDesktopButton), new PropertyMetadata(TasksChangedCallback));

        public TasksService TasksService
        {
            get { return (TasksService)GetValue(TasksServiceProperty); }
            set { SetValue(TasksServiceProperty, value); }
        }

        public ShowDesktopButton()
        {
            InitializeComponent();
        }

        private static void TasksChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ShowDesktopButton button && e.OldValue == null && e.NewValue != null)
            {
                button.SetupButton();
            }
        }

        // UXUI-Extreme: themes draw the Show desktop button with a (translucent) fill and border made of
        // nested Border elements; with the option off every one of them loses its thickness and fill.
        private void ApplyBorder()
        {
            ShowDesktop.ApplyTemplate();
            bool show = Settings.Instance.ShowDesktopBorder;
            foreach (System.Windows.Controls.Border border in Descendants<System.Windows.Controls.Border>(ShowDesktop))
            {
                if (show)
                {
                    border.ClearValue(System.Windows.Controls.Border.BorderThicknessProperty);
                    border.ClearValue(System.Windows.Controls.Border.BackgroundProperty);
                }
                else
                {
                    border.BorderThickness = new Thickness(0);
                    border.Background = System.Windows.Media.Brushes.Transparent;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    yield return match;
                }
                foreach (T nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private void SetIconSize()
        {
            if (DpiHelper.DpiScale > 1 || Settings.Instance.TaskbarScale > 1)
            {
                ShowDesktopIcon.Source = (System.Windows.Media.ImageSource)FindResource("ShowDesktopIconImageLarge");
            }
            else
            {
                ShowDesktopIcon.Source = (System.Windows.Media.ImageSource)FindResource("ShowDesktopIconImageSmall");
            }

            // UXUI-Extreme: a user's PNG in place of the theme's picture, and where it sits.
            if (CustomIcons.Load(Settings.Instance.ShowDesktopIcon, CustomIcons.DevicePixels(ShowDesktopIcon)) is System.Windows.Media.Imaging.BitmapSource custom)
            {
                ShowDesktopIcon.Source = custom;
            }
            ShowDesktopIcon.HorizontalAlignment = Settings.Instance.ShowDesktopIconAlign switch
            {
                TaskButtonAlignOption.Left => HorizontalAlignment.Left,
                TaskButtonAlignOption.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center,
            };
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            PeekAtDesktopItem.IsEnabled = true;
            if (!NativeMethods.DwmIsCompositionEnabled())
            {
                PeekAtDesktopItem.IsEnabled = false;
            }
        }

        private void ToggleDesktop()
        {
            NativeMethods.SendMessage(WindowHelper.FindWindowsTray(IntPtr.Zero),
                (int)NativeMethods.WM.COMMAND, (IntPtr)TOGGLE_DESKTOP, IntPtr.Zero);
        }

        private void PeekAtDesktop(uint shouldPeek)
        {
            if (Settings.Instance.PeekAtDesktop && NativeMethods.DwmIsCompositionEnabled())
            {
                if (isWindows81OrBetter)
                {
                    NativeMethods.DwmActivateLivePreview(shouldPeek, taskbarHandle,
                        IntPtr.Zero, NativeMethods.AeroPeekType.Desktop, IntPtr.Zero);
                }
                else
                {
                    NativeMethods.DwmActivateLivePreview(shouldPeek, taskbarHandle,
                        IntPtr.Zero, NativeMethods.AeroPeekType.Desktop);
                }
            }
        }

        private void ShowDesktop_OnMouseEnter(object sender, RoutedEventArgs e)
        {
            PeekAtDesktop(1);
        }

        private void ShowDesktop_OnMouseLeave(object sender, RoutedEventArgs e)
        {
            PeekAtDesktop(0);
        }

        private void ShowDesktop_OnClick(object sender, RoutedEventArgs e)
        {
            // If the user activates a window other than the desktop, HandleWindowActivated will deselect the button.
            ToggleDesktop();
        }

        private void OpenDisplayPropertiesCpl()
        {
            ShellHelper.StartProcess("desk.cpl");
        }

        private void PropertiesItem_OnClick(object sender, RoutedEventArgs e)
        {
            OpenDisplayPropertiesCpl();
        }

        private void HandleWindowActivated(object sender, WindowEventArgs e)
        {
            if (ShowDesktop.IsChecked == true)
            {
                ShowDesktop.IsChecked = false;
            }
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.ShowDesktopBorder))
            {
                ApplyBorder();
            }

            if (e.PropertyName == nameof(Settings.TaskbarScale) ||
                e.PropertyName == nameof(Settings.ShowDesktopIcon) ||
                e.PropertyName == nameof(Settings.ShowDesktopIconAlign))
            {
                SetIconSize();
            }
        }

        private void SetupButton()
        {
            if (!isLoaded && TasksService != null)
            {
                SetIconSize();
                ApplyBorder();
                TasksService.WindowActivated += HandleWindowActivated;

                Settings.Instance.PropertyChanged += Settings_PropertyChanged;

                dragHandler = new DelayedActivationHandler(() =>
                {
                    if (ShowDesktop.IsChecked == false)
                    {
                        ToggleDesktop();
                        ShowDesktop.IsChecked = true;
                    }
                });

                isLoaded = true;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            SetupButton();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (isLoaded && TasksService != null)
            {
                TasksService.WindowActivated -= HandleWindowActivated;
                Settings.Instance.PropertyChanged -= Settings_PropertyChanged;
                dragHandler?.Dispose();
                isLoaded = false;
            }
        }

        private void ShowDesktop_DragEnter(object sender, DragEventArgs e)
        {
            dragHandler?.OnDragEnter(e);
        }

        private void ShowDesktop_DragLeave(object sender, DragEventArgs e)
        {
            dragHandler?.OnDragLeave();
        }
    }
}