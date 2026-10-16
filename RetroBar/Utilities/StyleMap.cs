using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RetroBar.Utilities
{
    // UXUI-Extreme (UX-003): which theme resource keys draw each visible part of the taskbar.
    //
    // Themes don't share names for the same part, so the style layer works in parts
    // ("button.background") and this map turns a part into the keys of the theme in use.
    // Built-in themes fall into three families (detected from the keys the theme defines, so
    // custom themes built the same way work too):
    //
    //   Classic    System, Windows 95-98, Me, 2000, XP Classic, Vista Classic. One set of
    //              bevel brushes (ButtonFace, ButtonHighlight, ...) draws everything, so the bar,
    //              tray and Start button share one background.
    //   Watercolor Classic-style names plus its own per-state button and tray brushes.
    //   Modern     XP (Blue, Olive Green, Silver, Royale, Royale Noir, Embedded, Zune), Vista
    //              Aero/Basic and Longhorn Aero. Per-part Taskbar*/Tray*/TaskButton* brushes;
    //              not every theme has every key, so only keys the theme defines are set.
    //
    // A key is filled with the part's brush, filled with it turned 90° (backgrounds drawn on
    // a vertical taskbar), or cleared to transparent (theme overlays such as XP's button
    // highlight and Longhorn's glow, which would otherwise tint the custom background).
    // The same map is written up for people in UXUI-Extreme docs/STYLE-MAP.md.
    public static class StyleMap
    {
        public enum Family { Classic, Watercolor, Modern }

        public enum Role { Fill, FillVertical, Clear }

        public readonly struct Target
        {
            public readonly string Key;
            public readonly Role Role;

            public Target(string key, Role role = Role.Fill)
            {
                Key = key;
                Role = role;
            }
        }

        // Parts and their properties, as written in a style file.
        public static readonly string[] Parts = { "bar", "tray", "button", "buttonHover", "buttonActive", "buttonFlashing", "clock", "bevel" };

        public static readonly Dictionary<string, string[]> PartProperties = new Dictionary<string, string[]>
        {
            ["bar"] = new[] { "background", "border" },
            ["tray"] = new[] { "background", "border" },
            ["button"] = new[] { "background", "border", "foreground" },
            ["buttonHover"] = new[] { "background", "border" },
            ["buttonActive"] = new[] { "background", "border", "foreground" },
            ["buttonFlashing"] = new[] { "background", "border", "foreground" },
            ["clock"] = new[] { "foreground" },
            ["bevel"] = new[] { "highlight", "light", "shadow", "darkShadow" },
        };

        // Classic and Watercolor draw the inactive task button background with the button
        // style's Background (ButtonFace, shared with the bar), so the layer sets it on a copy
        // of the theme's TaskButton style instead of through a key.
        public const string TaskButtonStyleBackground = "button.background";

        public static Family Detect()
        {
            if (Application.Current.TryFindResource("TaskbarBackground") != null)
            {
                return Family.Modern;
            }

            if (Application.Current.TryFindResource("ButtonActiveBackground") != null)
            {
                return Family.Watercolor;
            }

            return Family.Classic;
        }

        public static IEnumerable<Target> Targets(Family family, string partProperty)
        {
            Dictionary<string, Target[]> map = family switch
            {
                Family.Modern => Modern,
                Family.Watercolor => Watercolor,
                _ => Classic,
            };

            return map.TryGetValue(partProperty, out Target[] targets) ? targets : Enumerable.Empty<Target>();
        }

        private static Target F(string key) => new Target(key);
        private static Target V(string key) => new Target(key, Role.FillVertical);
        private static Target C(string key) => new Target(key, Role.Clear);

        private static readonly Dictionary<string, Target[]> Classic = new Dictionary<string, Target[]>
        {
            ["bar.background"] = new[] { F("ButtonFace") },
            ["buttonActive.background"] = new[] { F("CheckeredBackground") },
            ["buttonFlashing.background"] = new[] { F("ButtonFlashingFace") },
            ["button.foreground"] = new[] { F("ButtonForeground") },
            ["buttonFlashing.foreground"] = new[] { F("ButtonFlashingForeground") },
            ["clock.foreground"] = new[] { F("ClockForeground") },
            ["bevel.highlight"] = new[] { F("ButtonHighlight"), F("ButtonInnerHighlight") },
            ["bevel.light"] = new[] { F("ButtonLight") },
            ["bevel.shadow"] = new[] { F("ButtonShadow") },
            ["bevel.darkShadow"] = new[] { F("ButtonDarkShadow") },
        };

        private static readonly Dictionary<string, Target[]> Watercolor = new Dictionary<string, Target[]>
        {
            ["bar.background"] = new[] { F("ButtonFace") },
            ["bar.border"] = new[] { F("ButtonBorder") },
            ["tray.background"] = new[] { F("TrayBackground") },
            ["tray.border"] = new[] { F("TrayBorder"), F("TrayInnerBorder") },
            ["button.border"] = new[] { F("ButtonInactiveBorder") },
            ["buttonHover.background"] = new[] { F("ButtonHoverBackground") },
            ["buttonHover.border"] = new[] { F("ButtonHoverBorder") },
            ["buttonActive.background"] = new[] { F("ButtonActiveBackground"), F("ButtonPressedBackground") },
            ["buttonActive.border"] = new[] { F("ButtonActiveBorder"), F("ButtonPressedBorder") },
            ["buttonFlashing.background"] = new[] { F("ButtonFlashingBackground") },
            ["buttonFlashing.border"] = new[] { F("ButtonFlashingBorder") },
            ["button.foreground"] = new[] { F("ButtonForeground") },
            ["buttonActive.foreground"] = new[] { F("ButtonPressedForeground") },
            ["clock.foreground"] = new[] { F("ClockForeground") },
            ["bevel.highlight"] = new[] { F("ButtonLight") },
            ["bevel.shadow"] = new[] { F("ButtonShadow") },
        };

        private static readonly Dictionary<string, Target[]> Modern = BuildModern();

        private static Dictionary<string, Target[]> BuildModern()
        {
            var map = new Dictionary<string, Target[]>
            {
                ["bar.background"] = new[]
                {
                    F("TaskbarBackground"), F("TaskbarBackgroundTall"), F("TaskbarWindowBackground"), V("TaskbarVerticalBackground"),
                    C("TaskbarBackgroundGlow"), C("TaskbarVerticalBackgroundGlow"),
                },
                ["bar.border"] = new[]
                {
                    F("TaskbarBorder"), F("TaskbarTopBorder"), F("TaskbarBottomBorder"), F("TaskbarVerticalTopBorder"),
                    F("TaskbarVerticalBottomBorder"), F("TaskbarUnlockedBorderTop"),
                },
                ["tray.background"] = new[] { F("TrayBackground"), V("TrayVerticalBackground") },
                ["tray.border"] = new[]
                {
                    F("TrayTopBottomBorder"), F("TrayTopBorder"), F("TrayBottomBorder"), F("TrayOuterBorderVertical"), F("TrayLeftBorder1"),
                    C("TrayLeftBorder4"),
                },
                ["button.foreground"] = new[] { F("ButtonForeground") },
                ["buttonActive.foreground"] = new[] { F("ButtonActiveForeground"), F("ButtonPressedForeground") },
                ["buttonFlashing.foreground"] = new[] { F("ButtonFlashingForeground") },
                ["clock.foreground"] = new[] { F("ClockForeground") },
            };

            var highlight = new List<Target>
            {
                F("TaskbarTopInnerBorder"), F("TaskbarTopInnerBorder1"), F("TaskbarTopInnerBorder2"), F("TaskbarBottomInnerBorder"),
                F("TaskbarVerticalTopInnerBorder"), F("TaskbarVerticalBottomInnerBorder"), F("TrayLeftBorder2"), F("TrayLeftBorder3"),
            };
            var shadow = new List<Target>();

            // Button states. XP calls the active (pushed-in) button "Pressed", Vista and Longhorn
            // call it "Active"; both have hover variants of it.
            var states = new (string part, string[] suffixes, bool pushedIn)[]
            {
                ("button", new[] { "" }, false),
                ("buttonHover", new[] { "Hover" }, false),
                ("buttonActive", new[] { "Pressed", "PressedHover", "Active", "ActiveHover" }, true),
                ("buttonFlashing", new[] { "Flashing" }, false),
            };

            foreach (var (part, suffixes, pushedIn) in states)
            {
                var background = new List<Target>();
                var border = new List<Target>();

                foreach (string s in suffixes)
                {
                    background.Add(F($"TaskButtonBackground{s}"));
                    background.Add(C($"TaskButtonBackgroundHighlight{s}"));
                    background.Add(C($"TaskButtonBottomBackground{s}"));

                    foreach (string edge in new[] { "Top", "Left", "Right", "Bottom", "Outer", "BottomRight" })
                    {
                        border.Add(F($"TaskButton{edge}Border{s}"));
                    }

                    // Inner bevel lines: light on raised buttons, swapped on pushed-in ones.
                    var light = new[] { "InnerLeft", "Inner", "InnerTopRight", "InnerBottomLeft", "InnerVertical", "InnerHorizontal", "InnerTop" };
                    var dark = new[] { "InnerRight", "InnerBottom" };
                    (pushedIn ? shadow : highlight).AddRange(light.Select(edge => F($"TaskButton{edge}Border{s}")));
                    (pushedIn ? highlight : shadow).AddRange(dark.Select(edge => F($"TaskButton{edge}Border{s}")));
                }

                map[$"{part}.background"] = background.ToArray();
                map[$"{part}.border"] = border.ToArray();
            }

            map["bevel.highlight"] = highlight.ToArray();
            map["bevel.shadow"] = shadow.ToArray();
            return map;
        }
    }
}
