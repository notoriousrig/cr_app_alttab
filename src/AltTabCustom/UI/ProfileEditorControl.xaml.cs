using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using AltTabCustom.Settings;

namespace AltTabCustom.UI;

/// <summary>
/// Edits a single <see cref="DisplayProfile"/> (all visual settings). Two of
/// these live in the Settings window — one per display profile.
/// </summary>
public partial class ProfileEditorControl : UserControl
{
    // Standard WPF font weights, lightest → heaviest.
    private static readonly string[] WeightNames =
    {
        "Thin", "ExtraLight", "Light", "Normal", "Medium",
        "SemiBold", "Bold", "ExtraBold", "Black",
    };

    public ProfileEditorControl()
    {
        InitializeComponent();

        FontFamilyBox.ItemsSource = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FontWeightBox.ItemsSource = WeightNames;
    }

    public void LoadFrom(DisplayProfile p)
    {
        MaxItemsBox.Text = p.MaxVisibleItems.ToString(CultureInfo.InvariantCulture);
        ColumnsBox.Text = p.Columns.ToString(CultureInfo.InvariantCulture);
        ItemWidthBox.Text = p.ItemWidth.ToString(CultureInfo.InvariantCulture);
        ItemHeightBox.Text = p.ItemHeight.ToString(CultureInfo.InvariantCulture);
        IconSizeBox.Text = p.IconSize.ToString(CultureInfo.InvariantCulture);

        FontFamilyBox.Text = p.FontFamily;
        FontSizeBox.Text = p.FontSize.ToString(CultureInfo.InvariantCulture);
        FontWeightBox.SelectedItem = WeightNames.Contains(p.FontWeight) ? p.FontWeight : "Normal";
        ProcessFontSizeBox.Text = p.ProcessFontSize.ToString(CultureInfo.InvariantCulture);
        ShowProcessNameBox.IsChecked = p.ShowProcessName;

        BackgroundColorBox.Text = p.BackgroundColor;
        SelectionColorBox.Text = p.SelectionColor;
        TextColorBox.Text = p.TextColor;
        SubTextColorBox.Text = p.SubTextColor;
        CornerRadiusBox.Text = p.CornerRadius.ToString(CultureInfo.InvariantCulture);
        OpacityBox.Text = p.WindowOpacity.ToString(CultureInfo.InvariantCulture);
    }

    public DisplayProfile ToProfile()
    {
        var d = new DisplayProfile();
        return new DisplayProfile
        {
            MaxVisibleItems = FieldParse.Int(MaxItemsBox.Text, d.MaxVisibleItems, 1, 100),
            Columns = FieldParse.Int(ColumnsBox.Text, d.Columns, 1, 12),
            ItemWidth = FieldParse.Dbl(ItemWidthBox.Text, d.ItemWidth, 80, 4000),
            ItemHeight = FieldParse.Dbl(ItemHeightBox.Text, d.ItemHeight, 24, 1000),
            IconSize = FieldParse.Dbl(IconSizeBox.Text, d.IconSize, 8, 256),

            FontFamily = string.IsNullOrWhiteSpace(FontFamilyBox.Text) ? d.FontFamily : FontFamilyBox.Text.Trim(),
            FontSize = FieldParse.Dbl(FontSizeBox.Text, d.FontSize, 6, 96),
            FontWeight = FontWeightBox.SelectedItem as string ?? d.FontWeight,
            ProcessFontSize = FieldParse.Dbl(ProcessFontSizeBox.Text, d.ProcessFontSize, 6, 72),
            ShowProcessName = ShowProcessNameBox.IsChecked == true,

            BackgroundColor = FieldParse.Color(BackgroundColorBox.Text, d.BackgroundColor),
            SelectionColor = FieldParse.Color(SelectionColorBox.Text, d.SelectionColor),
            TextColor = FieldParse.Color(TextColorBox.Text, d.TextColor),
            SubTextColor = FieldParse.Color(SubTextColorBox.Text, d.SubTextColor),
            CornerRadius = FieldParse.Dbl(CornerRadiusBox.Text, d.CornerRadius, 0, 60),
            WindowOpacity = FieldParse.Dbl(OpacityBox.Text, d.WindowOpacity, 0.1, 1.0),
        };
    }
}
