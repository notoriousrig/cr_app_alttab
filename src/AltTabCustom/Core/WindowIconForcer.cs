using System.Windows.Threading;
using AltTabCustom.Interop;
using AltTabCustom.Settings;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.Core;

/// <summary>
/// Applies the user's <see cref="IconRule"/> overrides to the <b>real</b> Windows
/// windows (taskbar button + title bar + Alt+Tab) via <c>WM_SETICON</c>, not just
/// inside our own overlay. This needs no administrator rights: <c>WM_SETICON</c>
/// works against any same-or-lower integrity window in the session.
///
/// The override is not persistent — an app that resets its own icon, or that is
/// restarted, drops it — so we poll on a timer and re-apply. We remember each
/// window's original icons and put them back when forcing is turned off or the
/// app exits, so windows don't keep a dangling handle. (A hard crash skips the
/// restore; the affected window fixes itself the next time it sets its icon.)
///
/// All work runs on the WPF dispatcher thread (the app already pumps messages
/// there), which keeps the bookkeeping dictionaries lock-free. The cross-thread
/// <c>SendMessage</c>s use a short timeout so a hung target can't stall us.
/// </summary>
internal sealed class WindowIconForcer : IDisposable
{
    private const int RefreshMs = 1500;
    private const uint SendTimeoutMs = 200;

    private readonly DispatcherTimer _timer;

    // Rules that are enabled and actually point at an icon file.
    private IReadOnlyList<IconRule> _rules = Array.Empty<IconRule>();
    private bool _enabled;

    // Icon-file path -> the HICONs we created for it (owned; DestroyIcon on clear).
    private readonly Dictionary<string, (IntPtr big, IntPtr small)> _iconCache =
        new(StringComparer.OrdinalIgnoreCase);

    // Windows we've overridden -> the icons they had before, to restore later.
    private readonly Dictionary<IntPtr, Original> _applied = new();

    private readonly record struct Original(IntPtr Big, IntPtr Small);

    public WindowIconForcer(AppSettings settings)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(RefreshMs),
        };
        _timer.Tick += (_, _) => Apply();
        UpdateSettings(settings);
    }

    /// <summary>Re-read settings: start/stop the watcher and refresh the icon cache.</summary>
    public void UpdateSettings(AppSettings settings)
    {
        _enabled = settings.ForceWindowIcons;
        _rules = (settings.IconRules ?? new List<IconRule>())
            .Where(r => r.Enabled && !string.IsNullOrWhiteSpace(r.IconPath))
            .ToList();

        // Icon files may have changed on disk; drop cached handles so they reload.
        ClearIconCache();

        if (_enabled)
        {
            Apply();          // take effect immediately, don't wait a tick
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            RestoreAll();     // hand every window its original icon back
        }
    }

    /// <summary>One sweep: force matching windows, restore ones that stopped matching.</summary>
    public void Apply()
    {
        if (!_enabled) return;

        List<WindowInfo> windows;
        try
        {
            windows = WindowEnumerator.EnumerateAltTabWindows(loadIcons: false);
        }
        catch (Exception ex)
        {
            Logger.Error("Icon forcer: window enumeration failed", ex);
            return;
        }

        var live = new HashSet<IntPtr>();
        foreach (var w in windows)
        {
            live.Add(w.Handle);
            var rule = _rules.FirstOrDefault(r => r.Matches(w.Title, w.ProcessName));
            if (rule is not null)
            {
                ApplyRule(w.Handle, rule);
            }
            else if (_applied.TryGetValue(w.Handle, out var orig))
            {
                // It used to match (e.g. the title changed) — undo our override.
                RestoreOne(w.Handle, orig);
                _applied.Remove(w.Handle);
            }
        }

        // Forget windows that have since closed (nothing left to restore).
        if (_applied.Count > 0)
        {
            foreach (var gone in _applied.Keys.Where(h => !live.Contains(h)).ToList())
                _applied.Remove(gone);
        }
    }

    private void ApplyRule(IntPtr hwnd, IconRule rule)
    {
        var (big, small) = GetIcons(rule.IconPath);
        if (big == IntPtr.Zero && small == IntPtr.Zero) return; // bad/missing file

        IntPtr bigToSet = big != IntPtr.Zero ? big : small;
        IntPtr smallToSet = small != IntPtr.Zero ? small : big;

        bool firstTouch = !_applied.ContainsKey(hwnd);

        // Already showing our small icon? Then it's untouched since last sweep —
        // skip the re-send so the taskbar doesn't flicker.
        if (!firstTouch && QueryIcon(hwnd, ICON_SMALL) == smallToSet) return;

        if (firstTouch)
            _applied[hwnd] = new Original(QueryIcon(hwnd, ICON_BIG), QueryIcon(hwnd, ICON_SMALL));

        SetIcon(hwnd, ICON_BIG, bigToSet);
        SetIcon(hwnd, ICON_SMALL, smallToSet);
        SetIcon(hwnd, ICON_SMALL2, smallToSet);
    }

    private (IntPtr big, IntPtr small) GetIcons(string path)
    {
        if (_iconCache.TryGetValue(path, out var cached)) return cached;
        var loaded = IconFileLoader.Load(path);
        _iconCache[path] = loaded;
        return loaded;
    }

    private static IntPtr QueryIcon(IntPtr hwnd, int which)
        => SendMessageTimeout(hwnd, WM_GETICON, new IntPtr(which), IntPtr.Zero,
               SMTO_ABORTIFHUNG, SendTimeoutMs, out IntPtr result) != IntPtr.Zero
               ? result : IntPtr.Zero;

    private static void SetIcon(IntPtr hwnd, int which, IntPtr hIcon)
        => SendMessageTimeout(hwnd, WM_SETICON, new IntPtr(which), hIcon,
               SMTO_ABORTIFHUNG, SendTimeoutMs, out _);

    private void RestoreAll()
    {
        foreach (var (hwnd, orig) in _applied) RestoreOne(hwnd, orig);
        _applied.Clear();
    }

    private static void RestoreOne(IntPtr hwnd, Original orig)
    {
        SetIcon(hwnd, ICON_BIG, orig.Big);
        SetIcon(hwnd, ICON_SMALL, orig.Small);
        SetIcon(hwnd, ICON_SMALL2, orig.Small);
    }

    private void ClearIconCache()
    {
        foreach (var (_, icons) in _iconCache)
        {
            if (icons.big != IntPtr.Zero) DestroyIcon(icons.big);
            if (icons.small != IntPtr.Zero) DestroyIcon(icons.small);
        }
        _iconCache.Clear();
    }

    public void Dispose()
    {
        _timer.Stop();
        RestoreAll();
        ClearIconCache();
    }
}
