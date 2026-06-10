using AltTabCustom.Interop;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Core;

/// <summary>
/// Tracks the most-recently-used order of windows by listening for foreground
/// changes via a WinEvent hook. This gives genuine MRU ordering (like the
/// system Alt+Tab) instead of relying on raw Z-order.
///
/// The hook is OUT_OF_CONTEXT, so its callback is delivered on the thread that
/// installed it (the WPF UI thread, which pumps messages). We also skip our own
/// process so the no-activate overlay never pollutes the history.
/// </summary>
internal sealed class MruTracker : IDisposable
{
    private readonly MruOrder _order = new();       // pure ordering logic
    private readonly WinEventDelegate _proc;        // kept alive to avoid GC
    private IntPtr _hook = IntPtr.Zero;

    public MruTracker()
    {
        _proc = OnForeground;
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
            _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        // Seed with whatever is currently focused.
        IntPtr fg = GetForegroundWindow();
        if (fg != IntPtr.Zero) _order.Touch(fg);
    }

    private void OnForeground(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Only care about top-level window foreground changes.
        if (idObject != OBJID_WINDOW || hwnd == IntPtr.Zero) return;
        _order.Touch(hwnd);
    }

    /// <summary>Order windows most-recently-used first (see <see cref="MruOrder"/>).</summary>
    public List<WindowInfo> Order(List<WindowInfo> windows) => _order.Order(windows);

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
