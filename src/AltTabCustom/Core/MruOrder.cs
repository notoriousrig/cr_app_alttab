using AltTabCustom.Interop;

namespace AltTabCustom.Core;

/// <summary>
/// Pure most-recently-used ordering over window handles. Kept free of any Win32
/// dependency so it can be unit tested. <see cref="MruTracker"/> feeds it from a
/// WinEvent hook.
/// </summary>
internal sealed class MruOrder
{
    private readonly List<IntPtr> _order = new(); // most-recent first
    private readonly object _gate = new();
    private const int MaxTracked = 256;

    /// <summary>Record that a window was just focused (moves it to the front).</summary>
    public void Touch(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        lock (_gate)
        {
            _order.Remove(handle);
            _order.Insert(0, handle);
            if (_order.Count > MaxTracked)
                _order.RemoveRange(MaxTracked, _order.Count - MaxTracked);
        }
    }

    /// <summary>
    /// Return the windows ordered most-recently-used first. Windows never seen
    /// keep their incoming (Z-order) position, after the tracked ones, because
    /// the sort is stable.
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
}
