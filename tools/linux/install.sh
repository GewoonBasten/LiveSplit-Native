#!/bin/bash
# Adds LiveSplit to the app menu with its icon (a desktop entry for this folder), or removes it
# with --remove. Nothing outside your home folder is changed.
here="$(cd "$(dirname "$0")" && pwd)"
apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
icons="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/256x256/apps"
entry="$apps/livesplit-native.desktop"

if [ "$1" = "--remove" ]; then
  rm -f "$entry" "$icons/livesplit-native.png"
  echo "Removed LiveSplit from the app menu."
  exit 0
fi

mkdir -p "$apps" "$icons"
cp "$here/livesplit.png" "$icons/livesplit-native.png"
cat > "$entry" <<DESKTOP
[Desktop Entry]
Type=Application
Name=LiveSplit
Comment=Speedrun timer
Exec="$here/LiveSplitNative.App"
Path=$here
Icon=livesplit-native
Terminal=false
Categories=Game;Utility;
StartupWMClass=LiveSplitNative.App
DESKTOP
chmod +x "$entry"
command -v update-desktop-database > /dev/null && update-desktop-database "$apps" > /dev/null 2>&1
command -v gtk-update-icon-cache > /dev/null && gtk-update-icon-cache -q "${icons%/256x256/apps}" > /dev/null 2>&1
echo "LiveSplit is in your app menu ($entry)."
