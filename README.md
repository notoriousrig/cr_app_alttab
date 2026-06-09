# AltTabCustom

A customizable **Alt + Tab window switcher for Windows 11**, in the spirit of
Alt‑Tab Terminator but with the bits you actually want to tweak exposed:
number of items on screen, columns, fonts, font size, colors, item size, and
more.

**It never requires administrator privileges.** It runs entirely as the normal
invoking user (`asInvoker` manifest), stores its config under `%AppData%`, and
can optionally start with Windows via the per‑user registry Run key — no
elevation anywhere.

---

## Features

- **Drop‑in Alt + Tab replacement** — intercepts <kbd>Alt</kbd>+<kbd>Tab</kbd>
  via a low‑level keyboard hook (no admin needed) and shows its own overlay.
- **Navigation**
  - <kbd>Alt</kbd>+<kbd>Tab</kbd> — open / next
  - <kbd>Alt</kbd>+<kbd>Shift</kbd>+<kbd>Tab</kbd> — open / previous
  - Arrow keys — move selection
  - <kbd>Enter</kbd> — activate; <kbd>Esc</kbd> — cancel
  - Release <kbd>Alt</kbd> — activate the highlighted window
  - Mouse click — activate (optional)
- **Customizable** (live, via the tray → Settings dialog):
  - Max items on screen, number of columns
  - Item width/height, icon size
  - Font family, title font size, bold, process‑name sub‑label + size
  - Background / selection / text / sub‑text colors, corner radius, opacity
- **Lightweight tray app** — right‑click the tray icon for Settings / Exit.
- **Optional start‑with‑Windows** (per‑user, no admin).

## Why no admin is needed

`RegisterHotKey` cannot bind <kbd>Alt</kbd>+<kbd>Tab</kbd> (the system reserves
it), so AltTabCustom uses a **`WH_KEYBOARD_LL` low‑level keyboard hook**. That
hook works from a standard‑privilege process — it observes the key sequence and
*swallows* the system Alt+Tab, then shows our overlay instead.

One consequence of running without elevation: a non‑elevated hook cannot
intercept input while an **elevated (admin) window** is in the foreground. In
that case Windows' built‑in Alt+Tab takes over for that window only. This is a
deliberate Windows security boundary, not a bug — getting around it would
require running AltTabCustom as admin, which defeats the requirement.

## Requirements

- Windows 10 / 11 (x64)
- To build: the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- To run the published single‑file build: **nothing** — it is self‑contained.

## Build & run

From the repo root in PowerShell:

```powershell
./build.ps1          # produces publish/AltTabCustom.exe (self-contained)
./build.ps1 -Run     # build, then launch
```

Or with the SDK directly:

```powershell
# Self-contained single file (no runtime install needed on target):
dotnet publish src/AltTabCustom/AltTabCustom.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish

# Or framework-dependent (smaller; requires the .NET 8 Desktop Runtime installed):
dotnet build src/AltTabCustom/AltTabCustom.csproj -c Debug
```

Run `publish/AltTabCustom.exe`. It minimizes to the system tray. Right‑click the
tray icon → **Settings…** to customize, or **Exit** to quit.

> The app replaces Alt+Tab only while it is running. To make it permanent,
> enable **Start with Windows** in Settings.

## Configuration

Settings live in:

```
%AppData%\AltTabCustom\settings.json
```

You can edit the dialog or the JSON directly. Defaults are applied for any
missing/invalid values, so a bad edit can never brick the app.

## Project layout

```
AltTabCustom.sln
src/AltTabCustom/
  App.xaml(.cs)              # tray app bootstrap, single-instance, settings wiring
  app.manifest              # asInvoker (no admin) + Per-Monitor v2 DPI
  Core/
    SwitcherController.cs   # the Alt+Tab state machine
    StartupManager.cs       # per-user "start with Windows"
  Interop/
    NativeMethods.cs        # all P/Invoke
    KeyboardHook.cs         # WH_KEYBOARD_LL global hook
    WindowEnumerator.cs     # "what shows in Alt+Tab" enumeration
    WindowActivator.cs      # reliable SetForegroundWindow
    IconHelper.cs           # window/process icon -> WPF ImageSource
    WindowInfo.cs
  Settings/
    AppSettings.cs          # the customizable options
    SettingsStore.cs        # JSON load/save under %AppData%
  UI/
    SwitcherWindow.xaml(.cs)# the overlay
    SwitcherItem.cs
    SettingsWindow.xaml(.cs)# the settings dialog
```

## Known limitations / roadmap

- **Static icons, not live thumbnails (yet).** v1 shows each window's icon.
  Live DWM thumbnails (`DwmRegisterThumbnail`) are a planned addition; they
  render as an OS‑composited layer rather than a WPF visual, so they need extra
  plumbing.
- **Elevated foreground windows** fall back to the system switcher (see above).
- The first Alt+Tab of a session does the window enumeration + icon load inside
  the hook callback. On a machine with a very large number of windows this can
  approach Windows' low‑level‑hook timeout; if you ever see a missed first
  press, that's the cause. Subsequent navigation is instant.
- No custom app icon yet (uses the default application icon in the tray).

## License

MIT — see `LICENSE` if present, otherwise treat as MIT.
