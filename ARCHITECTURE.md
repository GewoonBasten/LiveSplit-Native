# Architecture — LiveSplit Native (macOS)

**Decision: Approach B — Hybrid (.NET managed core + native macOS UI + component adapter host)**

Milestone 1 date: 2026-10-08  
Decision criterion: highest achievable Windows LiveSplit fidelity (not easiest stack), under hard constraints (no Wine/CrossOver/VM; DLL reuse priority ladder; upstream sync).

## Decision summary

| Approach | Verdict |
|---|---|
| **A — Direct .NET / WinForms port** | **Rejected** for the macOS product host |
| **B — Hybrid** | **Selected** |
| **C — livesplit-core foundation** | **Rejected as primary**; used as a supporting native library |

**Why B wins:** Empirical Apple Silicon tests prove Windows LiveSplit component DLLs **load as managed PE IL** into .NET 10 CoreCLR, but **cannot fully type-inspect or run** without WinForms / System.Drawing (`Microsoft.WindowsDesktop.App` is absent on `osx-arm64`). Approach A therefore cannot host the real UI/component surface. Approach C maximizes cross-platform timing/files but **abandons the Windows component DLL ecosystem**, violating the project’s highest compatibility priority. Approach B keeps the LiveSplit managed model and a ComponentManager-compatible loader, replaces the WinForms shell with a native macOS UI, and climbs the DLL ladder (unchanged → adapter/shim → minimal recompile → port → replacement) with verified evidence at each step.

## Upstream architecture (verified from source)

