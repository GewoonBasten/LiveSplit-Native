# LiveSplit Native

A port of [LiveSplit](https://github.com/LiveSplit/LiveSplit) (Windows) to **macOS** (Apple Silicon)
and **Linux**, in .NET 10 with [Avalonia](https://avaloniaui.net/). It keeps LiveSplit's own
behavior: the timing code, `.lss` / `.lsl` / `settings.cfg` files, menus, dialogs and components
work as on Windows, and many Windows component DLLs load unchanged.

What works and what is left: [ROADMAP.md](ROADMAP.md).

## How it is put together

| Folder | What it is |
|---|---|
| `src/LiveSplitNative.Core` | No UI. Timing (`Timing/`, `Session/`), the run model and comparisons (`Model/`), reading and writing LiveSplit's files (`Serialization/`, `Options/`), component logic and settings (`UI/`), racetime.gg (`Racing/`), the TCP/WebSocket server (`Server/`), speedrun.com and updates (`Web/`). Most of it is ported from LiveSplit.Core. |
| `src/LiveSplitNative.App` | The Avalonia app: the timer window (`MainWindow*`, `TimerSurfaceControl` draws every built-in component), the dialogs (`*Window.axaml.cs`), and the OS-specific parts behind interfaces in `Platform/` (macOS: AppKit menus, event-tap hotkeys, IOKit gamepads, Keychain; Linux: X11 hotkeys, evdev gamepads, libsecret). |
| `src/Compat` | Look-alike `LiveSplit.Core`, `System.Windows.Forms`, `System.Drawing` and friends, so Windows component DLLs load **unchanged** and draw through the app. Names and signatures must match Windows exactly. |
| `src/LiveSplitNative.ComponentHost` | Loads those component DLLs. |
| `tests/LiveSplitNative.Core.Tests` | Core tests. |
| `tests/LiveSplitNative.App.Tests` | Headless Avalonia tests of the windows and dialogs. |
| `tests/LiveSplitNative.Upstream.Tests` | Runs the real upstream LiveSplit.Core side by side with ours and compares (timing, formatting, comparisons, every `.lss` in `tests/fixtures`). |
| `native/` | Rust: the Auto Splitting Runtime's C library, and livesplit-core for the corpus tests. Optional. |
| `tools/` | Packaging: `mac/make-app.command` (the `.app` and zip), `linux/publish.sh` (tarballs). |

More: [ARCHITECTURE.md](ARCHITECTURE.md) (why this approach), [COMPONENT_COMPATIBILITY.md](COMPONENT_COMPATIBILITY.md)
(which component DLLs work and how), [docs/linux-readiness.md](docs/linux-readiness.md),
[docs/native-menus.md](docs/native-menus.md), [UPSTREAM.md](UPSTREAM.md) (the upstream commit it follows).

## Build, test, run

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build LiveSplitNative.slnx
dotnet test LiveSplitNative.slnx
dotnet run --project src/LiveSplitNative.App
```

`dotnet run --project src/LiveSplitNative.App -- --headless` runs a timer smoke test without a window.

## Packaging

- **macOS:** `tools/mac/make-app.command` builds `dist/LiveSplit.app` and a zip; nobody needs .NET
  to run it. See [MACOS_BUILD.md](MACOS_BUILD.md); testers: [INSTALL.md](INSTALL.md).
- **Linux:** `tools/linux/publish.sh x64` (or `arm64`) builds `dist/LiveSplit-Native-linux-<arch>.tar.gz`.
  Testers: [INSTALL-LINUX.md](INSTALL-LINUX.md).

Settings live in `~/Library/Application Support/LiveSplit Native/settings.cfg` (macOS) or
`~/.config/livesplit-native/settings.cfg` (Linux), in Windows LiveSplit's format.

## License

MIT, like LiveSplit. See [LICENSE](LICENSE), which also carries LiveSplit's notice.
