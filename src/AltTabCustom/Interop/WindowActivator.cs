using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Interop;

/// <summary>
/// Brings a chosen window to the foreground. SetForegroundWindow alone is
/// throttled by Windows' foreground lock, so we use the AttachThreadInput
/// technique, which works reliably for a normal-privilege process.
/// </summary>
internal static class WindowActivator
{
    public static void Activate(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        if (IsIconic(hWnd))
            ShowWindow(hWnd, SW_RESTORE);

        IntPtr foreground = GetForegroundWindow();
        if (foreground == hWnd)
            return;

        uint targetThread = GetWindowThreadProcessId(hWnd, out _);
        uint foregroundThread = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, out _);
        uint currentThread = GetCurrentThreadId();

        bool attachedToForeground = false;
        bool attachedToTarget = false;

        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
                attachedToForeground = AttachThreadInput(currentThread, foregroundThread, true);
            if (targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread)
                attachedToTarget = AttachThreadInput(currentThread, targetThread, true);

            BringWindowToTop(hWnd);
            ShowWindow(hWnd, SW_SHOW);
            SetForegroundWindow(hWnd);
        }
        finally
        {
            if (attachedToForeground)
                AttachThreadInput(currentThread, foregroundThread, false);
            if (attachedToTarget)
                AttachThreadInput(currentThread, targetThread, false);
        }
    }
}
