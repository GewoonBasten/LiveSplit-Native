# Roadmap

State as of November 2026. Linux details: [docs/linux-readiness.md](docs/linux-readiness.md).

| Milestone | Status |
|---|---|
| M1 Research | Done — Approach B (Hybrid) |
| M2 Functional `.app` | Done — timing, `.lss` / `.lsl` / settings.cfg read and written as Windows does |
| M3 Interface | Done — timer, menus, Splits Editor, Layout Editor, Settings, the settings look everywhere |
| M4 Components | Mostly done — built-in components ported; Windows component DLLs run through look-alikes |
| M5 Integrations | Mostly done — global hotkeys, gamepads, servers, racetime.gg, Share, From URL, updates |
| M6 Upstream sync | Not started |
| M7 Polish / release | In progress — app icon, one build signature (keeps the hotkey permission), install guides, tester builds (macOS + Linux) |

## Done

- **Timer and window:** borderless move/resize, Windows size rules, timer-only mode, orientation
  switch, drift correction, Hide, scroll a split with the wheel.
- **Files:** splits, layouts and settings.cfg as Windows LiveSplit; recent files; Edit History;
  Open Splits / Layout From URL; Hotkey Profiles; translations (Chinese catalog).
- **Splits Editor:** real `.lss` data, RT/GT, Other... (Clear History, Clear Times, Clean Sum of
  Best), Use Layout, speedrun.com game/category suggestions and tools.
- **Comparisons:** all nine upstream generators, tested against upstream LiveSplit.Core.
- **Components ported:** Title, Splits, Subsplits, Timer, Detailed Timer, Previous Segment, Sum of
  Best, Possible Time Save, Text, Separator, Blank Space, Run Prediction, Delta, Comparison Time,
  Current Comparison, Total Playtime, Counter, Graph, Hotkey Indicator, Manual Game Time, World
  Record, Sound Effects, therun.gg, Contextual Slideshow, AutoSplit Integration (Pilgrim), Auto
  Splitting Runtime (WebAssembly auto splitters).
- **Windows component DLLs:** load unchanged through look-alike libraries (Reset Chance, Pace
  Placement, Alternate Timing Method, Best World Segments, ...), with a crash guard and their
  settings pages in the settings look.
- **Layout:** fonts, background image, Component Spacing, live preview with OK/Cancel.
- **Integrations:** global hotkeys (macOS event tap, Linux X11), gamepads as hotkeys, TCP and
  WebSocket servers, racetime.gg (sign-in, races, race window), Manage Racing Services,
  Share... (Speedrun.com, Screenshot, Excel), updates from GitHub releases.
- **Linux:** x64 and arm64 builds; fonts, hotkeys, gamepads, sound, saved logins, updates.

## Left to do

1. **Open question:** a segment group's own split shows as a separate row, as on Windows (keep
   Windows' behavior or show the header row instead).
2. **Not yet tried for real:** AutoSplit Integration with a real Pilgrim Autosplitter; racetime.gg
   with a real account; Linux gamepads with a real controller; Linux on a real X11 / Wayland
   display and on x64.
3. **Not ported:** ASL auto splitters (LiveSplit.ScriptableAutoSplit — needs game memory reading on
   macOS/Linux); the server's named pipe.
4. **Linux gaps:** Deactivate Hotkeys For Other Programs; native Wayland hotkeys; Auto Splitting
   Runtime library for Linux; packaging (AppImage or Flatpak).
5. **Component DLL gaps:** fonts/images in their settings, windows a component opens itself,
   ControlComponent.
6. **Small differences from Windows:** Previous Segment settings order; a custom comparison named
   like a generated one.
7. **M6 Upstream sync:** a process for pulling new official LiveSplit changes (sketch in
   [UPSTREAM.md](UPSTREAM.md)).
