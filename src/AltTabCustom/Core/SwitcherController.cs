using System.Windows;
using AltTabCustom.Interop;
using AltTabCustom.Settings;
using AltTabCustom.UI;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Core;

/// <summary>
/// The brain of the app. Owns the keyboard hook and the switcher window, and
/// implements the Alt+Tab state machine:
///   * Alt+Tab (down)           -> open / advance forward
///   * Alt+Shift+Tab (down)     -> open / advance backward
///   * arrows while open        -> navigate
///   * Enter while open         -> commit
///   * Esc while open           -> cancel
///   * Alt released while open  -> commit
/// Runs entirely on the WPF UI/dispatcher thread (where the hook is installed).
/// </summary>
internal sealed class SwitcherController : IDisposable
{
    private readonly KeyboardHook _hook = new();
    private readonly SwitcherWindow _switcher = new();
    private AppSettings _settings;

    private bool _isOpen;
    private IntPtr _foregroundAtOpen;

    public SwitcherController(AppSettings settings)
    {
        _settings = settings;
        _switcher.ItemActivated += OnItemActivated;
        _hook.KeyIntercepted = OnKey;
    }

    public void Start() => _hook.Install();

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    // ---- Hook handling (returns true to swallow the key) ----
    private bool OnKey(KeyEventArgs e)
    {
        if (_isOpen)
            return HandleWhileOpen(e);

        // Not open yet: only Alt+Tab (down) starts a switch.
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

            case VK_RETURN:
                if (e.IsKeyDown) Commit();
                return true;

            case VK_ESCAPE:
                if (e.IsKeyDown) Cancel();
                return true;

            case VK_MENU:
            case VK_LMENU:
            case VK_RMENU:
                // Alt released -> commit. Let the Alt key-up flow to the system.
                if (!e.IsKeyDown) Commit();
                return false;

            default:
                return false;
        }
    }

    // ---- State transitions ----
    private void Open(bool backward)
    {
        _foregroundAtOpen = GetForegroundWindow();

        var windows = WindowEnumerator.EnumerateAltTabWindows(loadIcons: true);
        if (windows.Count == 0) return;

        // Forward: pre-select the previous window (index 1) like classic Alt+Tab.
        // Backward: pre-select the last window.
        int initial = backward ? windows.Count - 1 : Math.Min(1, windows.Count - 1);

        _isOpen = true;

        // Break the lone-Alt menu sequence so releasing Alt won't pop a menu bar.
        if (_settings.PreventAltMenu)
            InjectDummyKey();

        _switcher.ShowSwitcher(windows, initial, _settings, _foregroundAtOpen);
    }

    private void Navigate(int delta) => _switcher.MoveSelection(delta);

    private void Commit()
    {
        if (!_isOpen) return;
        var target = _switcher.SelectedWindow;
        Close();
        if (target is not null)
            WindowActivator.Activate(target.Handle);
    }

    private void Cancel() => Close();

    private void Close()
    {
        _isOpen = false;
        _switcher.HideSwitcher();
    }

    private void OnItemActivated(WindowInfo window)
    {
        // Mouse click in the overlay.
        Close();
        WindowActivator.Activate(window.Handle);
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
        if (Application.Current is not null)
            _switcher.Close();
    }
}
