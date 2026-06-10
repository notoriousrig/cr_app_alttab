using AltTabCustom.Interop;

namespace AltTabCustom.Core;

/// <summary>
/// Pure list filtering for the switcher: an optional application (process name)
/// restriction combined with optional type-to-filter text. Kept free of UI so it
/// can be unit tested.
/// </summary>
internal static class WindowFilter
{
    public static List<WindowInfo> Apply(IReadOnlyList<WindowInfo> windows, string? processName, string? text)
    {
        IEnumerable<WindowInfo> q = windows;

        if (!string.IsNullOrEmpty(processName))
            q = q.Where(w => string.Equals(w.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(text))
            q = q.Where(w => Contains(w.Title, text) || Contains(w.ProcessName, text));

        return q.ToList();
    }

    private static bool Contains(string haystack, string needle)
        => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
