namespace AltTabCustom.Settings;

/// <summary>
/// User-customizable settings. Persisted as JSON in
/// %AppData%\AltTabCustom\settings.json. All values have sensible defaults so
/// the app works the first time it runs with no config file present.
/// </summary>
public sealed class AppSettings
{
    // ---- Layout ----
    /// <summary>Maximum number of windows shown at once before the list scrolls.</summary>
    public int MaxVisibleItems { get; set; } = 7;

    /// <summary>Number of columns in the grid (1 = a classic vertical list).</summary>
    public int Columns { get; set; } = 1;

    /// <summary>Width of each item, in device-independent pixels.</summary>
    public double ItemWidth { get; set; } = 520;

    /// <summary>Height of each item, in device-independent pixels.</summary>
    public double ItemHeight { get; set; } = 64;

    /// <summary>Icon edge length, in device-independent pixels.</summary>
    public double IconSize { get; set; } = 40;

    // ---- Typography ----
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 18;

    /// <summary>
    /// Font weight name, e.g. Thin, ExtraLight, Light, Normal, Medium, SemiBold,
    /// Bold, ExtraBold, Black. Lets you pick a specific weight of a family such
    /// as "Light" for Bahnschrift. Anything WPF's FontWeightConverter accepts
    /// works; unknown values fall back to Normal.
    /// </summary>
    public string FontWeight { get; set; } = "Normal";

    /// <summary>Also show the owning process name beneath the title.</summary>
    public bool ShowProcessName { get; set; } = true;
    public double ProcessFontSize { get; set; } = 12;

    // ---- Colors (hex #AARRGGBB or #RRGGBB) ----
    public string BackgroundColor { get; set; } = "#EE1F1F22";
    public string SelectionColor { get; set; } = "#FF3B82F6";
    public string TextColor { get; set; } = "#FFF2F2F2";
    public string SubTextColor { get; set; } = "#FFAFAFAF";
    public double CornerRadius { get; set; } = 12;
    public double WindowOpacity { get; set; } = 1.0;

    // ---- Behavior ----
    /// <summary>Start with Windows (per-user HKCU Run key; never needs admin).</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Inject a harmless key when the switcher opens so releasing Alt does not
    /// pop the foreground app's menu bar. Leave on unless it causes trouble.
    /// </summary>
    public bool PreventAltMenu { get; set; } = true;

    /// <summary>Close and activate when the mouse clicks an item.</summary>
    public bool ClickToActivate { get; set; } = true;
}
