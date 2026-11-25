# Upstream tracking

Local clones are **not** vendored into the app root. Prefer documenting clone commands; keep trees under `/upstream` (gitignored) or any scratch path.

## Tracked repositories

| Repo | URL | Role |
|---|---|---|
| LiveSplit | https://github.com/LiveSplit/LiveSplit | Primary Windows app + component submodules |
| livesplit-core | https://github.com/LiveSplit/livesplit-core | Cross-platform timing/parse/layout library (C API) |

Component submodules live under `LiveSplit/components/LiveSplit.*` and `lib/SpeedrunComSharp`, `lib/livesplit-core` (LiveSplit’s pinned livesplit-core submodule).

## SHAs analyzed in Milestone 1 (2026-10-08)

### LiveSplit (recursive clone)

| Field | Value |
|---|---|
| Commit | `93a11b85b0593650b8c398615d61c9d57cecb84e` |
| Describe | `1.8.37-60-g93a11b85` |
| Release binaries tested | **1.8.37** zip (`LiveSplit_1.8.37.zip`, SHA-256 `14bc8ef8ded9ef4033fb2f0cb6a152386d393127da18a4de14f096c5347aa991`) |

Pinned submodule commits at that clone (abbreviated; full list via `git submodule status`):

| Path | Commit |
|---|---|
| `lib/livesplit-core` | `61070c47ea91e6e148d6801cb7a03e8e32a2ebc9` (`v0.13.0-214-g61070c4`) |
| `lib/SpeedrunComSharp` | `371673465625740b2cdf23ad2ed91740db2a7235` |
| `components/LiveSplit.Timer` | `6c9e478d94d77e491619b1170fe37df0c674aada` |
| `components/LiveSplit.Splits` | `20bbf92b7eb30d5a3cd5c0d6dfa931ba2df01841` |
| `components/LiveSplit.ScriptableAutoSplit` | `0d0d2f11da3e64c178053806fbf20cc517c9b000` |
| `components/LiveSplit.AutoSplittingRuntime` | `132dac14fa85b7a362309d53b72ccccb2a4f098e` |

(Other `components/LiveSplit.*` SHAs recorded in the M1 research environment via `git submodule status`.)

### livesplit-core (standalone clone)

| Field | Value |
|---|---|
| Commit | `7ce9729042cf9acc429ac796ae7470a614c57d97` |
| Describe | `v0.13.0-227-g7ce9729` |

Note: LiveSplit’s submodule pin (`61070c47…`) is slightly behind this standalone tip. Prefer LiveSplit’s pin when matching Windows LiveSplit behavior; advance deliberately.

## Clone commands

```bash
mkdir -p upstream
git clone --recursive https://github.com/LiveSplit/LiveSplit.git upstream/LiveSplit
git clone --recursive https://github.com/LiveSplit/livesplit-core.git upstream/livesplit-core

# Record pins
git -C upstream/LiveSplit rev-parse HEAD
git -C upstream/LiveSplit submodule status
git -C upstream/livesplit-core rev-parse HEAD
```

Optional Windows release corpus (managed DLL tests only — do not commit the zip):

```bash
curl -sL -o /tmp/LiveSplit_1.8.37.zip \
  https://github.com/LiveSplit/LiveSplit/releases/download/1.8.37/LiveSplit_1.8.37.zip
unzip -q /tmp/LiveSplit_1.8.37.zip -d /tmp/LiveSplit_1.8.37
```

## Sync process sketch (Milestone 6 will harden)

1. **Fetch** LiveSplit + update submodules; fetch livesplit-core.
2. **Diff** against last known-good pins (this file + a future `upstream.lock` if added).
3. **Classify** changes: model/timer/serialization vs WinForms UI vs components vs native caps.
4. **Port/cherry-pick** into LiveSplit Native branches — never auto-merge unverified upstream into stable.
5. **Rebuild** macOS `liblivesplit_core.dylib` when the livesplit-core pin moves; re-run component load harness on Apple Silicon.
6. **Update** this file’s SHAs and the COMPONENT_COMPATIBILITY notes.

## What not to vendor

- Full upstream git trees
- Windows release zips / PE native DLLs
- Harness temp directories from Mac/Linux probes
