<img width="1200" height="149" alt="2026-10-17 18_43_26-RetroBar Taskbar" src="https://github.com/user-attachments/assets/ac052ce9-b849-42bc-b862-b1a66b6650c2" />


<img width="1200" height="141" alt="image" src="https://github.com/user-attachments/assets/18d0bc4b-2583-4509-809b-5f3f32cc339e" />
 
 ![RetroBar screenshot](https://raw.githubusercontent.com/dremin/retrobar/master/retrobar-preview.png)


# RetroBar
[![Current release](https://img.shields.io/github/v/release/dremin/RetroBar)](https://github.com/dremin/RetroBar/releases/latest) ![Build status](https://github.com/dremin/RetroBar/workflows/RetroBar/badge.svg)  [![Discord](https://img.shields.io/badge/discord-white?logo=discord)](https://discord.gg/8bGuPmerxC)

## RetroBar Extreme

**MOD LIST // OCTOBER 2026**

This is the tinkering build of RetroBar. The upstream project already does the important bit — putting a proper 95/98/2000/XP/Vista-style taskbar back on modern Windows. **Extreme** keeps that base and adds the sort of controls that used to turn up three pages into a hardware/modding forum thread after somebody said *"can it do this too?"*

Everything in the list below is added by this fork. Defaults are kept sensible, so you can still run it much like ordinary RetroBar and only mess with the bits you care about.

### LOOKS / COLOURS / TEXTURES

[+] **Live Colours editor** over any theme: solids and multi-stop gradients for the bar, tray, task buttons (normal / hover / active / flashing), clock and 3D edges.

[+] **Saved styles + import/export**, with a pile of ready-made examples. Hand-editing XAML is not required.

[+] **Colour copy/paste, working palette and colourway generator.** Pick base/accent colours, use a preset, or pull a useful palette out of the current Start button and then tweak it by hand.

[+] **Start-button recolouring** for bitmap-based XP/Vista/Longhorn-style themes. Shading, transparency and the flag/artwork are kept instead of just tinting the whole bitmap.

[+] **Procedural + PNG textures.** Ten procedural patterns plus custom greyscale PNGs, palette/LUT colouring, rotation, scale, contrast, tone, generator controls, resampling and sharpening. Texture configs can be copied, pasted, saved, imported and exported; there is also a 20-slot texture palette.

[+] **Border thickness control** from none through heavy, plus complementary-colour filling for quick style building.

### FONTS / SCALE / CLOCK

[+] **Native-size taskbar scaling** as an alternative to the old stretch-everything method. Sizes are rebuilt at the requested scale so text and hard edges can stay crisp; classic stretching is still there when wanted.

[+] **Sharp scaled-text modes** for awkward in-between scales, without requiring font smoothing.

[+] **Taskbar font controls:** family, size, weight, italic and line spacing, with row-height adjustment for tall or unusual fonts.

[+] **24 fonts built into RetroBar Extreme**, including small-size, condensed, mono, display and true pixel-grid faces. They work without installing anything into Windows.

[+] **Clock controls:** separate font/weight/size/spacing, horizontal or vertical reading direction, selectable clock lines, and steady/equal-width digits so the tray does not shuffle around as the time changes.

[+] Menu text and symbol-font handling have been cleaned up so changing taskbar typography does not turn window-menu glyphs into random letters/numbers.

### START / QUICK LAUNCH

[+] **Start button layout controls:** horizontal or vertical (up/down), automatic or exact length/thickness, top/centre/bottom alignment, plus independent icon/text switches.

[+] **Start/Quick layout:** swap the Start and Quick Launch areas, put Show Desktop at the end/beside/under Start or inside Quick Launch, and keep the whole lot usable on very tall multi-row bars.

[+] **Quick Launch controls:** icon alignment, exact strip length, optional task-button-style boxes and a one-icon-cell minimum.

[+] **Custom PNG icons** for Start and Quick Launch. Multi-size icon folders (16px/24px/32px/etc.) are understood so the best source can be picked for the current DPI/scale.

[+] **25 extra Start-button sets, 100 matching Start icons**, plus Show Desktop and tray-image samples.

[+] Drag/drop onto Quick Launch has been fixed up as part of the layout work.

### TASK BUTTONS / ROWS / TRAY

[+] **Global task-button width / icon / label controls**, plus remembered per-application overrides. One program can be icon-only, another wide, another text-only, without changing the rest.

[+] **Per-row layout controls** for multi-row taskbars: app lists, alignment and row height, send selected apps to the end, and adjust icon/label vertical position.

[+] **Notification-area toggle customisation:** replacement PNG, expanded-state mirroring and custom margins.

[+] **Tray-icon inversion now updates live** when the setting, per-icon choice or theme changes; icons no longer get stuck inverted.

[+] Taskbar grips and unlocked-edge spacing have been made stable, so locking/unlocking the bar does not shuffle the layout.

### IN THE ZIP

[+] **26 example styles** available from Saved styles, including Arcade Checker, Blueprint, Copper Patina, Gunmetal Ops, Night Shift, Synthwave, Teletext, Workshop Steel and more.

[+] An **Extras** folder is included with installer, portable and Win7/8 builds: example styles, icon artwork and installable copies of the bundled fonts.

[+] Most of the new controls apply live and are saved, so this works more like an old-school skinning/modding utility than a one-shot theme pack.

---

Pining for simpler times? RetroBar teleports you back in time by replacing your modern Windows taskbar with the classic Windows 95, 98, Me, 2000, XP, or Vista style.

RetroBar is based on the [ManagedShell](https://github.com/cairoshell/ManagedShell) library for great compatibility and performance.

## Requirements
- Windows 7 SP1, Windows 8.1, Windows 10, or Windows 11
  - Fresh installs of Windows 7 may require additional Windows updates. See [#1319](https://github.com/dremin/RetroBar/issues/1319) for details.
- Microsoft .NET Desktop Runtime - When using the RetroBar installer, this is automatically downloaded and installed if necessary. If you're using the portable version, you will be prompted to download on first launch.

## Features
- Replaces default Windows taskbar with classic layout
- Native notification area with balloon notification support
- Native task list with UWP app support and drag reordering
- Quick launch toolbar
- Start button opens modern start menu
- Ability to show or hide the clock
- Ability to auto-hide the taskbar
- Locked and unlocked taskbar appearances
- Display taskbar on any side of the screen (even on Windows 11)
- Resizable taskbar with support for multiple rows
- Option to display the taskbar, notification area, and clock on multiple monitors
- Ability to show Vista-style window thumbnails
- Customizable XP-style collapsible notification area with drag reordering
- Input language and keyboard layout switcher
- Custom theme support

## Included themes
- System (Classic, XP, and Vista)
- Watercolor
- Windows 95-98
- Windows Me
- Windows 2000
- Windows XP:
  - Classic
  - Blue
  - Olive Green
  - Silver
  - Royale
  - Royale Noir
  - Embedded Style
  - Zune Style
- Windows Longhorn Aero
- Windows Vista:
  - Aero
  - Basic
  - Classic

Looking for more themes? [Check out the great community-made RetroBar themes on DeviantArt](https://www.deviantart.com/tag/retrobar), and the theme-repository channel on [our Discord](https://discord.gg/8bGuPmerxC)!

## Supported languages
- Arabic (العربية)
- Basque (euskara)
- Belarusian (беларуская)
- Bulgarian (български)
- Catalan (català)
- Chinese (Simplified) (中文(简体))
- Chinese (Traditional) (中文(繁體))
- Croatian (hrvatski)
- Czech (čeština)
- Danish (dansk)
- Dutch (Nederlands)
- English
- English (United Kingdom)
- Finnish (Suomi)
- French (français)
- German (Deutsch)
- Greek (ελληνικά)
- Hebrew (עברית)
- Hungarian (magyar)
- Icelandic (íslenska)
- Indonesian (Indonesia)
- Italian (italiano)
- Japanese (日本語)
- Korean (한국어)
- Latvian (latviešu)
- Lithuanian (lietuvių)
- Luxembourgish (Lëtzebuergesch)
- Macedonian (Македонски)
- Malay (Melayu)
- Maltese (Malti)
- Norsk (bokmål)
- Persian (فارسی)
- Polish (polski)
- Portuguese (português)
- Romanian (română)
- Russian (русский)
- Serbian (Cyrillic) (српски)
- Serbian (Latin) (srpski)
- Slovak (slovenčina)
- Spanish (español)
- Swedish (svenska)
- Thai (ไทย)
- Turkish (Türkçe)
- Ukrainian (українська)
- Vietnamese (Tiếng Việt)
- Welsh (Cymraeg)

## Custom languages and themes
RetroBar supports custom languages and themes. You may install community-made theme files that you have downloaded in RetroBar Properties > Advanced.

You may manually install custom languages or themes by creating a `Languages` or a `Themes` directory in `%localappdata%\RetroBar`, and placing valid `.xaml` language or theme files there.

Themes use the XAML `ResourceDictionary` format. When creating a new theme, [view the included example themes](https://github.com/dremin/RetroBar/tree/master/RetroBar/Themes) to get started.

## Open-Shell Menu users

You may need to adjust some Open-Shell Menu settings for the best compatibility with RetroBar. We recommend the following settings:

- Controls > Windows Key opens > Open-Shell Menu
- Menu Look > Align start menu to working area
