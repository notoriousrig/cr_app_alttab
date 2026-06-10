using System.Text;
using System.Windows;
using System.Windows.Threading;
using AltTabCustom.Interop;
using AltTabCustom.Settings;
using AltTabCustom.UI;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Core;

/// <summary>
/// The brain of the app. Owns the keyboard hook, the MRU focus tracker, and the
/// switcher window, and implements the Alt+Tab state machine.
///
/// Tap-vs-hold: after Alt+Tab the controller waits <see cref="AppSettings.ShowDelayMs"/>
/// before showing the overlay. Releasing Alt within that window is a "tap" that
/// switches straight to the previous (MRU) window with no UI; holding longer (or
/// pressing another key) opens the overlay for browsing.
///
/// While open: arrows/Tab navigate, Home/End jump, digits 1-9 quick-select (when
/// no filter is active), letters/space filter, Backspace edits the filter,
/// Delete closes the highlighted window, Enter or releasing Alt commits, Esc
/// cancels. Runs entirely on the WPF UI/dispatcher thread.
/// </summary>
internal sealed class SwitcherController : IDisposable
{
    private enum State { Closed, Pending, Open }

    private readonly KeyboardHook _hook = new();
    private readonly SwitcherWindow _switcher = new();
    private readonly MruTracker _mru = new();
    private readonly DispatcherTimer _showTimer;
    private readonly DispatcherTimer _watchdog;
    private long _lastReinstall;
    private AppSettings _settings;

    private State _state = State.Closed;
    private IntPtr _foregroundAtOpen;
    private List<WindowInfo> _allWindows = new();
    private int _pendingIndex;
    private readonly StringBuilder _filter = new();

    // When set, the list is restricted to windows of this process name (Right
    // arrow sets it from the selected window; Left arrow clears it).
    private string? _processFilter;

    public SwitcherController(AppSettings settings)
    {
        _settings = settings;
        _switcher.ItemActivated += OnItemActivated;
        _switcher.ItemCloseRequested += OnItemCloseRequested;
        _hook.KeyIntercepted = OnKey;
        _hook.OnError = ex => Logger.Error("Keyboard hook callback threw", ex);

        _showTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _showTimer.Tick += (_, _) =>
        {
            _showTimer.Stop();
            if (_state == State.Pending) PromoteToOpen(_pendingIndex);
        };

        _watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _watchdog.Tick += (_, _) => WatchdogTick();
    }

    public void Start()
    {
        _mru.Start();
        _hook.Install();
        _lastReinstall = Environment.TickCount64;
        _watchdog.Start();
        Logger.Info("Keyboard hook and MRU tracker started.");
    }

    // Periodically recover the keyboard hook in case Windows silently dropped it
    // (which it can do after a slow callback). Only acts while idle so it never
    // interrupts an in-progress switch.
    private const long PeriodicReinstallMs = 30_000;

