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
    private readonly List<IntPtr> _order = new();   // most-recent first
    private readonly object _gate = new();
    private readonly WinEventDelegate _proc;        // kept alive to avoid GC
    private IntPtr _hook = IntPtr.Zero;

    private const int MaxTracked = 256;

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
        if (fg != IntPtr.Zero) Touch(fg);
    }

    private void OnForeground(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // Only care about top-level window foreground changes.
        if (idObject != OBJID_WINDOW || hwnd == IntPtr.Zero) return;
        Touch(hwnd);
    }

    private void Touch(IntPtr hwnd)
    {
        lock (_gate)
        {
            _order.Remove(hwnd);
            _order.Insert(0, hwnd);
            if (_order.Count > MaxTracked)
                _order.RemoveRange(MaxTracked, _order.Count - MaxTracked);
        }
    }

    /// <summary>
    /// Returns the windows ordered most-recently-used first. Windows we have
    /// never seen focused keep their incoming (Z-order) position, after the
    /// tracked ones, because the sort is stable.
    /// </summary>
    public List<WindowInfo> Order(List<WindowInfo> windows)
    {
        lock (_gate)
        {
            return windows
                .OrderBy(w =>
                {
                    int i = _order.IndexOf(w.Handle);
                    return i < 0 ? int.MaxValue : i;
                })
                .ToList();
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
