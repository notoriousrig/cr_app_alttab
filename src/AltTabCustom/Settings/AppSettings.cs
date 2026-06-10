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
