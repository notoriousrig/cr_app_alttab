using System.Text;
using System.Windows;
using AltTabCustom.Interop;
using AltTabCustom.Settings;
using AltTabCustom.UI;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Core;

/// <summary>
/// The brain of the app. Owns the keyboard hook, the MRU focus tracker, and the
/// switcher window, and implements the Alt+Tab state machine:
///   * Alt+Tab (down)           -> open / advance forward
///   * Alt+Shift+Tab (down)     -> open / advance backward
///   * arrows while open        -> navigate
///   * letters/digits/space     -> type-to-filter the list
///   * Backspace                -> delete a filter character
///   * Delete                   -> close the highlighted window (no switch)
///   * Enter while open         -> commit
///   * Esc while open           -> cancel
///   * Alt released while open  -> commit
/// Runs entirely on the WPF UI/dispatcher thread (where the hook is installed).
/// </summary>
internal sealed class SwitcherController : IDisposable
{
    private readonly KeyboardHook _hook = new();
    private readonly SwitcherWindow _switcher = new();
    private readonly MruTracker _mru = new();
    private AppSettings _settings;

    private bool _isOpen;
    private IntPtr _foregroundAtOpen;
    private List<WindowInfo> _allWindows = new();
    private readonly StringBuilder _filter = new();

    public SwitcherController(AppSettings settings)
    {
        _settings = settings;
        _switcher.ItemActivated += OnItemActivated;
        _switcher.ItemCloseRequested += OnItemCloseRequested;
        _hook.KeyIntercepted = OnKey;
        _hook.OnError = ex => Logger.Error("Keyboard hook callback threw", ex);
    }

    public void Start()
    {
        _mru.Start();
        _hook.Install();
        Logger.Info("Keyboard hook and MRU tracker started.");
    }

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    // ---- Hook handling (returns true to swallow the key) ----
    private bool OnKey(KeyEventArgs e)
    {
        if (_isOpen)
            return HandleWhileOpen(e);

        if (e.VkCode == VK_TAB && e.IsKeyDown && e.AltDown)
        {
            Open(backward: e.ShiftDown);
            return true; // swallow the system Alt+Tab
        }
        return false;
    }

    private bool HandleWhileOpen(KeyEventArgs e)
    {
        switch (e.VkCode)
        {
            case VK_TAB:
                if (e.IsKeyDown) Navigate(e.ShiftDown ? -1 : +1);
                return true;

            case VK_LEFT:
            case VK_UP:
                if (e.IsKeyDown) Navigate(-1);
                return true;

            case VK_RIGHT:
            case VK_DOWN:
                if (e.IsKeyDown) Navigate(+1);
                return true;

            case VK_HOME:
                if (e.IsKeyDown) _switcher.SelectFirst();
                return true;

            case VK_END:
                if (e.IsKeyDown) _switcher.SelectLast();
                return true;

            case VK_RETURN:
                if (e.IsKeyDown) Commit();
                return true;

            case VK_ESCAPE:
                if (e.IsKeyDown) Cancel();
                return true;

            case VK_BACK:
                if (e.IsKeyDown) Backspace();
                return true;

            case VK_DELETE:
                if (e.IsKeyDown) CloseSelected();
                return true;

            case VK_MENU:
            case VK_LMENU:
            case VK_RMENU:
                // Alt released -> commit. Let the Alt key-up flow to the system.
                if (!e.IsKeyDown) Commit();
                return false;

            default:
                if (e.IsKeyDown && TryMapChar(e.VkCode, out char c))
                {
                    AppendFilter(c);
                    return true;
                }
                return false;
        }
    }

    private static bool TryMapChar(int vk, out char c)
    {
        if (vk >= VK_A && vk <= VK_Z) { c = (char)('a' + (vk - VK_A)); return true; }
        if (vk >= VK_0 && vk <= VK_9) { c = (char)('0' + (vk - VK_0)); return true; }
        if (vk >= VK_NUMPAD0 && vk <= VK_NUMPAD9) { c = (char)('0' + (vk - VK_NUMPAD0)); return true; }
        if (vk == VK_SPACE) { c = ' '; return true; }
        c = '\0';
        return false;
    }

    // ---- State transitions ----
    private void Open(bool backward)
    {
        try
        {
            _foregroundAtOpen = GetForegroundWindow();
            _filter.Clear();

            var windows = WindowEnumerator.EnumerateAltTabWindows(loadIcons: true);
            windows = _mru.Order(windows); // genuine most-recently-used order
            _allWindows = windows;

            if (_allWindows.Count == 0)
            {
                Logger.Info("Alt+Tab pressed but no switchable windows were found.");
                return;
            }

            // Forward: pre-select the previous window (index 1) like classic Alt+Tab.
            // Backward: pre-select the last window.
            int initial = backward ? _allWindows.Count - 1 : Math.Min(1, _allWindows.Count - 1);

            _isOpen = true;

            // Break the lone-Alt menu sequence so releasing Alt won't pop a menu bar.
            if (_settings.PreventAltMenu)
                InjectDummyKey();

            _switcher.ShowSwitcher(_allWindows, initial, _settings, _foregroundAtOpen);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to open the switcher", ex);
            _isOpen = false;
        }
    }

    private void Navigate(int delta) => _switcher.MoveSelection(delta);

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

    private void ApplyFilter()
    {
        string f = _filter.ToString();
        List<WindowInfo> filtered = string.IsNullOrEmpty(f)
            ? _allWindows
            : _allWindows.Where(w => Match(w.Title, f) || Match(w.ProcessName, f)).ToList();

        _switcher.UpdateItems(filtered, selectedIndex: 0, searchText: f);
    }

    private static bool Match(string haystack, string needle)
        => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private void Commit()
    {
        if (!_isOpen) return;
        var target = _switcher.SelectedWindow;
        Close();
        if (target is not null)
            ActivateSafe(target.Handle);
    }

    private void Cancel() => Close();

    private void Close()
    {
        _isOpen = false;
        _filter.Clear();
        _switcher.HideSwitcher();
    }

    private void CloseSelected()
    {
        if (!_isOpen) return;
        var target = _switcher.SelectedWindow;
        if (target is not null)
            RemoveAndClose(target);
    }

    private void OnItemActivated(WindowInfo window)
    {
        // Left mouse click in the overlay.
        Close();
        ActivateSafe(window.Handle);
    }

    private void OnItemCloseRequested(WindowInfo window)
    {
        // Middle mouse click in the overlay.
        if (_isOpen) RemoveAndClose(window);
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
            Close();
        else
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
        _hook.Dispose();
        _mru.Dispose();
        if (Application.Current is not null)
            _switcher.Close();
    }
}
