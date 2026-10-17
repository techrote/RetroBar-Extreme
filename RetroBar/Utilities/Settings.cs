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

        // UXUI-Extreme: the clock's own margins. Themes space the clock lines with uneven margins
        // (the XP family: 8 left / 11 right, a few px under each line), which a turned clock shows
        // as sitting off-centre and standing off the screen edge. Automatic evens them out when
        // the clock is turned and leaves a horizontal clock as the theme draws it.
        private ClockMarginOption _clockMarginMode = ClockMarginOption.Automatic;
        public ClockMarginOption ClockMarginMode
        {
            get => _clockMarginMode;
            set => SetEnum(ref _clockMarginMode, value);
        }

        // Extra room around the whole clock, as seen on screen: left, top, right, bottom (px at 100%).
        private bool _clockCustomMargin = false;
        public bool ClockCustomMargin
        {
            get => _clockCustomMargin;
            set => Set(ref _clockCustomMargin, value);
        }

        private string _clockMargin = "0,0,0,0";
        public string ClockMargin
        {
            get => _clockMargin;
            set => Set(ref _clockMargin, value ?? "0,0,0,0");
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

        // UXUI-Extreme (UX-015): user PNG icons (file names in %LOCALAPPDATA%RetroBarIcons; see CustomIcons).
        private string _startButtonIcon = "";
        public string StartButtonIcon
        {
            get => _startButtonIcon;
            set => Set(ref _startButtonIcon, value ?? "");
        }

        private List<QuickLaunchIconSetting> _quickLaunchIcons = new List<QuickLaunchIconSetting>();
        public List<QuickLaunchIconSetting> QuickLaunchIcons
        {
            get => _quickLaunchIcons;
            set => Set(ref _quickLaunchIcons, value ?? new List<QuickLaunchIconSetting>());
        }

        // UXUI-Extreme (UX-016): notification area expand/collapse toggle. "" = theme image.
        private string _trayToggleIcon = "";
        public string TrayToggleIcon
        {
            get => _trayToggleIcon;
            set => Set(ref _trayToggleIcon, value ?? "");
        }

        // UXUI-Extreme: a PNG for the whole Start button (see StartButton.Layout), and per-icon
        // replacements for notification area icons (see CustomIcons / NotifyIcon.Icons).
        private string _startButtonImage = "";
        public string StartButtonImage
        {
            get => _startButtonImage;
            set => Set(ref _startButtonImage, value ?? "");
        }

        private StartImageStatesOption _startButtonImageStates = StartImageStatesOption.One;
        public StartImageStatesOption StartButtonImageStates
        {
            get => _startButtonImageStates;
            set => Set(ref _startButtonImageStates, value);
        }

        private List<TrayIconReplacementSetting> _trayIconReplacements = new List<TrayIconReplacementSetting>();
        public List<TrayIconReplacementSetting> TrayIconReplacements
        {
            get => _trayIconReplacements;
            set => Set(ref _trayIconReplacements, value ?? new List<TrayIconReplacementSetting>());
        }

        // UXUI-Extreme: where the Start icon (and label) sit on the Start button, and a custom
        // picture for the Show desktop button with its own position.
        private TaskButtonAlignOption _startButtonIconAlign = TaskButtonAlignOption.Left;
        public TaskButtonAlignOption StartButtonIconAlign
        {
            get => _startButtonIconAlign;
            set => Set(ref _startButtonIconAlign, value);
        }

        private string _showDesktopIcon = "";
        public string ShowDesktopIcon
        {
            get => _showDesktopIcon;
            set => Set(ref _showDesktopIcon, value ?? "");
        }

        private TaskButtonAlignOption _showDesktopIconAlign = TaskButtonAlignOption.Centre;
        public TaskButtonAlignOption ShowDesktopIconAlign
        {
            get => _showDesktopIconAlign;
            set => Set(ref _showDesktopIconAlign, value);
        }

        // UXUI-Extreme: which rows of the task list particular apps use (see TaskRowPanel). Replaced as a
        // whole list on every change so PropertyChanged fires.
        private List<TaskRowRule> _taskRowRules = new List<TaskRowRule>();
        public List<TaskRowRule> TaskRowRules
        {
            get => _taskRowRules;
            set => Set(ref _taskRowRules, value ?? new List<TaskRowRule>());
        }

        // Apps whose buttons are placed after all the others (for example terminals), comma separated.
        private string _taskEndApps = "";
        public string TaskEndApps
        {
            get => _taskEndApps;
            set => Set(ref _taskEndApps, value ?? "");
        }

        private bool _taskEndAppsBottomRow = true;
        public bool TaskEndAppsBottomRow
        {
            get => _taskEndAppsBottomRow;
            set => Set(ref _taskEndAppsBottomRow, value);
        }

        // UXUI-Extreme: draw the task buttons' fill (colour or texture) behind each quick launch icon.
        private bool _quickLaunchButtonFill = false;
        public bool QuickLaunchButtonFill
        {
            get => _quickLaunchButtonFill;
            set => Set(ref _quickLaunchButtonFill, value);
        }

        // UXUI-Extreme: the Show desktop button's own translucent border, and its margins (left, top,
        // right, bottom; px at 100%). Without custom margins it keeps the Start button's edge gap
        // between itself and the Start button.
        private bool _showDesktopBorder = true;
        public bool ShowDesktopBorder
        {
            get => _showDesktopBorder;
            set => Set(ref _showDesktopBorder, value);
        }

        private bool _showDesktopCustomMargin = false;
        public bool ShowDesktopCustomMargin
        {
            get => _showDesktopCustomMargin;
            set => Set(ref _showDesktopCustomMargin, value);
        }

        private string _showDesktopMargin = "1,0,0,0";
        public string ShowDesktopMargin
        {
            get => _showDesktopMargin;
            set => Set(ref _showDesktopMargin, value ?? "1,0,0,0");
        }

        // UXUI-Extreme: space kept between the Start button and the taskbar's edge it is anchored to
        // (px at 100%), when the button is customised (turned, resized or given a picture). The
        // theme's own button overlaps the taskbar's border by a pixel, which only a custom shape shows.
        private double _startButtonEdgeGap = 1;
        public double StartButtonEdgeGap
        {
            get => _startButtonEdgeGap;
            set => Set(ref _startButtonEdgeGap, value);
        }

        // UXUI-Extreme: swap the quick launch area and the Start button area (the Show desktop button
        // stays with the Start button), and where the icons sit inside the quick launch area.
        private bool _quickLaunchFirst = false;
        public bool QuickLaunchFirst
        {
            get => _quickLaunchFirst;
            set => Set(ref _quickLaunchFirst, value);
        }

        private TaskButtonAlignOption _quickLaunchAlignH = TaskButtonAlignOption.Left;
        public TaskButtonAlignOption QuickLaunchAlignH
        {
            get => _quickLaunchAlignH;
            set => Set(ref _quickLaunchAlignH, value);
        }

        private VerticalPlaceOption _quickLaunchAlignV = VerticalPlaceOption.Top;
        public VerticalPlaceOption QuickLaunchAlignV
        {
            get => _quickLaunchAlignV;
            set => Set(ref _quickLaunchAlignV, value);
        }

        // UXUI-Extreme: where a task button's icon and label sit from top to bottom of the button.
        private VerticalPlaceOption _taskButtonVAlign = VerticalPlaceOption.Centre;
        public VerticalPlaceOption TaskButtonVAlign
        {
            get => _taskButtonVAlign;
            set => Set(ref _taskButtonVAlign, value);
        }

        // UXUI-Extreme: how long the quick launch area is along the taskbar (px at 100%); 0 = as long
        // as its icons. Set by dragging the grip between it and the task buttons (taskbar unlocked).
        private double _quickLaunchLength = 0;
        public double QuickLaunchLength
        {
            get => _quickLaunchLength;
            set => Set(ref _quickLaunchLength, value);
        }

        // UXUI-Extreme: where the expand/collapse toggle sits. Beside = the theme's place, before the
        // icons; First / Last = tucked into the icon area itself, as its first or last cell.
        private TrayTogglePlacementOption _trayTogglePlacement = TrayTogglePlacementOption.Beside;
        public TrayTogglePlacementOption TrayTogglePlacement
        {
            get => _trayTogglePlacement;
            set => SetEnum(ref _trayTogglePlacement, value);
        }

        private bool _trayToggleFlip = true; // mirror the custom image while the area is expanded
        public bool TrayToggleFlip
        {
            get => _trayToggleFlip;
            set => Set(ref _trayToggleFlip, value);
        }

        private bool _trayToggleCustomMargin = false;
        public bool TrayToggleCustomMargin
        {
            get => _trayToggleCustomMargin;
            set => Set(ref _trayToggleCustomMargin, value);
        }

        // px at 100% scale; used when TrayToggleCustomMargin is on
        private string _trayToggleMargin = "2,0,2,0";
        public string TrayToggleMargin
        {
            get => _trayToggleMargin;
            set => Set(ref _trayToggleMargin, value ?? "");
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

        // UXUI-Extreme: where the icon and label sit inside a task button
        private TaskButtonAlignOption _taskButtonAlign = TaskButtonAlignOption.Left;
        public TaskButtonAlignOption TaskButtonAlign
        {
            get => _taskButtonAlign;
            set => Set(ref _taskButtonAlign, value);
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

        // UXUI-Extreme: equal-width clock digits. Proportional digits (a "1" narrower than a "0")
        // make the time look off-centre and shuffle as it changes, whatever the alignment. On: the
        // clock uses tabular numerals, and where the chosen font has none, a monospaced font.
        private bool _clockMonospace = true;
        public bool ClockMonospace
        {
            get => _clockMonospace;
            set => Set(ref _clockMonospace, value);
        }

        private string _clockMonoFamily = "";   // "" = Consolas
        public string ClockMonoFamily
        {
            get => _clockMonoFamily;
            set => Set(ref _clockMonoFamily, value ?? "");
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

    // UXUI-Extreme (UX-015): a quick launch item's custom icon
    public class QuickLaunchIconSetting
    {
        public string Item { get; set; } = "";  // shortcut file name, lower case
        public string Icon { get; set; } = "";  // file name in the Icons folder
    }

    // UXUI-Extreme: apps pinned to a row of the task list.
    public class TaskRowRule
    {
        public int Row { get; set; } = 1;            // 1 = the top row; -1 = the lowest row
        public string Apps { get; set; } = "";        // exe names, comma separated (".exe" optional)
        public bool OnlyListed { get; set; }          // no other app may use this row
        public TaskButtonAlignOption Align { get; set; } // where the row's buttons sit: left, centre or right
        public double HeightAdjust { get; set; }       // px added to (or taken from) this row's button height

        public TaskRowRule Clone() => (TaskRowRule)MemberwiseClone();
    }

    // UXUI-Extreme: a notification area icon's replacement, remembered by the icon's identifier.
    public class TrayIconReplacementSetting
    {
        public string Identifier { get; set; } = ""; // the same identifier the show/hide behaviour uses
        public string Title { get; set; } = "";      // shown in the Icons tab when the icon isn't running
        public string Icon { get; set; } = "";       // file name in the Icons folder
    }

    // UXUI-Extreme: what a custom Start button image holds
    public enum StartImageStatesOption
    {
        One,   // one picture for every state
        Three  // normal, hover and pressed stacked top to bottom (like the XP themes' button)
    }

    // UXUI-Extreme (UX-014): one app's remembered task button settings.
    public class TaskButtonAppRule
    {
        public string App { get; set; } = "";   // exe file name (lower case) or AppUserModelID
        public string Name { get; set; } = "";  // shown in the Layout tab
        public double Width { get; set; }       // 0 = default; -1 = icon only; otherwise px at 100%
        public AppOverrideOption Icon { get; set; }
        public AppOverrideOption Label { get; set; }
        public TaskButtonAppAlignOption Align { get; set; } // Default = the all-apps alignment

        public TaskButtonAppRule Clone() => (TaskButtonAppRule)MemberwiseClone();
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEmpty => Width == 0 && Icon == AppOverrideOption.Default && Label == AppOverrideOption.Default && Align == TaskButtonAppAlignOption.Default;
    }

    // UXUI-Extreme: alignment of a task button's icon and label
    public enum TaskButtonAlignOption
    {
        Left,
        Centre,
        Right
    }

    public enum TaskButtonAppAlignOption
    {
        Default,
        Left,
        Centre,
        Right
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
        UnderStart,
        InQuickLaunch
    }

    // UXUI-Extreme: top / centre / bottom
    public enum VerticalPlaceOption
    {
        Top,
        Centre,
        Bottom
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

    // UXUI-Extreme: where the notification area toggle sits
    public enum TrayTogglePlacementOption
    {
        Beside,
        First,
        Last
    }

    // UXUI-Extreme: how the clock lines' own margins are treated
    public enum ClockMarginOption
    {
        Automatic, // even when the clock is turned, the theme's own when horizontal
        Theme,     // always the theme's margins
        Even,      // left = right and the first line's top = the last line's bottom
        None       // no outer margins at all (gaps between lines stay)
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