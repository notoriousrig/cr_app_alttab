using System.Runtime.InteropServices;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Interop;

public sealed class KeyEventArgs
{
    public required int VkCode { get; init; }
    public required bool IsKeyDown { get; init; }
    public required bool AltDown { get; init; }
    public required bool ShiftDown { get; init; }
    public required bool CtrlDown { get; init; }
}

/// <summary>
/// A global low-level keyboard hook (WH_KEYBOARD_LL). This is the key piece
/// that lets us intercept Alt+Tab <b>without administrator rights</b>:
/// RegisterHotKey cannot bind Alt+Tab (it is reserved by the system), but a
/// low-level hook can observe and swallow it from a normal-privilege process.
///
/// The hook must be installed on a thread that pumps messages — we install it
/// on the WPF UI/dispatcher thread, so the callback also runs there and may
/// touch the UI directly.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    // Return true from the handler to swallow the key (hide it from the system).
    public Func<KeyEventArgs, bool>? KeyIntercepted;

    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelKeyboardProc _proc;   // kept alive to avoid GC of the callback

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;
        IntPtr hMod = GetModuleHandle(null);
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hMod, 0);
        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException(
                $"Failed to install keyboard hook (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    /// <summary>Optional sink for exceptions thrown while handling a key.</summary>
    public Action<Exception>? OnError;

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                // Ignore input we synthesized ourselves so we never recurse.
                if ((data.flags & LLKHF_INJECTED) == 0)
                {
                    int msg = (int)wParam;
                    bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                    bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                    if (isDown || isUp)
                    {
                        var args = new KeyEventArgs
                        {
                            VkCode = (int)data.vkCode,
                            IsKeyDown = isDown,
                            AltDown = IsDown(VK_MENU),
                            ShiftDown = IsDown(VK_SHIFT),
                            CtrlDown = IsDown(VK_CONTROL),
                        };

                        if (KeyIntercepted?.Invoke(args) == true)
                            return new IntPtr(1); // swallow
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Never let an exception escape the hook callback — that can tear
            // down the hook and leave Alt+Tab broken. Report and carry on.
            OnError?.Invoke(ex);
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
