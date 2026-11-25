# Installing LiveSplit for macOS (test builds)

LiveSplit for macOS is a Mac version of [LiveSplit](https://livesplit.org). Test builds are not
signed by Apple, so macOS asks you to confirm once that you want to open it. After that it
opens like any other app.

**Needs:** a Mac with Apple Silicon (M1 or newer) and macOS 12 or newer. Nothing else to
install.

## 1. Download and unpack

1. Download `LiveSplit-Native-arm64.zip` from the release.
2. Double-click the zip. You get **LiveSplit**.
3. Drag **LiveSplit** into your **Applications** folder.

## 2. Open it the first time

1. Double-click **LiveSplit**. macOS says it can't check the app for malicious
   software. Click **Done** (not Move to Trash).
2. Open **System Settings → Privacy & Security** and scroll down to **Security**. You'll see
   "LiveSplit was blocked to protect your Mac". Click **Open Anyway**.
3. Enter your Mac's password if asked, then click **Open Anyway** once more.

You only do this once. (On macOS 14 and older, right-clicking the app and choosing **Open**
also works.)

## 3. Hotkeys

Global hotkeys (splitting while the game has focus) need permission from macOS:

- **Input Monitoring:** while this is off, LiveSplit asks when it starts. Click **Open System
  Settings** and turn on LiveSplit in **Privacy & Security → Input Monitoring**; hotkeys start
  working a few seconds later. (The first time, macOS asks once more on its own.) Tick
  **Don't ask again** if you don't use global hotkeys.
- **Accessibility** (only for **Deactivate Hotkeys For Other Programs**): turn on LiveSplit
  in **System Settings → Privacy & Security → Accessibility**.

## Your files

- Settings: `~/Library/Application Support/LiveSplit Native/settings.cfg` (the folder keeps the
  project's name, LiveSplit Native, so it is not mixed up with other LiveSplit apps)
- Downloaded components (`.dll` files from livesplit.org): the **Components** folder next to
  that settings file. Splits (`.lss`) and layouts (`.lsl`) from Windows LiveSplit open as they are.

If LiveSplit quits unexpectedly while downloaded components are in that folder, it
starts the next time with them turned off and asks whether to turn them back on.

## Reporting a problem

Please say what you did, what you expected and what happened, and attach:

- the splits and layout you used, if it's about them;
- after a crash, the newest `LiveSplitNative.App-….ips` file from
  `~/Library/Logs/DiagnosticReports` (in Finder: **Go → Go to Folder…** and paste that path).
