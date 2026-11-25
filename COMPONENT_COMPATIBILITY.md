# Component compatibility

Living matrix for LiveSplit Native. Distinguishes **portable managed**, **WinForms/Drawing**, **Win32**, and **process memory / native** surfaces.  
DLL-on-macOS rows use **real Apple Silicon evidence** (2026-10-08), not speculation.

## How Windows LiveSplit loads components

Source: `LiveSplit/src/LiveSplit.Core/UI/Components/ComponentManager.cs` @ `93a11b85`.

1. `PATH_COMPONENTS = "Components\\"`
2. Enumerate `*.dll`
3. `Assembly.UnsafeLoadFrom(path)`
4. Read `ComponentFactoryAttribute` → construct `IComponentFactory` → `Create(LiveSplitState)`
5. Exceptions swallowed; bad DLLs skipped
6. Autosplitter downloads land in the same folder and use `LoadFactory`

### `IComponent` contract (source)

Requires `System.Drawing` (`Graphics`, `Region`) and `System.Windows.Forms` (`Control` for settings).  
Draw path: `DrawHorizontal` / `DrawVertical` via `ComponentRenderer`.  
Settings: `GetSettingsControl`, `GetSettings` / `SetSettings` (XML).

**Implication:** Full end-to-end unchanged DLL reuse needs a WinForms/Drawing-compatible host. Stock .NET on macOS does not provide that.

## macOS evidence (Apple Silicon) — authoritative

| Field | Value |
|---|---|
| Host | Mac mini, macOS 26.5, Darwin 25.5.0, **arm64** |
| Runtime | .NET 10.0.3, RID `osx-arm64` (no `Microsoft.WindowsDesktop.App`) |
| Corpus | LiveSplit **1.8.37** release zip + upstream `93a11b85` |

### Headline counts (release zip)

| Bucket | Count |
|---|---:|
| DLLs scanned | 68 |
| Managed tested | 61 |
| Native skipped | 7 |
| Full pass (load **and** types inspectable) | **21** (utilities/deps only) |
| Built-in `Components/LiveSplit.*.dll` | **26** |
| Those with ALC `LoadFromAssemblyPath` OK | **26/26** |
| Those with full type inspection OK | **0/26** |
| LiveSplit UI/component full passes | **0** |

### Secondary probes (Mac)

| Probe | Result |
|---|---|
| Host + `System.Drawing.Common` | Drawing failures shift to WinForms; still 0 component full passes |
| `net10.0-windows` + WinForms | Builds; **cannot run** on osx-arm64 (no WindowsDesktop) |
| Partial `ReflectionTypeLoadException` types | `TimerFactory` / `BlankSpaceFactory` **construct OK** in isolation |
| Native PE `livesplit_core` / `asr_capi` / WebView2Loader | Not macOS dylibs; `dlopen` looks for `.dylib` and fails |

### Pass definition

**Full pass** = managed load succeeded **and** `GetTypes()` completed without `ReflectionTypeLoadException`.  
ALC load alone is **not** a full pass (documented as `ALC-load-OK / types-FAIL`).

## Linux evidence (supporting only)

Ubuntu 24.04 x86_64, .NET 8/10.

- Windows PE `livesplit_core.dll`: load fail (`invalid ELF header`)
- Release `LiveSplit.Core.dll`: loads; `TimeStamp.Now` invokes
- All 26 `LiveSplit.*.dll` components: fail without WinForms (even with `System.Drawing.Common` package)
- `livesplit-core` C API: **built** `liblivesplit_core.so` on this host

## Category legend

| Tag | Meaning |
|---|---|
| PortableManaged | Logic that can run on modern .NET without WinForms (timer math, some XML) |
| WinFormsDrawing | Needs WinForms and/or System.Drawing for types or paint/settings |
| Win32 | P/Invoke to Windows APIs |
| ProcessMemory | Game/emulator memory reading (`ComponentUtil`, ASL scripts) |
| NativePE | Ships Windows `.dll` native code |

## Built-in component matrix

Status codes: `ALC-OK` = LoadFromAssemblyPath succeeded on Mac; `Types-FAIL` = full inspection failed; `Factory-partial` = factory constructed after partial type load (only where tested).

