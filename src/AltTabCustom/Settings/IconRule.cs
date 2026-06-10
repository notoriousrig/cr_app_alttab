using System.IO;
using System.Text.RegularExpressions;

namespace AltTabCustom.Settings;

/// <summary>Which window field a rule tests.</summary>
public enum RuleField
{
    Title,
    ProcessName,
}

/// <summary>How a rule's pattern is compared against the field.</summary>
public enum RuleMatch
{
    Contains,
    Equals,
    Regex,
}

/// <summary>
/// A user-defined override that forces a specific icon for any window whose
/// title or process name matches. A rule may add an optional second condition
/// (e.g. title contains X <em>and</em> process contains Y); when present both
/// must match. Rules are evaluated in order; the first match wins. All matching
/// is case-insensitive.
/// </summary>
public sealed class IconRule
{
    public bool Enabled { get; set; } = true;

    public RuleField Field { get; set; } = RuleField.Title;
    public RuleMatch Match { get; set; } = RuleMatch.Contains;
    public string Pattern { get; set; } = string.Empty;

    // Optional second condition, ANDed with the first. Active only when Pattern2
    // is non-empty; null on rules saved before this was added.
    public RuleField? Field2 { get; set; }
    public RuleMatch? Match2 { get; set; }
    public string? Pattern2 { get; set; }

    /// <summary>Path to a .ico / .png (or other WPF-loadable image) to use.</summary>
    public string IconPath { get; set; } = string.Empty;

    /// <summary>Whether the second (AND) condition is in use.</summary>
    public bool HasSecondCondition => !string.IsNullOrWhiteSpace(Pattern2);

    /// <summary>True if this rule should be tested against the given window.</summary>
    public bool Matches(string? title, string? processName)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(Pattern)) return false;
        if (!MatchOne(Field, Match, Pattern, title, processName)) return false;

        if (HasSecondCondition &&
            !MatchOne(Field2 ?? RuleField.ProcessName, Match2 ?? RuleMatch.Contains, Pattern2!, title, processName))
            return false;

        return true;
    }

    private static bool MatchOne(RuleField field, RuleMatch match, string pattern, string? title, string? processName)
    {
        string value = (field == RuleField.Title ? title : processName) ?? string.Empty;
        return match switch
        {
            RuleMatch.Contains => value.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            RuleMatch.Equals => string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase),
            RuleMatch.Regex => SafeRegex(value, pattern),
            _ => false,
        };
    }

    private static bool SafeRegex(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(50));
        }
        catch
        {
            // Invalid regex or timeout — treat as no match rather than throwing.
            return false;
        }
    }

    /// <summary>Human-readable one-liner for the settings list.</summary>
    public string Summary
    {
        get
        {
            string file = string.IsNullOrWhiteSpace(IconPath) ? "(no icon)" : Path.GetFileName(IconPath);
            string prefix = Enabled ? string.Empty : "(off) ";
            string text = Describe(Field, Match, Pattern);
            if (HasSecondCondition)
                text += " and " + Describe(Field2 ?? RuleField.ProcessName, Match2 ?? RuleMatch.Contains, Pattern2!);
            return $"{prefix}{text} → {file}";
        }
    }

    private static string Describe(RuleField field, RuleMatch match, string pattern)
    {
        string fieldName = field == RuleField.Title ? "Title" : "Process";
        string verb = match switch
        {
            RuleMatch.Equals => "equals",
            RuleMatch.Regex => "matches",
            _ => "contains",
        };
        return $"{fieldName} {verb} “{pattern}”";
    }

    public IconRule Clone() => new()
    {
        Enabled = Enabled,
        Field = Field,
        Match = Match,
        Pattern = Pattern,
        Field2 = Field2,
        Match2 = Match2,
        Pattern2 = Pattern2,
        IconPath = IconPath,
    };
}
