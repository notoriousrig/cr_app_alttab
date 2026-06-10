namespace AltTabCustom.Settings;

/// <summary>
/// All the visual settings that can differ between displays (e.g. a docked 49"
/// monitor vs a laptop screen). One of these is chosen at switch-time based on
/// the active monitor's effective width.
/// </summary>
public sealed class DisplayProfile
{
    // ---- Layout ----
    public int MaxVisibleItems { get; set; } = 7;
    public int Columns { get; set; } = 1;
    public double ItemWidth { get; set; } = 520;
    public double ItemHeight { get; set; } = 64;
    public double IconSize { get; set; } = 40;

    // ---- Typography ----
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 18;
    public string FontWeight { get; set; } = "Normal";
    public bool ShowProcessName { get; set; } = true;
    public double ProcessFontSize { get; set; } = 12;

    // ---- Colors ----
    public string BackgroundColor { get; set; } = "#EE1F1F22";
    public string SelectionColor { get; set; } = "#FF3B82F6";
    public string TextColor { get; set; } = "#FFF2F2F2";
    public string SubTextColor { get; set; } = "#FFAFAFAF";
    public double CornerRadius { get; set; } = 12;
    public double WindowOpacity { get; set; } = 1.0;

    public DisplayProfile Clone() => (DisplayProfile)MemberwiseClone();

    /// <summary>Roomy defaults for a large docked monitor.</summary>
    public static DisplayProfile LargeDefault() => new()
    {
        MaxVisibleItems = 10,
        Columns = 1,
        ItemWidth = 660,
        ItemHeight = 76,
        IconSize = 52,
        FontSize = 22,
        ProcessFontSize = 14,
    };

    /// <summary>Compact defaults for a laptop screen.</summary>
    public static DisplayProfile SmallDefault() => new()
    {
        MaxVisibleItems = 7,
        Columns = 1,
        ItemWidth = 460,
        ItemHeight = 56,
        IconSize = 32,
        FontSize = 16,
        ProcessFontSize = 11,
    };
}
