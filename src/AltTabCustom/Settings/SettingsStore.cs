using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AltTabCustom.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> to a per-user JSON file under
/// %AppData%. Transparently migrates the flat v1 settings format (a single set
/// of visual fields) into the v2 profile format (Docked + Laptop).
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AltTabCustom");

    public static string FilePath => Path.Combine(Directory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return FromJson(File.ReadAllText(FilePath));
        }
        catch
        {
            // Corrupt or unreadable file — fall back to defaults rather than crash.
        }
        return new AppSettings();
    }

    /// <summary>
    /// Parse settings JSON, transparently migrating the flat v1 format to v2.
    /// Exposed (internal) for unit testing.
    /// </summary>
    internal static AppSettings FromJson(string json)
    {
        if (JsonNode.Parse(json) is JsonObject obj)
        {
            bool hasProfiles = obj.ContainsKey("Docked") || obj.ContainsKey("Laptop");
            bool looksLegacy = !hasProfiles &&
                (obj.ContainsKey("FontSize") || obj.ContainsKey("MaxVisibleItems") || obj.ContainsKey("IconSize"));

            if (looksLegacy)
            {
                var legacy = JsonSerializer.Deserialize<LegacyV1>(json, JsonOptions) ?? new LegacyV1();
                return Migrate(legacy);
            }
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        return settings is not null ? Normalize(settings) : new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json);
    }

    // Ensure profiles are never null if a hand-edited file omitted them.
    private static AppSettings Normalize(AppSettings s)
    {
        s.Docked ??= DisplayProfile.LargeDefault();
        s.Laptop ??= DisplayProfile.SmallDefault();
        return s;
    }

    private static AppSettings Migrate(LegacyV1 v1)
    {
        var profile = new DisplayProfile
        {
            MaxVisibleItems = v1.MaxVisibleItems,
            Columns = v1.Columns,
            ItemWidth = v1.ItemWidth,
            ItemHeight = v1.ItemHeight,
            IconSize = v1.IconSize,
            FontFamily = v1.FontFamily,
            FontSize = v1.FontSize,
            FontWeight = v1.FontWeight,
            ShowProcessName = v1.ShowProcessName,
            ProcessFontSize = v1.ProcessFontSize,
            BackgroundColor = v1.BackgroundColor,
            SelectionColor = v1.SelectionColor,
            TextColor = v1.TextColor,
            SubTextColor = v1.SubTextColor,
            CornerRadius = v1.CornerRadius,
            WindowOpacity = v1.WindowOpacity,
        };

        // Seed both profiles from the old single config so behavior is unchanged
        // until the user tailors the laptop profile.
        return new AppSettings
        {
            StartWithWindows = v1.StartWithWindows,
            PreventAltMenu = v1.PreventAltMenu,
            ClickToActivate = v1.ClickToActivate,
            Docked = profile.Clone(),
            Laptop = profile.Clone(),
        };
    }

    /// <summary>The shape of the original v1 (flat) settings file, for migration.</summary>
    private sealed class LegacyV1
    {
        public int MaxVisibleItems { get; set; } = 7;
        public int Columns { get; set; } = 1;
        public double ItemWidth { get; set; } = 520;
        public double ItemHeight { get; set; } = 64;
        public double IconSize { get; set; } = 40;
        public string FontFamily { get; set; } = "Segoe UI";
        public double FontSize { get; set; } = 18;
        public string FontWeight { get; set; } = "Normal";
        public bool ShowProcessName { get; set; } = true;
        public double ProcessFontSize { get; set; } = 12;
        public string BackgroundColor { get; set; } = "#EE1F1F22";
        public string SelectionColor { get; set; } = "#FF3B82F6";
        public string TextColor { get; set; } = "#FFF2F2F2";
        public string SubTextColor { get; set; } = "#FFAFAFAF";
        public double CornerRadius { get; set; } = 12;
        public double WindowOpacity { get; set; } = 1.0;
        public bool StartWithWindows { get; set; }
        public bool PreventAltMenu { get; set; } = true;
        public bool ClickToActivate { get; set; } = true;
    }
}
