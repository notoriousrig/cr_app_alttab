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
/// title or process name matches. Rules are evaluated in order; the first match
/// wins. All matching is case-insensitive.
/// </summary>
public sealed class IconRule
{
    public bool Enabled { get; set; } = true;
    public RuleField Field { get; set; } = RuleField.Title;
    public RuleMatch Match { get; set; } = RuleMatch.Contains;
    public string Pattern { get; set; } = string.Empty;

    /// <summary>Path to a .ico / .png (or other WPF-loadable image) to use.</summary>
    public string IconPath { get; set; } = string.Empty;

    /// <summary>True if this rule should be tested against the given window.</summary>
    public bool Matches(string? title, string? processName)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(Pattern)) return false;

        string value = (Field == RuleField.Title ? title : processName) ?? string.Empty;
        return Match switch
        {
            RuleMatch.Contains => value.Contains(Pattern, StringComparison.OrdinalIgnoreCase),
            RuleMatch.Equals => string.Equals(value, Pattern, StringComparison.OrdinalIgnoreCase),
            RuleMatch.Regex => SafeRegex(value, Pattern),
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
            string field = Field == RuleField.Title ? "Title" : "Process";
            string verb = Match switch
            {
                RuleMatch.Equals => "equals",
                RuleMatch.Regex => "matches",
                _ => "contains",
            };
            string file = string.IsNullOrWhiteSpace(IconPath) ? "(no icon)" : Path.GetFileName(IconPath);
            string prefix = Enabled ? string.Empty : "(off) ";
            return $"{prefix}{field} {verb} “{Pattern}” → {file}";
        }
    }

    public IconRule Clone() => new()
    {
        Enabled = Enabled,
        Field = Field,
        Match = Match,
        Pattern = Pattern,
        IconPath = IconPath,
    };
}
