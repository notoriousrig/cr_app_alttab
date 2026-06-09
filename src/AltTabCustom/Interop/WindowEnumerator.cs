using System.Diagnostics;
using System.Text;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Interop;

/// <summary>
/// Enumerates the top-level windows that should appear in an Alt+Tab list.
/// The visibility rules mirror the well-known "should this window show in the
/// Alt+Tab list" algorithm (Raymond Chen), plus a DWM-cloaking check so we
/// skip UWP windows parked on other virtual desktops.
/// </summary>
internal static class WindowEnumerator
{
    public static List<WindowInfo> EnumerateAltTabWindows(bool loadIcons = true)
    {
        var results = new List<WindowInfo>();
        IntPtr shell = GetShellWindow();

        EnumWindows((hWnd, _) =>
        {
            if (hWnd == shell) return true;
            if (!IsAltTabWindow(hWnd)) return true;

            string title = GetTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title)) return true;

            GetWindowThreadProcessId(hWnd, out uint pid);
            string procName = TryGetProcessName(pid);
            bool minimized = IsIconic(hWnd);

            var info = new WindowInfo
            {
                Handle = hWnd,
                Title = title,
                ProcessName = procName,
                IsMinimized = minimized,
            };

            if (loadIcons)
            {
                info.Icon = IconHelper.GetWindowIcon(hWnd, pid);
            }

            results.Add(info);
            return true;
        }, IntPtr.Zero);

        // EnumWindows returns windows in Z-order (topmost first), which is the
        // natural MRU-ish ordering for an Alt+Tab list.
        return results;
    }

    private static bool IsAltTabWindow(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd)) return false;

        // Walk to the root owner; only the root owner's last active popup is shown.
        IntPtr root = GetAncestor(hWnd, GA_ROOTOWNER);
        if (GetLastActiveVisiblePopup(root) != hWnd) return false;

        long exStyle = GetWindowLongPtrSafe(hWnd, GWL_EXSTYLE);

        // App windows are always candidates; tool windows never are.
        if ((exStyle & WS_EX_APPWINDOW) != 0) return !IsCloaked(hWnd);
        if ((exStyle & WS_EX_TOOLWINDOW) != 0) return false;
        if ((exStyle & WS_EX_NOACTIVATE) != 0) return false;

        if (IsCloaked(hWnd)) return false;

        return true;
    }

    private static IntPtr GetLastActiveVisiblePopup(IntPtr root)
    {
        // Find the last visible, non-owned popup of the root window.
        IntPtr hwndWalk = IntPtr.Zero;
        IntPtr hwndTry = root;
        while (hwndTry != hwndWalk)
        {
            hwndWalk = hwndTry;
            hwndTry = GetLastActivePopup(hwndWalk);
            if (IsWindowVisible(hwndTry)) break;
        }
        return hwndWalk;
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        try
        {
            if (DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0)
                return cloaked != 0;
        }
        catch
        {
            // dwmapi should always be present on Win10/11; ignore failures.
        }
        return false;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        int len = GetWindowTextLength(hWnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string TryGetProcessName(uint pid)
    {
        if (pid == 0) return string.Empty;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
