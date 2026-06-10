using System.IO;
using System.Windows.Media.Imaging;
using AltTabCustom.Settings;

namespace AltTabCustom.Interop;

/// <summary>
/// Resolves user-defined <see cref="IconRule"/> overrides to a WPF image,
/// caching each loaded file so repeated lookups stay off disk. Safe to call from
/// the icon worker threads: loaded bitmaps are frozen before being returned.
/// </summary>
internal static class CustomIconResolver
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, BitmapSource?> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// First rule that matches the window wins; returns its icon, or null if no
    /// rule matches or the file can't be loaded (caller then falls back to the OS).
    /// </summary>
    public static BitmapSource? Resolve(IReadOnlyList<IconRule>? rules, string? title, string? processName)
    {
        if (rules is null) return null;
        foreach (var rule in rules)
        {
            if (rule.Matches(title, processName))
                return Load(rule.IconPath);
        }
        return null;
    }

    /// <summary>Drop cached images (call when settings change so edits take effect).</summary>
    public static void Clear()
    {
        lock (Gate) Cache.Clear();
    }

    private static BitmapSource? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        lock (Gate)
        {
            if (Cache.TryGetValue(path, out var cached)) return cached;
        }

        BitmapSource? result = null;
        try
        {
            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // fully load now so the file isn't locked
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                result = bmp;
            }
        }
        catch
        {
            // Unloadable/corrupt image — cache the miss so we don't retry every time.
            result = null;
        }

        lock (Gate) Cache[path] = result;
        return result;
    }
}
