using System.Text.Json;
using AltTabCustom.Settings;
using Xunit;

namespace AltTabCustom.Tests;

public class SettingsMigrationTests
{
    [Fact]
    public void LegacyFlatConfig_MigratesIntoBothProfiles()
    {
        // The original v1 settings shape: a single flat set of visual fields.
        string json = """
        {
            "MaxVisibleItems": 9,
            "IconSize": 50,
            "FontSize": 21,
            "FontFamily": "Bahnschrift",
            "FontWeight": "Light",
            "BackgroundColor": "#FF101010",
            "StartWithWindows": true
        }
        """;

        var s = SettingsStore.FromJson(json);

        Assert.Equal(9, s.Docked.MaxVisibleItems);
        Assert.Equal(9, s.Laptop.MaxVisibleItems);
        Assert.Equal(50.0, s.Docked.IconSize);
        Assert.Equal(21.0, s.Laptop.FontSize);
        Assert.Equal("Bahnschrift", s.Docked.FontFamily);
        Assert.Equal("Light", s.Laptop.FontWeight);
        Assert.Equal("#FF101010", s.Docked.BackgroundColor);
        Assert.True(s.StartWithWindows);
    }

    [Fact]
    public void V2Config_RoundTrips()
    {
        var original = new AppSettings { LargeDisplayMinWidth = 3000 };
        original.Docked.FontSize = 30;
        original.Laptop.FontSize = 14;

        string json = JsonSerializer.Serialize(original);
        var s = SettingsStore.FromJson(json);

        Assert.Equal(3000.0, s.LargeDisplayMinWidth);
        Assert.Equal(30.0, s.Docked.FontSize);
        Assert.Equal(14.0, s.Laptop.FontSize);
    }

    [Fact]
    public void EmptyObject_ReturnsNonNullDefaults()
    {
        var s = SettingsStore.FromJson("{}");

        Assert.NotNull(s.Docked);
        Assert.NotNull(s.Laptop);
    }
}