Cloned: [LiveSplit/LiveSplit](https://github.com/LiveSplit/LiveSplit) @ `93a11b85b0593650b8c398615d61c9d57cecb84e` (`1.8.37-60-g93a11b85`), recursive submodules.

| Layer | Location | Role |
|---|---|---|
| Host exe | `src/LiveSplit` | WinExe, WinForms entry |
| UI shell | `src/LiveSplit.View` | `TimerForm`, menus, editors, layout chrome |
| Core | `src/LiveSplit.Core` | Timer model, state, components API, settings, server, autosplit helpers |
| Components | `components/LiveSplit.*` (submodules) | Shipped as `Components\*.dll` |
| Native timing/parse | `livesplit_core.dll` (x86/x64 PE) via `LiveSplitCore*.cs` | Run parse + some core helpers |
| ASR | `asr_capi.dll` (PE) | Auto Splitting Runtime |

**Timing:** `TimeStamp` uses `System.Diagnostics.Stopwatch` (QPC-backed on Windows) with optional NTP drift correction (`AtomicDateTime`). `TimerModel` / `LiveSplitState.CurrentTime` derive elapsed time from `TimeStamp.Now - AdjustedStartTime` — not from frame presentation. This is portable managed code and was exercised on Linux .NET 8.

**UI:** Entire interactive surface is WinForms (`UseWindowsForms=true`, TFM `net4.8.1`). Layout paint uses `System.Drawing.Graphics` through `IComponent.DrawHorizontal/DrawVertical` and `ComponentRenderer`.

**Components:** `ComponentManager` enumerates `Components\*.dll`, `Assembly.UnsafeLoadFrom`, reads `[assembly: ComponentFactory(typeof(...))]`, constructs `IComponentFactory`, then `Create(state)`. Failures are swallowed. Third-party autosplitter DLLs use the same path.

**Serialization:** `.lss` runs via `StandardFormatsRunFactory` → native `livesplit-core` parse, saved with `XMLRunSaver`. Layouts `.lsl` via `XMLLayoutFactory` / `XMLLayoutSaver`. App settings `settings.cfg` via XML settings factories/savers. File-type registration is Windows Registry–specific.

**Settings / hotkeys / server:** Hotkeys via `user32` hooks + SharpDX DirectInput; credentials via Advapi32; named pipe `\\.\pipe\LiveSplit` plus optional TCP/WebSocket command server.

## Windows-specific dependencies (inventory)

| Category | Examples | macOS impact |
|---|---|---|
| WinForms UI | `TimerForm`, settings `Control`s, menus | No `Microsoft.WindowsDesktop.App` on osx-arm64 |
| System.Drawing GDI+ | `IComponent` draw path, icons/images | Blocks full type load of components |
| Win32 P/Invoke | `user32`, `kernel32`, `gdi32`, `psapi`, `ntdll`, `shell32`, `Advapi32` | Hotkeys, process memory, registry, credentials |
| Native PE | `livesplit_core.dll`, `asr_capi.dll`, WebView2Loader, VLC ActiveX | Not loadable as macOS dylibs |
| Desktop frameworks | WPF refs (`PresentationCore`, `WindowsBase`), WindowsAPICodePack | Unavailable on macOS |
| Media | NAudio, AXVLC, WebView2 | Windows-oriented |

## Empirical load evidence (decisive)

### Apple Silicon (authoritative for DLL-on-macOS)

Host: Mac mini, macOS 26.5 arm64, .NET 10.0.3, RID `osx-arm64`.  
Corpus: LiveSplit **1.8.37** release zip + upstream commit `93a11b85`.  

| Result | Count / note |
|---|---|
| DLLs scanned | 68 (61 managed + 7 native) |
| Full pass (load + types inspectable) | **21** — utilities/deps only; **zero** LiveSplit UI/components |
| Built-in `Components/LiveSplit.*.dll` | **26/26** ALC load OK; **0/26** full type inspection |
| Dominant blockers | `System.Windows.Forms` 4.0 / `System.Drawing.Common` |
| `LiveSplit.Core` / `View` | Fail type inspection; Core also misses Framework `System.Web.Extensions` |
| `net*-windows` + WinForms host | Builds with targeting pack; **cannot run** (no WindowsDesktop runtime) |
| Native `livesplit_core` / `asr_capi` | Windows PE only |
| Partial success | `TimerFactory` / `BlankSpaceFactory` constructible after partial type load |

### Linux x64 (supporting)

Ubuntu 24.04 x86_64, .NET 8/10. Confirms: release PE native DLL fails (`invalid ELF header`); managed `LiveSplit.Core` can load and `TimeStamp.Now` runs; with dependency resolution, all 26 component DLLs still fail without WinForms; `livesplit-core` C API builds as `liblivesplit_core.so`.

## Approach comparison

| Criterion | A Direct .NET/WinForms | **B Hybrid (selected)** | C livesplit-core foundation |
|---|---|---|---|
| Code reuse | Highest source overlap, but host cannot run WinForms on macOS | High reuse of model/timer/serialization ideas + ComponentManager pattern; UI replaced | Low reuse of Windows LiveSplit C#; high reuse of Rust core |
| Windows DLL compatibility | Requires WindowsDesktop — **unavailable** on osx-arm64 | **Best achievable:** ALC load proven; adapters/shims + ladder; factories partially constructible | Effectively none for `IComponent` DLLs |
| UI fidelity to Windows LiveSplit | Theoretically pixel-path via GDI+, practically **blocked** | Port WinForms layout/behavior to native macOS chrome; fidelity is engineering work, not a runtime impossibility | LiveSplit One–style layouts; different component model |
| Timing accuracy | Portable `TimeStamp`/Stopwatch OK if host ran | Same managed timing + macOS `livesplit_core` dylib | Excellent (designed cross-platform) |
| File compatibility (.lss/.lsl/settings) | Same code paths if runnable | Keep XML savers/factories / livesplit-core parse with Mac dylib | Native parse/write in Rust; must carefully match Windows LiveSplit behavior |
| Performance | N/A on macOS host | Native UI + managed core should be fine for timer UI | Excellent |
| Upstream update compatibility | Hard (net481 WinForms tree) | Track LiveSplit submodules + cherry-pick model/component changes; dual-track livesplit-core | Track livesplit-core well; diverge from LiveSplit.exe component world |
| Development complexity | False simplicity — blocked by runtime | Highest complexity, **only path** to DLL-ladder goals | Lower for a timer app; fails DLL priority |

## Selected architecture (B) — shape

```
┌─────────────────────────────────────────────┐
│  LiveSplit.app (macOS arm64)                │
│  Native UI shell (AppKit/SwiftUI or .NET    │
│  macOS UI) — menus, timer window, editors   │
├─────────────────────────────────────────────┤
│  Managed host (.NET 8/10, osx-arm64)        │
│  - TimerModel / LiveSplitState (ported)     │
│  - XML .lss/.lsl/settings                   │
│  - Command server (TCP/WS; pipe TBD)        │
│  - ComponentHost (ALC + Factory attribute)  │
├───────────────┬─────────────────────────────┤
│ Adapter/shim  │  liblivesplit_core.dylib    │
│ ladder for    │  (build from livesplit-core │
│ Components\   │   for aarch64-apple-darwin) │
└───────────────┴─────────────────────────────┘
```

### Component compatibility ladder (operational)

1. **Unchanged DLL** — ALC load works; full `IComponent` surface **does not** (Mac evidence). Not a viable end-to-end path today without a WindowsDesktop-equivalent shim (does not exist on macOS).
2. **Adapter** — Primary near-term strategy: load factory where possible; bridge draw/settings through a compatibility façade or dual interface; prefer this before forking component logic.
3. **Minimal recompile** — Retarget component sources against a portable draw/settings abstraction while keeping behavior.
4. **Port** — C# logic moved into the native UI toolkit.
5. **Replacement** — Last resort (especially process-memory / ASR / Video).

### Explicit non-goals for the chosen path

- Running the stock WinForms `TimerForm` on macOS.
- Loading Windows PE `livesplit_core.dll` / `asr_capi.dll` in-process.
- Treating Approach C / LiveSplit One as the product (unless a future milestone revisits after DLL goals are abandoned).

## Top risks

1. **DLL-on-macOS ceiling** — Unchanged UI component DLLs will not fully activate without WinForms/Drawing; adapter complexity dominates Milestone 4.
2. **IComponent API coupling** — Public component contract *is* WinForms/Drawing; any façade must preserve layout/settings XML compatibility.
3. **Process memory / ASL / ASR** — Win32 `ReadProcessMemory` and Windows PE `asr_capi` need Mac-specific strategies; many autosplitters are Windows-game oriented.
4. **Upstream sync** — Hybrid means tracking both LiveSplit (C#) and livesplit-core (Rust) without auto-merging unverified changes.
5. **UI fidelity labor** — Native shell must reimplement editors/menus/layout editing without redesigning UX.

## Milestone 2 implication

Scaffold a compilable managed solution: portable core (timing/state), component-host stub (ALC loader), and an Apple Silicon–oriented app project with documented Mac packaging requirements. Full `.app` linking requires a Mac (`net*-macos` / Xcode).