    private void WatchdogTick()
    {
        if (_state != State.Closed) return;

        bool slow = _hook.ConsumeSlowFlag();
        bool due = Environment.TickCount64 - _lastReinstall >= PeriodicReinstallMs;
        if (!slow && !due) return;

        try
        {
            _hook.Reinstall();
            _lastReinstall = Environment.TickCount64;
            if (slow) Logger.Info("Re-installed keyboard hook after a slow callback.");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to re-install keyboard hook in watchdog", ex);
        }
    }

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        // Forget cached custom icons so edited rules / replaced files take effect.
        Interop.CustomIconResolver.Clear();
    }

    // ---- Hook handling (returns true to swallow the key) ----
    private bool OnKey(KeyEventArgs e)
    {
        if (_state == State.Closed)
        {
            if (e.VkCode == VK_TAB && e.IsKeyDown && e.AltDown)
            {
                Begin(backward: e.ShiftDown);
                return true; // swallow the system Alt+Tab
            }
            return false;
        }

        return HandleActive(e);
    }

    private bool HandleActive(KeyEventArgs e)
    {
        switch (e.VkCode)
        {
            case VK_TAB:
                if (e.IsKeyDown) { EnsureOpen(); Navigate(e.ShiftDown ? -1 : +1); }
                return true;

            case VK_UP:
                if (e.IsKeyDown) { EnsureOpen(); Navigate(-1); }
                return true;

            case VK_DOWN:
                if (e.IsKeyDown) { EnsureOpen(); Navigate(+1); }
                return true;

            case VK_RIGHT:
                // Drill into just the selected window's application.
                if (e.IsKeyDown) { EnsureOpen(); FilterToSelectedProcess(); }
                return true;

            case VK_LEFT:
                // While drilled into an app, pop back out first; otherwise close
                // the selected window.
                if (e.IsKeyDown)
                {
                    EnsureOpen();
                    if (_processFilter is not null) ClearProcessFilter();
                    else CloseSelected();
                }
                return true;

            case VK_HOME:
                if (e.IsKeyDown) { EnsureOpen(); _switcher.SelectFirst(); }
                return true;

            case VK_END:
                if (e.IsKeyDown) { EnsureOpen(); _switcher.SelectLast(); }
                return true;

            case VK_RETURN:
                if (e.IsKeyDown) Commit();
                return true;

            case VK_ESCAPE:
                if (e.IsKeyDown) Cancel();
                return true;

            case VK_BACK:
                if (e.IsKeyDown) { EnsureOpen(); Backspace(); }
                return true;

            case VK_DELETE:
                if (e.IsKeyDown) { EnsureOpen(); CloseSelected(); }
                return true;

            case VK_MENU:
            case VK_LMENU:
            case VK_RMENU:
                // Alt released -> commit. Let the Alt key-up flow to the system.
                if (!e.IsKeyDown) Commit();
                return false;

            default:
                if (e.IsKeyDown)
                {
                    if (TryDigit(e.VkCode, out int digit)) { HandleDigit(digit); return true; }
                    if (TryMapChar(e.VkCode, out char c)) { EnsureOpen(); AppendFilter(c); return true; }
                }
                return false;
        }
    }

    private static bool TryDigit(int vk, out int digit)
    {
        if (vk >= VK_0 && vk <= VK_9) { digit = vk - VK_0; return true; }
        if (vk >= VK_NUMPAD0 && vk <= VK_NUMPAD9) { digit = vk - VK_NUMPAD0; return true; }
        digit = 0;
        return false;
    }

    private static bool TryMapChar(int vk, out char c)
    {
        if (vk >= VK_A && vk <= VK_Z) { c = (char)('a' + (vk - VK_A)); return true; }
        if (vk == VK_SPACE) { c = ' '; return true; }
        c = '\0';
        return false;
    }

    // ---- State transitions ----
    private void Begin(bool backward)
    {
        try
        {
            _foregroundAtOpen = GetForegroundWindow();
            _filter.Clear();
            _processFilter = null;

            // Skip icon loading here — it's the slow part and would run inside the
            // keyboard-hook callback. The overlay loads icons asynchronously.
            var windows = WindowEnumerator.EnumerateAltTabWindows(loadIcons: false);
            windows = _mru.Order(windows); // genuine most-recently-used order
            _allWindows = windows;

            if (_allWindows.Count == 0)
            {
                Logger.Info("Alt+Tab pressed but no switchable windows were found.");
                return; // stay Closed
            }

            // Forward: previous window (index 1); Backward: last window.
            _pendingIndex = backward ? _allWindows.Count - 1 : Math.Min(1, _allWindows.Count - 1);

            // Break the lone-Alt menu sequence so releasing Alt won't pop a menu bar.
            if (_settings.PreventAltMenu)
                InjectDummyKey();

            if (_settings.ShowDelayMs <= 0)
            {
                PromoteToOpen(_pendingIndex);
                return;
            }

            // Defer showing the overlay so a quick tap can switch with no UI.
            _state = State.Pending;
            _showTimer.Interval = TimeSpan.FromMilliseconds(_settings.ShowDelayMs);
            _showTimer.Start();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to begin a switch", ex);
            Close();
        }
    }

    private void EnsureOpen()
    {
        if (_state == State.Pending) PromoteToOpen(_pendingIndex);
    }

    private void PromoteToOpen(int selectedIndex)
    {
        _showTimer.Stop();
        _state = State.Open;
        double effectiveWidth = DisplayMetrics.EffectiveWidth(_foregroundAtOpen);
        var profile = _settings.ResolveProfile(effectiveWidth);
        _switcher.ShowSwitcher(_allWindows, selectedIndex, profile, _settings.ClickToActivate,
            _foregroundAtOpen, _settings.AcrylicBackground, _settings.IconRules);
    }

    private void Navigate(int delta) => _switcher.MoveSelection(delta);

    private void HandleDigit(int d)
    {
        EnsureOpen();
        // 1-9 jump to that row when no filter is being typed; otherwise the
        // digit extends the filter.
        if (d >= 1 && _filter.Length == 0)
            _switcher.SelectIndex(d - 1);
        else
            AppendFilter((char)('0' + d));
    }

    private void AppendFilter(char c)
    {
        _filter.Append(c);
        ApplyFilter();
    }

    private void Backspace()
    {
        if (_filter.Length == 0) return;
        _filter.Length--;
        ApplyFilter();
    }

    // Restrict the list to the selected window's application.
    private void FilterToSelectedProcess()
    {
        var sel = _switcher.SelectedWindow;
        if (sel is null || string.IsNullOrEmpty(sel.ProcessName)) return;
        _processFilter = sel.ProcessName;
        ApplyFilter(keepSelection: sel.Handle);
    }

    // Back to the full list.
    private void ClearProcessFilter()
    {
        if (_processFilter is null) return;
        IntPtr? keep = _switcher.SelectedWindow?.Handle;
        _processFilter = null;
        ApplyFilter(keepSelection: keep);
    }

    private void ApplyFilter(IntPtr? keepSelection = null)
    {
        string f = _filter.ToString();
        List<WindowInfo> filtered = Filtered(f);

        int selectedIndex = 0;
        if (keepSelection is IntPtr h)
        {
            int idx = filtered.FindIndex(w => w.Handle == h);
            if (idx >= 0) selectedIndex = idx;
        }

        _switcher.UpdateItems(filtered, selectedIndex, searchText: f, statusLabel: StatusLabel(f));
    }

    private List<WindowInfo> Filtered(string f) => WindowFilter.Apply(_allWindows, _processFilter, f);

    // The text shown in the overlay's filter bar; null falls back to the default
    // typed-search display.
    private string? StatusLabel(string f)
    {
        if (_processFilter is null) return null;
        string label = "⊞  " + _processFilter;
        if (f.Length > 0) label += "    🔍  " + f;
        return label;
    }

    private void Commit()
    {
        WindowInfo? target = _state switch
        {
            State.Pending => _pendingIndex >= 0 && _pendingIndex < _allWindows.Count ? _allWindows[_pendingIndex] : null,
            State.Open => _switcher.SelectedWindow,
            _ => null,
        };

        Close();
        if (target is not null)
            ActivateSafe(target.Handle);
    }

    private void Cancel() => Close();

    private void Close()
    {
        _showTimer.Stop();
        bool wasOpen = _state == State.Open;
        _state = State.Closed;
        _filter.Clear();
        _processFilter = null;
        if (wasOpen) _switcher.HideSwitcher();
    }

    private void CloseSelected()
    {
        if (_state != State.Open) return;
        var target = _switcher.SelectedWindow;
        if (target is not null)
            RemoveAndClose(target);
    }

    private void OnItemActivated(WindowInfo window)
    {
        Close();
        ActivateSafe(window.Handle);
    }

    private void OnItemCloseRequested(WindowInfo window)
    {
        if (_state == State.Open) RemoveAndClose(window);
    }

    private void RemoveAndClose(WindowInfo window)
    {
        try
        {
            PostMessage(window.Handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to post WM_CLOSE", ex);
        }

        _allWindows.RemoveAll(w => w.Handle == window.Handle);
        if (_allWindows.Count == 0)
        {
            Close();
            return;
        }

        // If the app we were drilled into has no windows left, pop the filter.
        if (_processFilter is not null &&
            !_allWindows.Any(w => string.Equals(w.ProcessName, _processFilter, StringComparison.OrdinalIgnoreCase)))
        {
            _processFilter = null;
        }

        ApplyFilter();
    }

    private static void ActivateSafe(IntPtr handle)
    {
        try
        {
            WindowActivator.Activate(handle);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to activate window", ex);
        }
    }

    /// <summary>
    /// Send a harmless Ctrl tap so the system records an intervening key during
    /// the Alt hold; otherwise releasing Alt could activate the foreground
    /// window's menu bar. The injected flag makes our own hook ignore it.
    /// </summary>
    private static void InjectDummyKey()
    {
        var inputs = new INPUT[2];
        inputs[0] = new INPUT { type = INPUT_KEYBOARD, u = { ki = new KEYBDINPUT { wVk = (ushort)VK_CONTROL } } };
        inputs[1] = new INPUT { type = INPUT_KEYBOARD, u = { ki = new KEYBDINPUT { wVk = (ushort)VK_CONTROL, dwFlags = KEYEVENTF_KEYUP } } };
        SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    public void Dispose()
    {
        _showTimer.Stop();
        _watchdog.Stop();
        _hook.Dispose();
        _mru.Dispose();
        if (Application.Current is not null)
            _switcher.Close();
    }
}
