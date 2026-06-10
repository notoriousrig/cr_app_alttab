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
  - Arrow keys — move selection; <kbd>Home</kbd> / <kbd>End</kbd> — jump to first / last
  - <kbd>1</kbd>–<kbd>9</kbd> — jump straight to that row (when no search is active)
  - **Type to filter** — start typing to narrow the list by window title or
    process name (the match is highlighted); <kbd>Backspace</kbd> clears a character
  - <kbd>Delete</kbd> — close the highlighted window (without switching to it)
  - <kbd>Enter</kbd> — activate; <kbd>Esc</kbd> — cancel
  - Release <kbd>Alt</kbd> — activate the highlighted window
  - Left mouse click — activate (optional); middle click — close that window
- **True MRU ordering** — the list is ordered most-recently-used (tracked via a
  focus hook), so a quick Alt+Tab flips to your last window just like the
  system switcher.
- **Tap vs hold** — a quick Alt+Tab tap switches straight to your previous
  window with no overlay; hold Alt a moment (configurable delay) to open the
  switcher and browse. Set the delay to 0 to always show it immediately.
- **Display profiles (dock / undock)** — two independent visual profiles,
  **Docked** (large monitor) and **Laptop** (small screen), are switched
  automatically based on the effective width of the monitor the switcher opens
  on. Plug into a 49″ ultrawide and it uses your roomy layout; unplug and it
  flips to the compact one. The crossover width is configurable, and
  auto-switching can be turned off (always uses the Docked profile).
- **Customizable** (per profile, via the tray → Settings dialog):
  - Max items on screen, number of columns
  - Item width/height, icon size
  - Font family, **font weight** (Thin → Black, e.g. *Light* for Bahnschrift),
    title font size, process‑name sub‑label + size
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

## Download

Prebuilt Windows binaries are attached to each
[GitHub Release](https://github.com/notoriousrig/cr_app_alttab/releases/latest).
Two flavors of the same app — pick one:

| File | Size | Needs |
|---|---|---|
| **`AltTabCustom.exe`** | ~70 MB | Nothing — self-contained, no install, **no admin**. *Recommended.* |
| **`AltTabCustom-requires-dotnet8.exe`** | ~3 MB | The [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed first. |

The big file bundles the entire .NET runtime + WPF, which is why it's large; it's
the price of being zero-install. The slim file skips the runtime, so it's tiny
but won't start unless the .NET 8 Desktop Runtime is present (installing that
usually needs admin — which is why the self-contained build is recommended for
the no-admin use case).

> Note: trimming (which normally shrinks self-contained apps) is **not supported
> for WPF**, so the self-contained build can't be made meaningfully smaller.

## Requirements

- Windows 10 / 11 (x64)
- To build: the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- To run the self‑contained build: **nothing** — the runtime is bundled.

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
    SwitcherController.cs   # the Alt+Tab state machine (nav, search, close, tap/hold)
    MruTracker.cs           # focus-history WinEvent hook
    MruOrder.cs             # pure most-recently-used ordering (unit tested)
    DisplayMetrics.cs       # effective monitor width (for profile switching)
    Logger.cs               # best-effort logging to %AppData%
    StartupManager.cs       # per-user "start with Windows"
  Interop/
    NativeMethods.cs        # all P/Invoke
    KeyboardHook.cs         # WH_KEYBOARD_LL global hook
    WindowEnumerator.cs     # "what shows in Alt+Tab" enumeration
    WindowActivator.cs      # reliable SetForegroundWindow
    IconHelper.cs           # window/process icon -> WPF ImageSource
    WindowInfo.cs
  Settings/
    AppSettings.cs          # behavior + profile switching + the two profiles
    DisplayProfile.cs       # per-display visual settings (Docked / Laptop)
    SettingsStore.cs        # JSON load/save + v1->v2 migration, under %AppData%
  UI/
    SwitcherWindow.xaml(.cs)    # the overlay
    SwitcherItem.cs
    ProfileEditorControl.xaml(.cs)  # editor for one display profile
    FieldParse.cs               # tolerant settings-field parsing
    SettingsWindow.xaml(.cs)    # the tabbed settings dialog
tests/AltTabCustom.Tests/      # xUnit tests for the pure logic (run in CI)
```

## Troubleshooting

AltTabCustom writes a log to:

```
%AppData%\AltTabCustom\log.txt
```

Startup, the keyboard hook, and any errors are recorded there — that's the
first place to look if Alt+Tab stops responding or a window won't activate.

## Known limitations

- **Icon-based items by design.** Each entry shows the window's icon, title, and
  process — not a live thumbnail. This keeps the switcher fast, light, and free
  of the DWM-compositing complexity that live previews require.
- **Elevated foreground windows** fall back to the system switcher (see
  "Why no admin is needed" above) — an intentional Windows security boundary.
- Window icons load **asynchronously** (off the keyboard-hook hot path), so on a
  machine with many windows the list appears instantly and icons fade in a
  moment later rather than blocking the first Alt+Tab.

## License

MIT — see `LICENSE` if present, otherwise treat as MIT.
