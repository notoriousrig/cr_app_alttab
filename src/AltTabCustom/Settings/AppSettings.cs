namespace AltTabCustom.Settings;

/// <summary>
/// Top-level settings: shared behavior, display-profile switching, and the two
/// per-display visual profiles. Persisted as JSON in
/// %AppData%\AltTabCustom\settings.json.
/// </summary>
public sealed class AppSettings
{
    // ---- Behavior (shared across displays) ----
    /// <summary>Start with Windows (per-user HKCU Run key; never needs admin).</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Inject a harmless key when the switcher opens so releasing Alt does not
    /// pop the foreground app's menu bar.
    /// </summary>
    public bool PreventAltMenu { get; set; } = true;

    /// <summary>Close and activate when the mouse clicks an item.</summary>
    public bool ClickToActivate { get; set; } = true;

    /// <summary>
    /// User-defined icon overrides. The first rule whose title/process matches a
    /// window forces its icon, falling back to the OS icon when none match.
    /// </summary>
    public List<IconRule> IconRules { get; set; } = new();

    /// <summary>
    /// Also apply the icon rules to the <b>real</b> Windows windows (taskbar
    /// button + title bar), not just inside our overlay, via WM_SETICON. Needs no
    /// admin; the override lasts while AltTabCustom runs and is restored on exit.
    /// Elevated (admin) windows are unaffected — the same security boundary that
    /// applies to the keyboard hook.
    /// </summary>
    public bool ForceWindowIcons { get; set; } = false;

    /// <summary>
    /// Milliseconds Alt must be held after Alt+Tab before the overlay appears.
    /// A quick tap within this window switches straight to the previous (MRU)
    /// window without ever showing the UI, like the native switcher. Set to 0
    /// to always show the overlay immediately.
    /// </summary>
    public int ShowDelayMs { get; set; } = 200;

    /// <summary>
    /// Experimental: paint an acrylic (blurred) background behind the overlay.
    /// Falls back silently to the normal background if unsupported.
    /// </summary>
    public bool AcrylicBackground { get; set; } = false;

    // ---- Display-profile switching ----
    /// <summary>
    /// When true, the active profile is chosen automatically from the effective
    /// width of the monitor the switcher opens on. When false, the Docked
    /// profile is always used.
    /// </summary>
    public bool EnableDisplayProfiles { get; set; } = true;

    /// <summary>
    /// Effective (DPI-scaled) width, in device-independent pixels, at or above
    /// which the Docked profile is used; below it, the Laptop profile is used.
    /// Default 2560 separates a typical laptop (~1920) from a large/ultrawide
    /// external monitor.
    /// </summary>
    public double LargeDisplayMinWidth { get; set; } = 2560;

    /// <summary>Visual settings for a large/docked monitor.</summary>
    public DisplayProfile Docked { get; set; } = DisplayProfile.LargeDefault();

    /// <summary>Visual settings for a small/laptop screen.</summary>
    public DisplayProfile Laptop { get; set; } = DisplayProfile.SmallDefault();

    /// <summary>
    /// Pick the profile to use for a monitor of the given effective width.
    /// </summary>
    public DisplayProfile ResolveProfile(double effectiveWidth)
    {
        if (!EnableDisplayProfiles) return Docked;
        return effectiveWidth >= LargeDisplayMinWidth ? Docked : Laptop;
    }
}
