using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Interop;

/// <summary>
/// Resolves a window's icon as a WPF <see cref="System.Windows.Media.ImageSource"/>.
/// Tries WM_GETICON, then the window class icon, then the process executable.
/// All of this works without elevation.
/// </summary>
internal static class IconHelper
{
    public static BitmapSource? GetWindowIcon(IntPtr hWnd, uint processId)
    {
        IntPtr hIcon = TryGetIconHandle(hWnd);

        if (hIcon != IntPtr.Zero)
        {
            try
            {
                return ToBitmapSource(hIcon);
            }
            catch
            {
                // fall through to executable extraction
            }
        }

        // Fall back to the process executable's associated icon.
        return TryGetProcessIcon(processId);
    }

    private static IntPtr TryGetIconHandle(IntPtr hWnd)
    {
        IntPtr result;

        // WM_GETICON: big, then small2, then small.
        foreach (int which in new[] { ICON_BIG, ICON_SMALL2, ICON_SMALL })
        {
            if (SendMessageTimeout(hWnd, WM_GETICON, new IntPtr(which), IntPtr.Zero,
                    SMTO_ABORTIFHUNG, 200, out result) != IntPtr.Zero && result != IntPtr.Zero)
            {
                return result;
            }
        }

        // Class icon.
        IntPtr classIcon = GetClassLongPtrSafe(hWnd, GCLP_HICON);
        if (classIcon != IntPtr.Zero) return classIcon;

        classIcon = GetClassLongPtrSafe(hWnd, GCLP_HICONSM);
        return classIcon;
    }

    private static BitmapSource ToBitmapSource(IntPtr hIcon)
    {
        var src = Imaging.CreateBitmapSourceFromHIcon(
            hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        src.Freeze();
        return src;
    }

    private static BitmapSource? TryGetProcessIcon(uint processId)
    {
        if (processId == 0) return null;
        try
        {
            using var proc = Process.GetProcessById((int)processId);
            string? path = proc.MainModule?.FileName;
            if (string.IsNullOrEmpty(path)) return null;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null) return null;

            var src = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch
        {
            // Access to MainModule across architectures/sessions can throw; ignore.
            return null;
        }
    }
}
