# macOS / Apple Silicon build notes

Linux/dev hosts build portable libraries + Avalonia desktop + headless smoke.  
**A real LiveSplit Native `.app` must be produced on Apple Silicon macOS.**

## Requirements (Mac)

- macOS on Apple Silicon (arm64)
- Xcode + CLT
- **.NET 10 SDK/runtime** (solution TFM is `net10.0` — Mac M2 host had no .NET 8 runtime)
- Rust (for `livesplit-core` dylib)

## Avalonia app → `.app`

`tools/mac/make-app.command` (double-click, or run from a shell) publishes the app
self-contained for this Mac's chip and assembles `dist/LiveSplit.app` (signed with the "LiveSplit Native Signing" certificate when `tools/mac/make-signing-certificate.command` was run once, ad hoc otherwise)
plus `dist/LiveSplit-Native-<arch>.zip` to send to others. Nobody needs .NET installed to run it.

The bundle's `Contents/MacOS/compat/` holds the look-alike assemblies downloaded LiveSplit
components load against (`src/Compat/CopyCompat.targets` copies them on publish too); the Auto
Splitting Runtime's `libasr_capi.dylib` goes along when it was built (native/README.md).
Downloaded components go in the user's Components folder next to settings.cfg, not in the app.

The app isn't notarized (free route, on purpose): on another Mac the first open needs System
Settings → Privacy & Security → Open Anyway. INSTALL.md is the guide for testers.

Rendering: `Program.cs` prefers **Software then Metal** (`AvaloniaNativePlatformOptions`).

## Native livesplit-core (Mac)

```bash
git clone --recursive https://github.com/LiveSplit/livesplit-core.git
cd livesplit-core
# Prefer LiveSplit submodule pin when matching Windows parse — see UPSTREAM.md
cargo rustc --release -p livesplit-core-capi --crate-type cdylib
# Expect: target/release/liblivesplit_core.dylib
```

Do **not** ship Windows `livesplit_core.dll` from the LiveSplit release zip.

## Component probing on Mac

Stock .NET ALC-loads `Components/LiveSplit.*.dll` but fails full type inspection without WinForms/Drawing — see COMPONENT_COMPATIBILITY.md. The in-app probe must never crash the timer (covered by unit tests).
