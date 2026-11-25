# Native libraries

## asr-capi (Auto Splitting Runtime)

`asr-capi/` is LiveSplit.AutoSplittingRuntime's C API over livesplit-core's
`livesplit-auto-splitting` crate (copied from LiveSplit.AutoSplittingRuntime 132dac1, the
submodule LiveSplit 93a11b85 uses; the crate pinned to the revision its Cargo.lock locks). The
Auto Splitting Runtime component loads it as `libasr_capi.dylib` (macOS) next to the app.

Build it with Rust installed:

    cd native/asr-capi && cargo build --release
    mkdir -p ../osx-arm64 && cp target/release/libasr_capi.dylib ../osx-arm64/

## livesplit-core (the .lss corpus tests)

Windows LiveSplit opens splits with livesplit-core (StandardFormatsRunFactory over its C API).
The corpus tests in tests/LiveSplitNative.Upstream.Tests run that same code path, so they need
livesplit-core's C API as `liblivesplit_core.dylib`, built from the commit LiveSplit 93a11b85
pins (`lib/livesplit-core` at 61070c47ea91e6e148d6801cb7a03e8e32a2ebc9).

Build it with Rust installed (`native/livesplit-core-src/` is ignored by git):

    git clone https://github.com/LiveSplit/livesplit-core.git native/livesplit-core-src
    cd native/livesplit-core-src && git checkout 61070c47ea91e6e148d6801cb7a03e8e32a2ebc9
    cargo build --release -p livesplit-core-capi
    mkdir -p ../osx-arm64 && cp target/release/liblivesplit_core.dylib ../osx-arm64/

Without it, the corpus tests that need livesplit-core are skipped.