| Component | Source deps (static) | macOS 1.8.37 load | Ladder expectation |
|---|---|---|---|
| LiveSplit.BlankSpace | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing), **Factory-partial** | Adapter → minimal recompile |
| LiveSplit.Timer | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing), **Factory-partial** | Adapter → minimal recompile |
| LiveSplit.Title | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.Text | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.Splits | WinFormsDrawing | ALC-OK, Types-FAIL (WinForms) | Adapter → minimal recompile |
| LiveSplit.Subsplits | WinFormsDrawing | ALC-OK, Types-FAIL (WinForms) | Adapter → minimal recompile |
| LiveSplit.DetailedTimer | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.PreviousSegment | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.PossibleTimeSave | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.SumOfBest | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.RunPrediction | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.Delta | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.ComparisonTime | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.CurrentComparison | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.TotalPlaytime | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.Graph | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.HotkeyIndicator | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter + Mac hotkey backend |
| LiveSplit.Counter | WinFormsDrawing | ALC-OK, Types-FAIL (Drawing) | Adapter → minimal recompile |
| LiveSplit.ManualGameTime | WinFormsDrawing | ALC-OK, Types-FAIL (WinForms) | Adapter → minimal recompile |
| LiveSplit.WorldRecord | WinFormsDrawing | ALC-OK, Types-FAIL (WinForms) | Adapter + network; UI port |
| LiveSplit.TheRun | WinFormsDrawing | ALC-OK, Types-FAIL (WinForms) | Adapter / port |
| LiveSplit.Racetime | WinFormsDrawing (+ web UI deps) | ALC-OK, Types-FAIL (WinForms) | Port / replacement likely |
| LiveSplit.Sound | WinFormsDrawing + NativeMedia | ALC-OK, Types-FAIL (WinForms) | Minimal recompile + Mac audio |
| LiveSplit.Video | WinFormsDrawing + VLC/ActiveX | ALC-OK, Types-FAIL (WinForms) | Replacement (no AXVLC on Mac) |
| LiveSplit.ScriptableAutoSplit | WinFormsDrawing + ProcessMemory | ALC-OK, Types-FAIL (WinForms) | Port + Mac memory APIs / limited |
| LiveSplit.AutoSplittingRuntime | WinFormsDrawing + NativePE (`asr_capi`) | ALC-OK, Types-FAIL (WinForms) | Needs `asr_capi` macOS build or replacement |

## Core / host assemblies (Mac release corpus)

| Assembly | macOS result | Notes |
|---|---|---|
| LiveSplit.Core.dll | ALC-OK, Types-FAIL | Misses `System.Web.Extensions` / WinForms / ConfigurationManager among others |
| LiveSplit.View.dll | ALC-OK, Types-FAIL | Drawing/WinForms shell |
| UpdateManager.dll | Types-FAIL (WinForms) | |
| CustomFontDialog / WinFormsColor | Types-FAIL (WinForms) | |
| SpeedrunComSharp.dll | Types-FAIL (missing `System.Web.Extensions`) | Partial helper types constructible |
| Irony, Codaxy.Xlio, SharpZipLib, websocket-sharp, many System.* | **Full pass** | Not LiveSplit UI |
| SharpDX / SharpDX.DirectInput | Types OK; some ctors hit `kernel32` | Not a Mac input solution |
| x64/x86 `livesplit_core.dll`, `asr_capi.dll` | Native skip / BadImage | Build dylibs from source for Mac |

## Priority ladder status (M1)

| Rung | Status on macOS (verified) |
|---|---|
| Unchanged DLL (full UI component) | **Not viable** for built-in components — types never fully inspect |
| Unchanged DLL (ALC load only) | **Works** for all 26 built-ins — insufficient alone |
| Adapter (factory / partial types) | **Promising** — Timer/BlankSpace factories constructed; needs façade for draw/settings |
| Minimal recompile | **Planned** — sources build as net4.8.1 PE even on Mac SDK; need portable TFM + abstraction |
| Port | **Planned** for shell + hard components |
| Replacement | **Expected** for Video (VLC ActiveX), likely parts of ASR/autosplit |

## TODOs (post-M1)

- [ ] Design Drawing/WinForms façade vs dual `IComponent` interface (Milestone 4)
- [ ] Expand factory-instantiation probe to all 26 built-ins on Mac
- [ ] Build `liblivesplit_core.dylib` + wire P/Invoke on Apple Silicon
- [ ] Investigate ASR macOS artifacts / upstream support
- [x] Third-party component sampling from livesplit.org: API scan, 27 Oct (section below)

## Third-party components: API scan (27 Oct 2026)

Question: can third-party component DLLs work on Mac/Linux unchanged, through look-alike
libraries instead of WinForms / System.Drawing / LiveSplit.Core?

