using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AltTabCustom.UI;

/// <summary>
/// Attached behavior that renders a <see cref="TextBlock"/>'s text with the
/// portion matching <see cref="QueryProperty"/> highlighted (bold + the
/// "HighlightBrush" resource). Used to show what the type-to-filter search
/// matched in each window title.
/// </summary>
public static class TextHighlight
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.RegisterAttached(
            "Source", typeof(string), typeof(TextHighlight),
            new PropertyMetadata(string.Empty, OnChanged));

    public static readonly DependencyProperty QueryProperty =
        DependencyProperty.RegisterAttached(
            "Query", typeof(string), typeof(TextHighlight),
            new PropertyMetadata(string.Empty, OnChanged));

    public static void SetSource(DependencyObject o, string v) => o.SetValue(SourceProperty, v);
    public static string GetSource(DependencyObject o) => (string)o.GetValue(SourceProperty);
    public static void SetQuery(DependencyObject o, string v) => o.SetValue(QueryProperty, v);
    public static string GetQuery(DependencyObject o) => (string)o.GetValue(QueryProperty);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;

        string text = GetSource(tb) ?? string.Empty;
        string query = GetQuery(tb) ?? string.Empty;

        tb.Inlines.Clear();

        if (string.IsNullOrEmpty(query))
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        var highlight = tb.TryFindResource("HighlightBrush") as Brush;
        int i = 0;
        while (i < text.Length)
        {
            int match = text.IndexOf(query, i, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
            {
                tb.Inlines.Add(new Run(text[i..]));
                break;
            }

            if (match > i)
                tb.Inlines.Add(new Run(text[i..match]));

            var hit = new Run(text.Substring(match, query.Length)) { FontWeight = FontWeights.Bold };
            if (highlight is not null) hit.Foreground = highlight;
            tb.Inlines.Add(hit);

            i = match + query.Length;
        }
    }
}
