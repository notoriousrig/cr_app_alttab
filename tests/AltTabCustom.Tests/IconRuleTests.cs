using AltTabCustom.Settings;
using Xunit;

namespace AltTabCustom.Tests;

public class IconRuleTests
{
    [Theory]
    [InlineData("Calendar — Outlook", true)]
    [InlineData("My CALENDAR view", true)] // case-insensitive
    [InlineData("Inbox — Outlook", false)]
    public void Contains_MatchesTitleCaseInsensitively(string title, bool expected)
    {
        var rule = new IconRule { Field = RuleField.Title, Match = RuleMatch.Contains, Pattern = "calendar" };
        Assert.Equal(expected, rule.Matches(title, "OUTLOOK"));
    }

    [Fact]
    public void Equals_MatchesProcessNameExactlyIgnoringCase()
    {
        var rule = new IconRule { Field = RuleField.ProcessName, Match = RuleMatch.Equals, Pattern = "chrome" };
        Assert.True(rule.Matches("anything", "CHROME"));
        Assert.False(rule.Matches("anything", "chromedriver"));
    }

    [Fact]
    public void Regex_MatchesTitle()
    {
        var rule = new IconRule { Field = RuleField.Title, Match = RuleMatch.Regex, Pattern = @"^\d+ unread" };
        Assert.True(rule.Matches("42 unread messages", "mail"));
        Assert.False(rule.Matches("no unread", "mail"));
    }

    [Fact]
    public void InvalidRegex_DoesNotThrowAndDoesNotMatch()
    {
        var rule = new IconRule { Field = RuleField.Title, Match = RuleMatch.Regex, Pattern = "[unclosed" };
        Assert.False(rule.Matches("anything", "proc"));
    }

    [Fact]
    public void SecondCondition_RequiresBothToMatch()
    {
        var rule = new IconRule
        {
            Field = RuleField.Title, Match = RuleMatch.Contains, Pattern = "calendar",
            Field2 = RuleField.ProcessName, Match2 = RuleMatch.Contains, Pattern2 = "outlook",
        };

        Assert.True(rule.Matches("My Calendar", "OUTLOOK"));      // both match
        Assert.False(rule.Matches("My Calendar", "chrome"));      // process fails
        Assert.False(rule.Matches("Inbox", "OUTLOOK"));           // title fails
    }

    [Fact]
    public void EmptySecondPattern_IsIgnored_RuleActsOnFirstOnly()
    {
        var rule = new IconRule
        {
            Field = RuleField.Title, Match = RuleMatch.Contains, Pattern = "calendar",
            Field2 = RuleField.ProcessName, Match2 = RuleMatch.Contains, Pattern2 = "",
        };

        Assert.False(rule.HasSecondCondition);
        Assert.True(rule.Matches("My Calendar", "anything"));
    }

    [Fact]
    public void DisabledRule_NeverMatches()
    {
        var rule = new IconRule { Enabled = false, Match = RuleMatch.Contains, Pattern = "calendar" };
        Assert.False(rule.Matches("Calendar", "proc"));
    }

    [Fact]
    public void EmptyPattern_NeverMatches()
    {
        var rule = new IconRule { Match = RuleMatch.Contains, Pattern = "" };
        Assert.False(rule.Matches("anything", "proc"));
    }

    [Fact]
    public void NullFieldValues_AreTreatedAsEmpty()
    {
        var rule = new IconRule { Field = RuleField.Title, Match = RuleMatch.Contains, Pattern = "x" };
        Assert.False(rule.Matches(null, null));
    }

    [Fact]
    public void Rules_RoundTripThroughSettingsJson()
    {
        var settings = new AppSettings();
        settings.IconRules.Add(new IconRule
        {
            Field = RuleField.Title,
            Match = RuleMatch.Regex,
            Pattern = "calendar",
            IconPath = @"C:\icons\cal.ico",
        });

        var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        });
        var loaded = SettingsStore.FromJson(json);

        var rule = Assert.Single(loaded.IconRules);
        Assert.Equal(RuleField.Title, rule.Field);
        Assert.Equal(RuleMatch.Regex, rule.Match);
        Assert.Equal("calendar", rule.Pattern);
        Assert.Equal(@"C:\icons\cal.ico", rule.IconPath);
        // Enums should persist as readable strings, not integers.
        Assert.Contains("\"Regex\"", json);
    }
}