Corpus: the latest GitHub release of each "Downloadable" component on livesplit.org/components plus
Best World Segments, therun.gg's DLL and SoulSplitter (11 components). Scanned with
`tools/component-scan`, which reads metadata only (type and member references, base types,
P/Invoke).

| Component | Kind | Needs besides LiveSplit.Core / WinForms / Drawing | Mac verdict |
|---|---|---|---|
| Alternate Timing Method | IComponent, InfoTimeComponent | WinFormsColor (LiveSplit's color picker) | Shim candidate |
| Reset Chance | IComponent, InfoTextComponent | - | Shim candidate |
| Pace Placement | IComponent, InfoTextComponent | - | Shim candidate |
| Best World Segments | IComponent, own drawing | reflection into Windows' Subsplits internals | Works through the look-alikes, gold included (our Subsplits mirrored as LiveSplit.Subsplits) |
| LiveSplit.Discord | LogicComponent | native `discord_game_sdk` (Windows DLL only in the release) | Shim + native Mac library |
| therun.gg | LogicComponent | System.Web.Extensions JavaScriptSerializer | Already ported as a built-in |
| LiveSplit.Server | IComponent | Noesis.Javascript (C++/CLI, MSVCR100) | Already built in (deprecated component) |
| Focused Hotkeys | LogicComponent | user32 GetForegroundWindow / GetWindowText | Windows only (would need a Mac version of those calls) |
| TimeAttackPause | ControlComponent + Form | WinForms Form / Paint | Big WinForms surface; poor candidate |
| SourceSplit | auto splitter | reads Source-engine game memory (ComponentUtil, SigScan), winmm | Game must be a local process; not on Mac |
| SoulSplitter | auto splitter | WPF (MaterialDesign), process memory, CreateRemoteThread, Rust DLLs | Not on Mac |

API surface the four display components plus Discord use (distinct, from member references):

| Library | Types | Members |
|---|---:|---:|
| LiveSplit.Core | 39 | 119 |
| System.Windows.Forms | 52 | 126 |
| System.Drawing | 17 | 23 |

All 11: LiveSplit.Core 58 types / 203 members, WinForms 105 / 258, Drawing 20 / 40 (plus WPF for
SoulSplitter). Every component references UpdateManager only for `IUpdateable`.

Findings:
- LiveSplit.Core use is mostly LiveSplitState (Run, CurrentSplitIndex, CurrentPhase, CurrentTime,
  CurrentTimingMethod, LayoutSettings, the OnStart/OnSplit/OnReset/... events), Time, ISegment,
  SettingsHelper, the time formatters, SimpleLabel, GraphicsCache and the InfoText/InfoTime
  components. All of these exist in our Core already as ports; a look-alike `LiveSplit.Core` would
  wrap them under Windows' names and signatures.
- WinForms use is almost entirely the settings page, written by the WinForms designer:
  UserControl, TableLayoutPanel with Row/ColumnStyles, Label, CheckBox, ComboBox, RadioButton,
  NumericUpDown, GroupBox, TextBox, Button, and `DataBindings.Add` (data binding to settings
  properties). A look-alike would build the same page from Avalonia controls.
- System.Drawing use in drawing is small: Graphics.FillRectangle, MeasureString, LinearGradientBrush,
  SolidBrush, Font, Color, Region (the Draw* signatures). A look-alike Graphics would draw into the
  timer's own renderer.
- Auto splitters that read game memory cannot work on a Mac whatever the shim: the game is not a
  local process (and P/Invoke to kernel32 has no Mac counterpart). ASR (wasm) is the cross-platform
  route, already ported.
- Loading: .NET Framework 4.x component DLLs load on .NET 10 (mscorlib and System facades exist);
  a per-component AssemblyLoadContext can hand out our look-alikes when a component asks for
  `LiveSplit.Core`, `System.Windows.Forms`, `System.Drawing` and `UpdateManager`, so the DLL is not
  changed (no converter needed for these). Names and member signatures must match Windows' exactly,
  because the DLL's references bind by name and signature; base classes (UserControl, Control,
  InfoTextComponent, LogicComponent) must be subclassable with the same virtual members.

Proof done 27 Oct: Reset Chance's release DLL loads unchanged through the look-alikes in
src/Compat, is offered in the Add menu, draws in the layout, and its WinForms settings page works.
Pace Placement, Alternate Timing Method and Best
World Segments followed the same day.
