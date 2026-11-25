#!/bin/bash
# Builds LiveSplit for Linux, self-contained (no .NET install needed to run it), from a
# Mac or a Linux machine with the .NET 10 SDK: dist/linux-<arch>/ (run ./LiveSplitNative.App)
# and dist/LiveSplit-Native-linux-<arch>.tar.gz (with INSTALL-LINUX.md). Arch: x64 (default) or arm64.
# The Linux port is being prepared (docs/linux-readiness.md): the timer, splits, layouts,
# settings and downloaded components work; global hotkeys, gamepads, sounds, racetime.gg's
# chat and saved logins don't yet.
cd "$(dirname "$0")/../.." || exit 1

DOTNET="$(command -v dotnet)"
for candidate in /usr/local/share/dotnet/dotnet /opt/homebrew/bin/dotnet "$HOME/.dotnet/dotnet"; do
  [ -z "$DOTNET" ] && [ -x "$candidate" ] && DOTNET="$candidate"
done
if [ -z "$DOTNET" ]; then
  echo "Could not find .NET (dotnet). Install the .NET 10 SDK from https://dotnet.microsoft.com/download"
  exit 1
fi

arch="${1:-x64}"
case "$arch" in
  x64|arm64) ;;
  *) echo "usage: $0 [x64|arm64]"; exit 1 ;;
esac
rid="linux-$arch"
out="dist/$rid"
version="$(TZ=UTC git log -1 --format=%cd --date=format-local:%Y.%m.%d.%H%M 2>/dev/null || date -u +%Y.%m.%d.%H%M)"

echo "== publish $rid ($version)"
rm -rf "$out"
"$DOTNET" publish src/LiveSplitNative.App -c Release -r "$rid" --self-contained true -o "$out" \
  -p:InformationalVersion="$version" -nologo -v minimal || exit 1
if [ ! -f "$out/compat/LiveSplit.Core.dll" ]; then
  echo "compat/ is missing from the published app."
  exit 1
fi
chmod +x "$out/LiveSplitNative.App"
cp INSTALL-LINUX.md "$out/INSTALL-LINUX.md"
# The release's version: the app's update check compares it with GitHub's newest release.
echo "$version" > "$out/version.txt"

echo "== archive"
# Built on a Mac: without macOS's file attributes, which Linux's tar warns about for every file.
if [ "$(uname)" = "Darwin" ]; then
  COPYFILE_DISABLE=1 tar --no-xattrs --no-mac-metadata -C dist -czf "dist/LiveSplit-Native-$rid.tar.gz" "$rid"
else
  tar -C dist -czf "dist/LiveSplit-Native-$rid.tar.gz" "$rid"
fi
du -sh "$out" "dist/LiveSplit-Native-$rid.tar.gz"
echo "PUBLISH_LINUX_EXIT=0"
