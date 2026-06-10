using static AltTabCustom.Interop.NativeMethods;
using Forms = System.Windows.Forms;

namespace AltTabCustom.Core;

/// <summary>
/// Helpers for reasoning about the monitor a window lives on. We use the
/// <em>effective</em> (DPI-scaled) width so a high-DPI laptop panel isn't
/// mistaken for a large external monitor.
/// </summary>
internal static class DisplayMetrics
{
    /// <summary>
    /// Effective width, in device-independent pixels, of the monitor that the
    /// given window is on (falls back to the primary monitor).
    /// </summary>
    public static double EffectiveWidth(IntPtr windowOnMonitor)
    {
        Forms.Screen screen = windowOnMonitor != IntPtr.Zero
            ? Forms.Screen.FromHandle(windowOnMonitor)
            : Forms.Screen.PrimaryScreen!;

        double scale = DpiScaleFor(windowOnMonitor);
        return screen.Bounds.Width / scale;
    }

    private static double DpiScaleFor(IntPtr hwnd)
    {
        uint dpi = hwnd != IntPtr.Zero ? GetDpiForWindow(hwnd) : 0;
        if (dpi == 0) dpi = 96;
        return dpi / 96.0;
    }
}
