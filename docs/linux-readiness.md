# Linux readiness

Where LiveSplit Native stands on Linux, and what the port still needs. Linux is not released yet;
this is the plan for when it starts.

## Building and trying it

- `tools/linux/publish.sh [x64|arm64]` builds a self-contained Linux app (no .NET install needed)
  into `dist/linux-<arch>/` and `dist/LiveSplit-Native-linux-<arch>.tar.gz`. It runs on a Mac or
  on Linux with the .NET 10 SDK.
- Run `./LiveSplitNative.App`. `--headless` runs the timer smoke test without a window.

### Verified (2 Nov 2026, Ubuntu 22.04 arm64, in the Mac's Linux VM)

| Check | Result |
|---|---|
| `--headless` smoke (timer start, split, pause, undo, skip, reset) | OK |
| A real layout and splits, the official component DLLs and their settings pages (rendered headless) | Renders; the component DLLs load through the look-alikes |
| Dialogs, Light and Dark (rendered headless) | Render |
| Timer clock | CLOCK_BOOTTIME (counts through sleep), as livesplit-core |
| Settings folder | `$XDG_CONFIG_HOME/livesplit-native/` (else `~/.config/livesplit-native/`) |

Not yet tried: a real window on X11 or Wayland (the VM has no display), and x64.

## What works on Linux already

Everything that is not a macOS service: the timer and its atomic clock sync, splits, layouts,
every built-in component, the Splits and Layout editors, settings, Speedrun.com and therun.gg,
racetime.gg (without its chat panel), the server, downloaded components through the look-alikes,
the crash guard, and the Auto Splitting Runtime once `libasr_capi.so` is built for Linux.

macOS-only code is behind checks (`OperatingSystem.IsMacOS()`, the factories below), so on Linux
it is left out rather than failing.

## What the port needs

| Area | macOS today | Linux plan | Size |
|---|---|---|---|
| Fonts | The layout's Windows font names map to macOS faces | Done: Segoe UI's stand-ins go on to Liberation Sans (Arimo) and the bundled Inter, never a wide face like DejaVu Sans; a missing narrow face (Roboto Condensed) is drawn with an installed narrow one (Liberation Sans Narrow), so split names fit as on the Mac | Done |
| Global hotkeys | `MacGlobalKeyboardHook` (event tap) | Done (3 Nov): `X11GlobalKeyboardHook`, X11 RECORD (every key, nothing taken from the app); on Wayland through XWayland (X11 apps and games). Checked in the VM: F1 pressed outside the window starts the timer with Global Hotkeys on, nothing with it off. Not done: Deactivate For Other Programs (XGrabKey), native Wayland apps (portal / evdev) | Done (X11) |
| Gamepads | `MacGamepadHook` (IOKit HID) behind `IGamepadSource` | Done: `LinuxGamepadHook`, evdev joysticks and gamepads, same `GamepadInput` names (not checked with a real controller) | Done |
| Sound Effects | `MacOsSoundPlayer` (AVFoundation) behind `ISoundPlayer` | Done: `LinuxSoundPlayer`, pw-play / paplay (ffplay / mpv for MP3), default output | Done |
| Saved logins | Keychain (`MacKeychainCredentialStore`) | Done: `LinuxSecretCredentialStore`, libsecret; the session's store where there is none | Done |
| racetime.gg chat | `MacWebView` (WKWebView) | Done: the race's page opens in the browser; the race window keeps the channel (the timer follows the race). WebKitGTK later | Done (browser) |
| Context menu | NSMenu | `AvaloniaFallbackContextMenu` (works); GTK menu later | Done / optional |
| File dialogs | Avalonia StorageProvider | Same (uses the XDG portal or GTK) | Done |
| Window chrome, always on top | `MacOsNativeWindowFrame` | `AvaloniaNativeWindowFrame`; always on top depends on the Wayland compositor | Check |
| Appearance (Light/Dark) | NSAppearance | Avalonia theme; follow the portal's color-scheme setting | Small |
| Updates | `AppUpdater` replaces the .app | Done: the folder is replaced from the release's tar.gz (`version.txt` from publish.sh) | Done |
| Packaging | `.app` + zip (make-app) | tarball now; AppImage or Flatpak, `.desktop` file, icon | Medium |
| OBS capture | Window capture | X11 window capture works; Wayland via PipeWire screen cast | Check |

## Suggested order

1. A real window on X11 and on Wayland (fonts are done).
2. Global hotkeys on X11, and the Wayland answer (portal) — say in the docs what Wayland users get.
3. Sound Effects, saved logins, gamepads.
4. Packaging (AppImage or Flatpak) and the update check for it.
