# Native context menus (macOS)

## Principle

**OS chrome** (right-click menus, file dialogs) uses the **real platform menu**, not a styled Avalonia/WinForms clone.

- **macOS:** AppKit `NSMenu` / `NSMenuItem` via libobjc (`MacOsNativeContextMenu`).
- **Linux (future):** GTK/Qt/XDG through the same `INativeContextMenu` seam — see [linux-readiness.md](./linux-readiness.md).
- **Fallback:** `Avalonia.ContextMenu` only when NSMenu is unavailable (not claimed as native).

Timer **surface** rendering stays Windows LiveSplit fidelity. Native menus must not restyle the timer.

## API proof

| Check | Evidence |
|--------|----------|
| Backend name | Startup log: `Context menu backend: AppKit.NSMenu (systemNative=True)` |
| Types | `objc_getClass("NSMenu")` / `NSMenuItem`; `popUpMenuPositioningItem:atLocation:inView:` |
| Abstraction | `INativeContextMenu` + `NativeMenuModel` in `src/LiveSplitNative.App/Platform/` |
| Not used for live right-click | Avalonia `Window.ContextMenu` removed from `MainWindow.axaml` |

## Hierarchy (TimerForm parity)

Authority: upstream `TimerForm.Designer.cs` + `RebuildControlMenu` / comparisons rebuild.

```
Edit Splits...
Open Splits ▸ From File... / From URL… (disabled) / Edit History (disabled)
Save Splits | Save Splits As... | Close Splits
──
Control ▸ Start|Split / Reset / Undo / Skip / Pause|Resume / Undo All Pauses /
          Global Hotkeys (check) / TCP (disabled) / WebSocket (disabled)
Compare Against ▸ comparisons… / Real Time / Game Time
──
Share... (disabled)
──
Edit Layout...
Open Layout ▸ From File... / From URL… (disabled) / Default / Edit History (disabled)
Save Layout | Save Layout As...
──
Settings
──
About | Exit
```

## Layout Editor — Add Component

Same `INativeContextMenu` path. Windows category order: Timer / List / Information / Media / Control / Other, then separator + disabled **Download More...**.

Builder: `LayoutEditorWindow.BuildAddComponentMenuModel`.
