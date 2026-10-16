using ManagedShell.AppBar;
using ManagedShell.Common.Helpers;
using ManagedShell.WindowsTray;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RetroBar.Utilities
{
    internal class Settings : INotifyPropertyChanged, IMigratableSettings
    {
        private static Settings instance;

        public static Settings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = _settingsManager.Settings;
                    _isInitializing = false;
                }

                return instance;
            }
        }

        private static string _settingsPath = "settings.json".InLocalAppData();
        private static bool _isInitializing = true;
        private static SettingsManager<Settings> _settingsManager = new(_settingsPath, new Settings());

        private bool _migrationPerformed = false;
        public bool MigrationPerformed { get => _migrationPerformed; }
        public event PropertyChangedEventHandler PropertyChanged;

        // This should not be used directly! Unfortunately it must be public for JsonSerializer.
        public Settings()
        {
            PropertyChanged += Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isInitializing)
            {
                return;
            }

            _settingsManager.Settings = this;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (!field.Equals(value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        protected void SetEnum<T>(ref T field, T value, [CallerMemberName] string propertyName = "") where T : Enum
        {
            if (!field.Equals(value))
            {
                if (!Enum.IsDefined(typeof(T), value))
                {
                    return;
                }

                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        #region Properties
        private string _language = "System";
        public string Language
        {
            get => _language;
            set => Set(ref _language, value);
        }

        private string _theme = "Windows 95-98";
        public string Theme
        {
            get => _theme;
            set => Set(ref _theme, value);
        }

        private bool _showInputLanguage = false;
        public bool ShowInputLanguage
        {
            get => _showInputLanguage;
            set => Set(ref _showInputLanguage, value);
        }

        private bool _showClock = true;
        public bool ShowClock
        {
            get => _showClock;
            set => Set(ref _showClock, value);
        }

        private bool _overrideClockFormat = false;
        public bool OverrideClockFormat
        {
            get
            {
                return _overrideClockFormat;
            }
            set
            {
                if (_overrideClockFormat != value)
                {
                    _overrideClockFormat = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _clockFormat = "ddd MMM d  h:mm:ss tt";
        public string ClockFormat
        {
            get
            {
                return _clockFormat;
            }
            set
            {
                if (_clockFormat != value)
                {
                    _clockFormat = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _overrideAMPMDesignators = false;
        public bool OverrideAMPMDesignators
        {
            get
            {
                return _overrideAMPMDesignators;
            }
            set
            {
                if (_overrideAMPMDesignators != value)
                {
                    _overrideAMPMDesignators = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _amDesignator = "a.m.";
        public string AMDesignator
        {
            get
            {
                return _amDesignator;
            }
            set
            {
                if (_amDesignator != value)
                {
                    _amDesignator = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _pmDesignator = "p.m.";
        public string PMDesignator
        {
            get
            {
                return _pmDesignator;
            }
            set
            {
                if (_pmDesignator != value)
                {
                    _pmDesignator = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showDesktopButton = false;
        public bool ShowDesktopButton
        {
            get => _showDesktopButton;
            set => Set(ref _showDesktopButton, value);
        }

        private bool _peekAtDesktop = false;
        public bool PeekAtDesktop
        {
            get => _peekAtDesktop;
            set => Set(ref _peekAtDesktop, value);
        }

        private bool _showMultiMon = false;
        public bool ShowMultiMon
        {
            get => _showMultiMon;
            set => Set(ref _showMultiMon, value);
        }

        private bool _showQuickLaunch = true;
        public bool ShowQuickLaunch
        {
            get => _showQuickLaunch;
            set => Set(ref _showQuickLaunch, value);
        }

        private string _quickLaunchPath = "%appdata%\\Microsoft\\Internet Explorer\\Quick Launch\\User Pinned\\TaskBar";
        public string QuickLaunchPath
        {
            get => _quickLaunchPath;
            set => Set(ref _quickLaunchPath, value);
        }

        private bool _collapseNotifyIcons = false;
        public bool CollapseNotifyIcons
        {
            get => _collapseNotifyIcons;
            set => Set(ref _collapseNotifyIcons, value);
        }

        private List<string> _invertNotifyIcons = new List<string> { NotificationArea.HARDWARE_GUID, NotificationArea.UPDATE_GUID, NotificationArea.MICROPHONE_GUID, NotificationArea.LOCATION_GUID, NotificationArea.MEETNOW_GUID, NotificationArea.NETWORK_GUID, NotificationArea.POWER_GUID, NotificationArea.VOLUME_GUID };
        public List<string> InvertNotifyIcons
        {
            get => _invertNotifyIcons;
            set => Set(ref _invertNotifyIcons, value);
        }

        private List<NotifyIconBehaviorSetting> _notifyIconBehaviors = new List<NotifyIconBehaviorSetting>
        {
            new NotifyIconBehaviorSetting
            {
                Identifier = NotificationArea.HEALTH_GUID,
                Behavior = NotifyIconBehavior.AlwaysShow
            },
            new NotifyIconBehaviorSetting
            {
                Identifier = NotificationArea.POWER_GUID,
                Behavior = NotifyIconBehavior.AlwaysShow
            },
            new NotifyIconBehaviorSetting
            {
                Identifier = NotificationArea.NETWORK_GUID,
                Behavior = NotifyIconBehavior.AlwaysShow
            },
            new NotifyIconBehaviorSetting
            {
                Identifier = NotificationArea.VOLUME_GUID,
                Behavior = NotifyIconBehavior.AlwaysShow
            },
        };
        public List<NotifyIconBehaviorSetting> NotifyIconBehaviors
        {
            get => _notifyIconBehaviors;
            set => Set(ref _notifyIconBehaviors, value);
        }

        private List<string> _notifyIconOrder = new List<string>();
        public List<string> NotifyIconOrder
        {
            get => _notifyIconOrder;
            set => Set(ref _notifyIconOrder, value);
        }

        private bool _allowFontSmoothing = false;
        public bool AllowFontSmoothing
        {
            get => _allowFontSmoothing;
            set => Set(ref _allowFontSmoothing, value);
        }

        private bool _allowFontSmoothingMenu = true;
        public bool AllowFontSmoothingMenu
        {
            get => _allowFontSmoothingMenu;
            set => Set(ref _allowFontSmoothingMenu, value);
        }

        private bool _useSoftwareRendering = false;
        public bool UseSoftwareRendering
        {
            get => _useSoftwareRendering;
            set => Set(ref _useSoftwareRendering, value);
        }

        private AppBarEdge _edge = AppBarEdge.Bottom;
        public AppBarEdge Edge
        {
            get => _edge;
            set => SetEnum(ref _edge, value);
        }

        private int _rowCount = 1;
        public int RowCount
        {
            get => _rowCount;
            set => Set(ref _rowCount, value);
        }

        private int _rowLimit = 5;
        public int RowLimit
        {
            get => _rowLimit;
            set => Set(ref _rowLimit, value);
        }

        private int _taskbarWidth = 1;
        public int TaskbarWidth
        {
            get => _taskbarWidth;
            set => Set(ref _taskbarWidth, value);
        }

        private int _taskbarWidthLimit = 7;
        public int TaskbarWidthLimit
        {
            get => _taskbarWidthLimit;
            set => Set(ref _taskbarWidthLimit, value);
        }

        private List<string> _quickLaunchOrder = [];
        public List<string> QuickLaunchOrder
        {
            get => _quickLaunchOrder;
            set => Set(ref _quickLaunchOrder, value);
        }

        private bool _showTaskThumbnails = false;
        public bool ShowTaskThumbnails
        {
            get => _showTaskThumbnails;
            set => Set(ref _showTaskThumbnails, value);
        }

        private MultiMonOption _multiMonMode = MultiMonOption.AllTaskbars;
        public MultiMonOption MultiMonMode
        {
            get => _multiMonMode;
            set => SetEnum(ref _multiMonMode, value);
        }

        private double _taskbarScale = 1.0;
        public double TaskbarScale
        {
            get => _taskbarScale;
            set => Set(ref _taskbarScale, value);
        }

        private bool _debugLogging = false;
        public bool DebugLogging
        {
            get => _debugLogging;
            set => Set(ref _debugLogging, value);
        }

        private bool _autoHide = false;
        public bool AutoHide
        {
            get => _autoHide;
            set => Set(ref _autoHide, value);
        }

        private bool _lockTaskbar = false;
        public bool LockTaskbar
        {
            get => _lockTaskbar;
            set => Set(ref _lockTaskbar, value);
        }

        private InvertIconsOption _invertIconsMode = EnvironmentHelper.IsWindows10OrBetter ? InvertIconsOption.WhenNeededByTheme : InvertIconsOption.Never;
        public InvertIconsOption InvertIconsMode
        {
            get => _invertIconsMode;
            set => SetEnum(ref _invertIconsMode, value);
        }

        // UXUI-Extreme (UX-006): Native = draw at real size (default); Stretch = upstream zoom.
        private ScalingModeOption _scalingMode = ScalingModeOption.Native;
        public ScalingModeOption ScalingMode
        {
            get => _scalingMode;
            set => SetEnum(ref _scalingMode, value);
        }

        // UXUI-Extreme (UX-002): experimental crisp text when scaled. Off = stock behaviour.
        private PixelTextOption _pixelTextMode = PixelTextOption.Off;
        public PixelTextOption PixelTextMode
        {
            get => _pixelTextMode;
            set => SetEnum(ref _pixelTextMode, value);
        }

        // UXUI-Extreme (UX-005): taskbar font over any theme. Empty/0/false = theme decides.
        private string _fontOverrideFamily = "";
        public string FontOverrideFamily
        {
            get => _fontOverrideFamily;
            set => Set(ref _fontOverrideFamily, value ?? "");
        }

        private double _fontOverrideSize = 0; // px at 100% scale
        public double FontOverrideSize
        {
            get => _fontOverrideSize;
            set => Set(ref _fontOverrideSize, value);
        }

        private int _fontOverrideWeight = 0; // OpenType weight 100-900
        public int FontOverrideWeight
        {
            get => _fontOverrideWeight;
            set => Set(ref _fontOverrideWeight, value);
        }

        // UXUI-Extreme (UX-008): line spacing as a multiple of font size; 0 = the font's own spacing.
        private double _fontOverrideLineSpacing = 0;
        public double FontOverrideLineSpacing
        {
            get => _fontOverrideLineSpacing;
            set => Set(ref _fontOverrideLineSpacing, value);
        }

        // UXUI-Extreme (UX-008): hide clock lines that don't fit (never adds lines)
        private ClockLinesOption _clockLines = ClockLinesOption.ThemeDefault;
        public ClockLinesOption ClockLines
        {
            get => _clockLines;
            set => SetEnum(ref _clockLines, value);
        }

        // UXUI-Extreme (UX-008): squeeze stacked clock lines together when they don't fit
        private bool _clockFitLines = true;
        public bool ClockFitLines
        {
            get => _clockFitLines;
            set => Set(ref _clockFitLines, value);
        }

        // UXUI-Extreme (UX-008): centred time (theme look) or anchored so only changing digits move
        private ClockTimeAlignmentOption _clockTimeAlignment = ClockTimeAlignmentOption.Centred;
        public ClockTimeAlignmentOption ClockTimeAlignment
        {
            get => _clockTimeAlignment;
            set => SetEnum(ref _clockTimeAlignment, value);
        }

        // UXUI-Extreme (UX-007): manual row height fine-tuning, px at 100% scale (-10..+20).
        private double _rowHeightAdjust = 0;
        public double RowHeightAdjust
        {
            get => _rowHeightAdjust;
            set => Set(ref _rowHeightAdjust, value);
        }

        private bool _fontOverrideItalic = false;
        public bool FontOverrideItalic
        {
            get => _fontOverrideItalic;
            set => Set(ref _fontOverrideItalic, value);
        }

        // UXUI-Extreme (UX-014): task buttons for all apps. 0 = theme width (px at 100% scale).
        private double _taskButtonWidthPx = 0;
        public double TaskButtonWidthPx
        {
            get => _taskButtonWidthPx;
            set => Set(ref _taskButtonWidthPx, value);
        }

        private bool _taskButtonShowIcons = true;
        public bool TaskButtonShowIcons
        {
            get => _taskButtonShowIcons;
            set => Set(ref _taskButtonShowIcons, value);
        }

        private bool _taskButtonShowLabels = true;
        public bool TaskButtonShowLabels
        {
            get => _taskButtonShowLabels;
            set => Set(ref _taskButtonShowLabels, value);
        }

        // UXUI-Extreme (UX-014): per-app task button settings, remembered by app (see TaskButtonRules).
        // Replaced as a whole list on every change so PropertyChanged fires.
        private List<TaskButtonAppRule> _taskButtonAppRules = new List<TaskButtonAppRule>();
        public List<TaskButtonAppRule> TaskButtonAppRules
        {
            get => _taskButtonAppRules;
            set => Set(ref _taskButtonAppRules, value ?? new List<TaskButtonAppRule>());
        }

        // UXUI-Extreme (UX-013): multiplier for all theme border widths (0 = no borders, 1 = theme).
        private double _borderThicknessFactor = 1;
        public double BorderThicknessFactor
        {
            get => _borderThicknessFactor;
            set => Set(ref _borderThicknessFactor, value);
        }

        // UXUI-Extreme (UX-012): Start button layout. Defaults = the theme's own look.
        private ElementOrientationOption _startButtonOrientation = ElementOrientationOption.Horizontal;
        public ElementOrientationOption StartButtonOrientation
        {
            get => _startButtonOrientation;
            set => SetEnum(ref _startButtonOrientation, value);
        }

        // Length along the button's text: Automatic = fill the taskbar height when vertical,
        // the theme's size when horizontal. Custom = StartButtonLengthPx (at 100% scale).
        private SizeModeOption _startButtonLengthMode = SizeModeOption.Automatic;
        public SizeModeOption StartButtonLengthMode
        {
            get => _startButtonLengthMode;
            set => SetEnum(ref _startButtonLengthMode, value);
        }

        private double _startButtonLengthPx = 80;
        public double StartButtonLengthPx
        {
            get => _startButtonLengthPx;
            set => Set(ref _startButtonLengthPx, value);
        }

        // Thickness across the text: Automatic = keep the button's proportions.
        // UXUI-Extreme (UX-012)
        private StretchModeOption _startButtonStretchMode = StretchModeOption.KeepEnds;
        public StretchModeOption StartButtonStretchMode
        {
            get => _startButtonStretchMode;
            set => SetEnum(ref _startButtonStretchMode, value);
        }

        private SizeModeOption _startButtonThicknessMode = SizeModeOption.Automatic;
        public SizeModeOption StartButtonThicknessMode
        {
            get => _startButtonThicknessMode;
            set => SetEnum(ref _startButtonThicknessMode, value);
        }

        private double _startButtonThicknessPx = 30;
        public double StartButtonThicknessPx
        {
            get => _startButtonThicknessPx;
            set => Set(ref _startButtonThicknessPx, value);
        }

        private VerticalAnchorOption _startButtonAnchor = VerticalAnchorOption.Centre;
        public VerticalAnchorOption StartButtonAnchor
        {
            get => _startButtonAnchor;
            set => SetEnum(ref _startButtonAnchor, value);
        }

        private bool _startButtonShowIcon = true;
        public bool StartButtonShowIcon
        {
            get => _startButtonShowIcon;
            set => Set(ref _startButtonShowIcon, value);
        }

        private bool _startButtonShowText = true;
        public bool StartButtonShowText
        {
            get => _startButtonShowText;
            set => Set(ref _startButtonShowText, value);
        }

        // UXUI-Extreme (UX-012)
        private ShowDesktopPositionOption _showDesktopPosition = ShowDesktopPositionOption.TaskbarEnd;
        public ShowDesktopPositionOption ShowDesktopPosition
        {
            get => _showDesktopPosition;
            set => SetEnum(ref _showDesktopPosition, value);
        }

        // UXUI-Extreme (UX-012): clock layout and its own font. Empty/0 = same as the taskbar font.
        private ElementOrientationOption _clockOrientation = ElementOrientationOption.Horizontal;
        public ElementOrientationOption ClockOrientation
        {
            get => _clockOrientation;
            set => SetEnum(ref _clockOrientation, value);
        }

        private string _clockFontFamily = "";
        public string ClockFontFamily
        {
            get => _clockFontFamily;
            set => Set(ref _clockFontFamily, value ?? "");
        }

        private double _clockFontSize = 0; // px at 100% scale
        public double ClockFontSize
        {
            get => _clockFontSize;
            set => Set(ref _clockFontSize, value);
        }

        private int _clockFontWeight = 0; // OpenType weight 100-900
        public int ClockFontWeight
        {
            get => _clockFontWeight;
            set => Set(ref _clockFontWeight, value);
        }

        private ClockItalicOption _clockFontItalic = ClockItalicOption.SameAsTaskbar;
        public ClockItalicOption ClockFontItalic
        {
            get => _clockFontItalic;
            set => SetEnum(ref _clockFontItalic, value);
        }

        private double _clockLineSpacing = 0; // multiple of font size
        public double ClockLineSpacing
        {
            get => _clockLineSpacing;
            set => Set(ref _clockLineSpacing, value);
        }

        private bool _showTaskBadges = true;
        public bool ShowTaskBadges
        {
            get => _showTaskBadges;
            set => Set(ref _showTaskBadges, value);
        }

        private TaskMiddleClickOption _taskMiddleClickAction = TaskMiddleClickOption.OpenNewInstance;
        public TaskMiddleClickOption TaskMiddleClickAction
        {
            get => _taskMiddleClickAction;
            set => SetEnum(ref _taskMiddleClickAction, value);
        }

        private TaskWheelActionOption _taskWheelAction = TaskWheelActionOption.DoNothing;
        public TaskWheelActionOption TaskWheelAction
        {
            get => _taskWheelAction;
            set => SetEnum(ref _taskWheelAction, value);
        }

        private ClockClickOption _clockClickAction = EnvironmentHelper.IsWindows10OrBetter ? ClockClickOption.OpenNotificationCenter : ClockClickOption.DoNothing;
        public ClockClickOption ClockClickAction
        {
            get
            {
                // On Windows versions prior to 10, neither the Modern calendar nor the Notification Center is available
                if (!EnvironmentHelper.IsWindows10OrBetter && _clockClickAction > ClockClickOption.OpenAeroCalendar)
                {
                    return ClockClickOption.DoNothing;
                }

                return _clockClickAction;
            }
            set => SetEnum(ref _clockClickAction, value);
        }

        private bool _checkForUpdates = true;
        public bool CheckForUpdates
        {
            get => _checkForUpdates;
            set => Set(ref _checkForUpdates, value);
        }

        private bool _showExitMenuItem = true;
        public bool ShowExitMenuItem
        {
            get => _showExitMenuItem;
            set => Set(ref _showExitMenuItem, value);
        }

        private bool _showEndTaskButton = false;
        public bool ShowEndTaskButton
        {
            get => _showEndTaskButton;
            set => Set(ref _showEndTaskButton, value);
        }

        private bool _showStartButtonMultiMon = false;
        public bool ShowStartButtonMultiMon
        {
            get => _showStartButtonMultiMon;
            set => Set(ref _showStartButtonMultiMon, value);
        }

        private bool _autoHideTransparent = false;
        public bool AutoHideTransparent
        {
            get => _autoHideTransparent;
            set => Set(ref _autoHideTransparent, value);
        }

        private bool _slideTaskbarButtons = false;
        public bool SlideTaskbarButtons
        {
            get => _slideTaskbarButtons;
            set => Set(ref _slideTaskbarButtons, value);
        }

        private bool _showClockSeconds = false;
        public bool ShowClockSeconds
        {
            get => _showClockSeconds;
            set => Set(ref _showClockSeconds, value);
        }

        private WinNumHotkeysOption _winNumHotkeysAction = WinNumHotkeysOption.WindowsDefault;
        public WinNumHotkeysOption WinNumHotkeysAction
        {
            get => _winNumHotkeysAction;
            set => SetEnum(ref _winNumHotkeysAction, value);
        }

        private bool _allowBlurBehind = true;
        public bool AllowBlurBehind
        {
            get => _allowBlurBehind;
            set => Set(ref _allowBlurBehind, value);
        }
        #endregion

        #region Old Properties
        public bool? MiddleMouseToClose
        {
            get => null;
            set
            {
                // Migrate to TaskMiddleClickAction
                if (value != null)
                {
                    TaskMiddleClickAction = (bool)value ? TaskMiddleClickOption.CloseTask : TaskMiddleClickOption.OpenNewInstance;
                    _migrationPerformed = true;
                }
            }
        }

        public string[] PinnedNotifyIcons
        {
            get => [];
            set
            {
                // Migrate to NotifyIconBehaviors
                if (value.Length > 0)
                {
                    var newSettings = new List<NotifyIconBehaviorSetting>();

                    foreach (var identifier in value)
                    {
                        newSettings.Add(new NotifyIconBehaviorSetting
                        {
                            Identifier = identifier,
                            Behavior = NotifyIconBehavior.AlwaysShow
                        });
                    }

                    NotifyIconBehaviors = newSettings;
                    _migrationPerformed = true;
                }
            }
        }
        #endregion
    }

    #region Enums
    // UXUI-Extreme (UX-014): a per-app override of an all-apps setting
    public enum AppOverrideOption
    {
        Default,
        Show,
        Hide
    }

    // UXUI-Extreme (UX-014): one app's remembered task button settings.
    public class TaskButtonAppRule
    {
        public string App { get; set; } = "";   // exe file name (lower case) or AppUserModelID
        public string Name { get; set; } = "";  // shown in the Layout tab
        public double Width { get; set; }       // 0 = default; -1 = icon only; otherwise px at 100%
        public AppOverrideOption Icon { get; set; }
        public AppOverrideOption Label { get; set; }

        public TaskButtonAppRule Clone() => (TaskButtonAppRule)MemberwiseClone();
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEmpty => Width == 0 && Icon == AppOverrideOption.Default && Label == AppOverrideOption.Default;
    }

    // UXUI-Extreme (UX-012): rotation for the Start button and clock on horizontal taskbars
    public enum ElementOrientationOption
    {
        Horizontal,
        VerticalUp,   // turned 90° anticlockwise: reads bottom to top
        VerticalDown  // turned 90° clockwise: reads top to bottom
    }

    // UXUI-Extreme (UX-012): how the Start button is made longer or shorter
    public enum StretchModeOption
    {
        KeepEnds, // repeat a middle slice of the image (or widen a drawn button); ends not distorted
        Scale     // scale the whole button
    }

    // UXUI-Extreme (UX-012)
    public enum SizeModeOption
    {
        Automatic,
        Custom
    }

    // UXUI-Extreme (UX-012): where the Start button sits along the taskbar's height
    public enum VerticalAnchorOption
    {
        Top,
        Centre,
        Bottom
    }

    // UXUI-Extreme (UX-012)
    public enum ShowDesktopPositionOption
    {
        TaskbarEnd,
        BesideStart,
        UnderStart
    }

    // UXUI-Extreme (UX-012): clock italic, independent of the taskbar font
    public enum ClockItalicOption
    {
        SameAsTaskbar,
        Italic,
        Upright
    }

    // UXUI-Extreme (UX-008)
    public enum ClockLinesOption
    {
        ThemeDefault,
        TimeAndDay,
        TimeOnly
    }

    // UXUI-Extreme (UX-008)
    public enum ClockTimeAlignmentOption
    {
        Centred,
        Anchored
    }

    // UXUI-Extreme (UX-006)
    public enum ScalingModeOption
    {
        Native,
        Stretch
    }

    // UXUI-Extreme (UX-002)
    public enum PixelTextOption
    {
        Off,
        NativeWholePixels,
        NativeExact
    }

    public enum InvertIconsOption
    {
        WhenNeededByTheme,
        Always,
        Never
    }

    public enum MultiMonOption
    {
        AllTaskbars,
        SameAsWindow,
        SameAsWindowAndPrimary
    }

    public enum TaskMiddleClickOption
    {
        DoNothing,
        OpenNewInstance,
        CloseTask
    }
    public enum TaskWheelActionOption
    {
        DoNothing,
        ShowHideWindow
    }

    public enum ClockClickOption
    {
        DoNothing,
        OpenAeroCalendar,
        OpenModernCalendar,
        OpenNotificationCenter,
    }

    public enum WinNumHotkeysOption
    {
        WindowsDefault,
        SwitchTasks,
        InvokeQuickLaunch,
    }

    public enum NotifyIconBehavior
    {
        HideWhenInactive,
        AlwaysHide,
        AlwaysShow,
        Remove
    }
    #endregion

    #region Structs
    public struct NotifyIconBehaviorSetting
    {
        public string Identifier {  get; set; }
        public NotifyIconBehavior Behavior { get; set; }
    }
    #endregion
}