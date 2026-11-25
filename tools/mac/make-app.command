#!/bin/bash
# Builds "LiveSplit.app" for this Mac's chip (dist/), self-contained: no .NET install is
# needed to run it. Double-click in Finder or run from a shell. The app carries compat/ (what
# downloaded LiveSplit components load against) and, when it was built, the Auto Splitting
# Runtime's library. dist/LiveSplit-Native-<arch>.zip is the same app, ready to send to others.
cd "$(dirname "$0")/../.." || exit 1

DOTNET="$(command -v dotnet)"
for candidate in /usr/local/share/dotnet/dotnet /opt/homebrew/bin/dotnet "$HOME/.dotnet/dotnet"; do
  [ -z "$DOTNET" ] && [ -x "$candidate" ] && DOTNET="$candidate"
done
if [ -z "$DOTNET" ]; then
  echo "Could not find .NET (dotnet). Install the .NET 10 SDK from https://dotnet.microsoft.com/download"
  exit 1
fi

arch="$(uname -m)"
rid="osx-$arch"
[ "$arch" = "x86_64" ] && rid="osx-x64"
publish="publish/$rid"
# Assembled and signed in publish/, then copied to dist/: once the app was opened from dist/,
# macOS keeps attributes on that folder that codesign refuses ("detritus not allowed").
app="publish/LiveSplit.app"
final="dist/LiveSplit.app"
# (Before 2 Nov 2026 the app was "LiveSplit Native.app"; an old one in dist goes.)
rm -rf "dist/LiveSplit Native.app"

echo "== publish $rid"
rm -rf "$publish"
"$DOTNET" publish src/LiveSplitNative.App -c Release -r "$rid" --self-contained true -o "$publish" -nologo -v minimal || exit 1
if [ ! -f "$publish/compat/LiveSplit.Core.dll" ]; then
  echo "compat/ is missing from the published app."
  exit 1
fi

echo "== assemble $app"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
ditto "$publish" "$app/Contents/MacOS"
cp tools/mac/AppIcon.icns "$app/Contents/Resources/AppIcon.icns"
# The version is the last commit's time (a release's tag is "v" and this; the app compares it
# with GitHub's newest release to offer updates).
version="$(TZ=UTC git log -1 --format=%cd --date=format-local:%Y.%m.%d.%H%M 2>/dev/null || date -u +%Y.%m.%d.%H%M)"
build="$(git rev-parse --short HEAD 2>/dev/null || echo local)"
cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleExecutable</key><string>LiveSplitNative.App</string>
  <key>CFBundleIdentifier</key><string>org.livesplit.LiveSplitNative</string>
  <key>CFBundleName</key><string>LiveSplit</string>
  <key>CFBundleDisplayName</key><string>LiveSplit</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundleVersion</key><string>$build</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
</dict></plist>
PLIST
chmod +x "$app/Contents/MacOS/LiveSplitNative.App"
# codesign refuses a bundle whose files carry Finder data or other extended attributes
# ("resource fork, Finder information, or similar detritus not allowed"); they go first.
detritus="$(xattr -r "$app" 2>/dev/null | head -n 5)"
[ -n "$detritus" ] && printf 'Removing extended attributes before signing:\n%s\n' "$detritus"
xattr -cr "$app"

# One signature for every build (tools/mac/make-signing-certificate.command, run once): macOS
# then keeps the app's permissions (Input Monitoring, Accessibility) across builds and updates.
# Without that certificate the build is signed ad hoc, which macOS sees as a new app each time.
identity="LiveSplit Native Signing"
if security find-certificate -c "$identity" > /dev/null 2>&1; then
  echo "== sign ($identity)"
  codesign --force --deep -s "$identity" --identifier org.livesplit.LiveSplitNative "$app" || exit 1
else
  echo "== sign (ad hoc: run tools/mac/make-signing-certificate.command once to keep permissions across builds)"
  codesign --force --deep -s - "$app" || exit 1
fi
codesign --verify --deep "$app" && echo "signature ok"
mkdir -p dist
rm -rf "$final"
ditto "$app" "$final"
codesign --verify --deep "$final" || exit 1
app="$final"

echo "== zip"
zip="dist/LiveSplit-Native-$arch.zip"
rm -f "$zip"
ditto -c -k --keepParent "$app" "$zip"

echo "== contents"
ls "$app/Contents/MacOS/compat"
ls "$app/Contents/MacOS" | grep -E '^libasr_capi.dylib$' || echo "(no Auto Splitting Runtime library: see native/README.md)"
du -sh "$app" "$zip"
echo "Version $version: publish dist/LiveSplit-Native-$arch.zip as a GitHub release tagged v$version (the app's update check finds it)."
echo "MAKE_APP_EXIT=0"
