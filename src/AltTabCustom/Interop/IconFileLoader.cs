using System.IO;

namespace AltTabCustom.Interop;

/// <summary>
/// Loads an icon file (.ico / .png / other GDI+ readable image) into native
/// <c>HICON</c> handles suitable for <c>WM_SETICON</c>. Two sizes are produced —
/// a "big" handle for the title bar / Alt+Tab and a "small" handle for the
/// taskbar button. The returned handles are <b>owned by the caller</b> and must
/// be released with <see cref="NativeMethods.DestroyIcon"/>.
/// </summary>
internal static class IconFileLoader
{
    public const int BigSize = 32;
    public const int SmallSize = 16;

    /// <summary>
    /// Load <paramref name="path"/> into (big, small) HICONs. Returns
    /// <c>(Zero, Zero)</c> if the file is missing or cannot be decoded; a partial
    /// result (one handle Zero) is possible but unusual.
    /// </summary>
    public static (IntPtr big, IntPtr small) Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return (IntPtr.Zero, IntPtr.Zero);

        try
        {
            return (MakeHicon(path, BigSize), MakeHicon(path, SmallSize));
        }
        catch
        {
            // Unloadable/corrupt image — caller treats this as "no override".
            return (IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static IntPtr MakeHicon(string path, int size)
    {
        // For .ico, let GDI+ pick the frame closest to the requested size; for
        // raster formats, scale the decoded bitmap. GetHicon() returns a handle
        // we own (the caller DestroyIcon()s it).
        if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            using var ico = new System.Drawing.Icon(path, size, size);
            using var bmp = ico.ToBitmap();
            return bmp.GetHicon();
        }

        using var src = new System.Drawing.Bitmap(path);
        using var scaled = new System.Drawing.Bitmap(src, size, size);
        return scaled.GetHicon();
    }
}
