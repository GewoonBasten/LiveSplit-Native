# Vendored upstream source

Unmodified copies of files from [LiveSplit](https://github.com/LiveSplit/LiveSplit)
`src/LiveSplit.Core`, commit `93a11b85b0593650b8c398615d61c9d57cecb84e` (2026-09-15),
under the MIT license in `LICENSE.txt`. They exist only so the differential tests can run
Windows LiveSplit's own timing, comparison and formatting code next to ours.

| Folder | Upstream path |
| --- | --- |
| `Model/` | `src/LiveSplit.Core/Model/` (20 files) |
| `Model/Comparisons/` | `src/LiveSplit.Core/Model/Comparisons/` (all 14 files) |
| `TimeFormatters/` | `src/LiveSplit.Core/TimeFormatters/` (5 files) |
| `Model/RunFactories/` | `src/LiveSplit.Core/Model/RunFactories/` (`IRunFactory.cs`, `StandardFormatsRunFactory.cs`) |
| `LiveSplitCore.g.cs` | `src/LiveSplit.Core/LiveSplitCore.g.cs` (livesplit-core's generated C# bindings) |

The run factory is how Windows LiveSplit opens splits: livesplit-core (submodule
`lib/livesplit-core` at `61070c47ea91e6e148d6801cb7a03e8e32a2ebc9`) parses the file and the
factory turns the result into LiveSplit's model. The corpus tests need livesplit-core's C API
as `liblivesplit_core.dylib` beside the tests (`native/README.md`); without it they skip.

`Model/TimeStamp.cs` is deliberately not copied: `../Shims/UpstreamShims.cs` provides a
TimeStamp with the same API that reads LiveSplitNative's clock source, so both
implementations see the same manual clock. The other shims stand in for WinForms, GDI+,
speedrun.com metadata, the event log and the JSON helper, which the timer logic does not use
(images "decode" to their size only).

To update: copy the same files from a newer upstream commit, update the commit above,
and run the tests. A failing differential test then shows exactly where behaviour changed.
