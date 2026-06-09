using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AltTabCustom.Settings;

namespace AltTabCustom.UI;

public partial class SettingsWindow : Window
{
    /// <summary>Raised with the new settings when the user clicks Save.</summary>
    public event Action<AppSettings>? SettingsSaved;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        PopulateFontList();
        LoadInto(current);
    }

    private void PopulateFontList()
    {
        var names = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FontFamilyBox.ItemsSource = names;
    }

    private void LoadInto(AppSettings s)
    {
        MaxItemsBox.Text = s.MaxVisibleItems.ToString(CultureInfo.InvariantCulture);
        ColumnsBox.Text = s.Columns.ToString(CultureInfo.InvariantCulture);
        ItemWidthBox.Text = s.ItemWidth.ToString(CultureInfo.InvariantCulture);
        ItemHeightBox.Text = s.ItemHeight.ToString(CultureInfo.InvariantCulture);
        IconSizeBox.Text = s.IconSize.ToString(CultureInfo.InvariantCulture);

        FontFamilyBox.Text = s.FontFamily;
        FontSizeBox.Text = s.FontSize.ToString(CultureInfo.InvariantCulture);
        FontBoldBox.IsChecked = s.FontBold;
        ProcessFontSizeBox.Text = s.ProcessFontSize.ToString(CultureInfo.InvariantCulture);

        BackgroundColorBox.Text = s.BackgroundColor;
        SelectionColorBox.Text = s.SelectionColor;
        TextColorBox.Text = s.TextColor;
        SubTextColorBox.Text = s.SubTextColor;
        CornerRadiusBox.Text = s.CornerRadius.ToString(CultureInfo.InvariantCulture);
        OpacityBox.Text = s.WindowOpacity.ToString(CultureInfo.InvariantCulture);

        ShowProcessNameBox.IsChecked = s.ShowProcessName;
        StartWithWindowsBox.IsChecked = s.StartWithWindows;
        PreventAltMenuBox.IsChecked = s.PreventAltMenu;
        ClickToActivateBox.IsChecked = s.ClickToActivate;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Start from defaults, then overlay parsed values (falling back to the
        // default when a field can't be parsed, so a typo can't corrupt config).
        var d = new AppSettings();
        var s = new AppSettings
        {
            MaxVisibleItems = ParseInt(MaxItemsBox.Text, d.MaxVisibleItems, 1, 100),
            Columns = ParseInt(ColumnsBox.Text, d.Columns, 1, 12),
            ItemWidth = ParseDouble(ItemWidthBox.Text, d.ItemWidth, 80, 4000),
            ItemHeight = ParseDouble(ItemHeightBox.Text, d.ItemHeight, 24, 1000),
            IconSize = ParseDouble(IconSizeBox.Text, d.IconSize, 8, 256),

            FontFamily = string.IsNullOrWhiteSpace(FontFamilyBox.Text) ? d.FontFamily : FontFamilyBox.Text.Trim(),
            FontSize = ParseDouble(FontSizeBox.Text, d.FontSize, 6, 96),
            FontBold = FontBoldBox.IsChecked == true,
            ProcessFontSize = ParseDouble(ProcessFontSizeBox.Text, d.ProcessFontSize, 6, 72),

            BackgroundColor = ValidColor(BackgroundColorBox.Text, d.BackgroundColor),
            SelectionColor = ValidColor(SelectionColorBox.Text, d.SelectionColor),
            TextColor = ValidColor(TextColorBox.Text, d.TextColor),
            SubTextColor = ValidColor(SubTextColorBox.Text, d.SubTextColor),
            CornerRadius = ParseDouble(CornerRadiusBox.Text, d.CornerRadius, 0, 60),
            WindowOpacity = ParseDouble(OpacityBox.Text, d.WindowOpacity, 0.1, 1.0),

            ShowProcessName = ShowProcessNameBox.IsChecked == true,
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            PreventAltMenu = PreventAltMenuBox.IsChecked == true,
            ClickToActivate = ClickToActivateBox.IsChecked == true,
        };

        SettingsSaved?.Invoke(s);
        Close();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => LoadInto(new AppSettings());

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private static int ParseInt(string text, int fallback, int min, int max)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? Math.Clamp(v, min, max) : fallback;

    private static double ParseDouble(string text, double fallback, double min, double max)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
            ? Math.Clamp(v, min, max) : fallback;

    private static string ValidColor(string text, string fallback)
    {
        try
        {
            _ = ColorConverter.ConvertFromString(text.Trim());
            return text.Trim();
        }
        catch
        {
            return fallback;
        }
    }
}
