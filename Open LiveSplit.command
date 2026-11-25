#!/bin/bash
# Double-click in Finder to open LiveSplit Native: builds what changed (a few seconds when
# nothing did), then starts the app. You can close this Terminal window once it is open.
cd "$(dirname "$0")" || exit 1

DOTNET="$(command -v dotnet)"
for candidate in /usr/local/share/dotnet/dotnet /opt/homebrew/bin/dotnet "$HOME/.dotnet/dotnet"; do
  [ -z "$DOTNET" ] && [ -x "$candidate" ] && DOTNET="$candidate"
done
if [ -z "$DOTNET" ]; then
  echo "Could not find .NET (dotnet). Install the .NET 10 SDK from https://dotnet.microsoft.com/download"
  read -r -p "Press Return to close."
  exit 1
fi

if pgrep -f "LiveSplitNative.App.dll" > /dev/null; then
  echo "LiveSplit is already open."
  sleep 2
  exit 0
fi

echo "Building LiveSplit..."
if ! "$DOTNET" build src/LiveSplitNative.App -c Release -nologo -v quiet; then
  echo "The build failed (see above)."
  read -r -p "Press Return to close."
  exit 1
fi

nohup "$DOTNET" src/LiveSplitNative.App/bin/Release/net10.0/LiveSplitNative.App.dll > /dev/null 2>&1 &
disown
echo "LiveSplit is opening. You can close this window."
sleep 2
