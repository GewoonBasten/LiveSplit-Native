# LiveSplit for Linux (test build)

A Linux build of the LiveSplit port. It is an early test: it opens and times on Linux, but some
parts that need Linux's own services are not done yet (see below). Feedback is very welcome.

**Needs:** a 64-bit PC (x64; `linux-arm64` for ARM) with a desktop (X11, or Wayland with
XWayland, which most desktops have). Nothing else to install on common distributions (Ubuntu,
Fedora, Arch, Mint...). If it says it cannot find ICU, install your distribution's `libicu`.

## Start it

1. Unpack `LiveSplit-Native-linux-x64.tar.gz` (double-click it, or `tar -xzf` it in a terminal).
2. In a terminal, in the unpacked `linux-x64` folder: `./LiveSplitNative.App`

Right-click the timer for the menu: open splits (`.lss`) and layouts (`.lsl`) from Windows
LiveSplit as they are.

## What works

The timer, splits, layouts and every built-in component, the Splits and Layout editors, settings,
speedrun.com, therun.gg, the LiveSplit Server, downloaded components (`.dll` files from
livesplit.org, in the **Components** folder next to the settings), and:

- **Global hotkeys** (Settings, Global Hotkeys): keys are seen whichever window has the keyboard.
  On an X11 desktop that is every app. On a Wayland desktop it is every X11 app, which includes
  most games (and everything through Proton or Wine), but not native Wayland apps.
- **Controllers as hotkeys** (Allow Gamepads as Hotkeys): joysticks and gamepads.
- **Sound Effects**, through the desktop's player (PipeWire's `pw-play` or PulseAudio's
  `paplay`; for MP3 files `ffplay` or `mpv` if installed). Always on the default output.
- **racetime.gg races**: the timer follows the race; the race's page with its chat opens in your
  browser (keep LiveSplit's race window open). Signing in once is enough: logins are kept in
  your desktop's keyring (GNOME Keyring, KDE Wallet).
- **Updates:** LiveSplit looks for a newer version when it starts and can put it in place.

## Not yet on Linux

- **Deactivate Hotkeys For Other Programs:** the keys also reach the other program.
- **Auto splitters** of the Auto Splitting Runtime.

## Your files

Settings: `~/.config/livesplit-native/settings.cfg` (or under `$XDG_CONFIG_HOME`).

## Reporting a problem

Say what you did, what you expected and what happened; attach the splits and layout if it's about
them, and what the terminal printed if LiveSplit closed by itself.
