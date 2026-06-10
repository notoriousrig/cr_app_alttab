using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AltTabCustom.Settings;

namespace AltTabCustom.UI;

/// <summary>
/// Edits a single <see cref="DisplayProfile"/> (all visual settings) and shows a
/// live preview of how the switcher will look. Two of these live in the Settings
/// window — one per display profile.
/// </summary>
public partial class ProfileEditorControl : UserControl
{
    private static readonly string[] WeightNames =
    {
        "Thin", "ExtraLight", "Light", "Normal", "Medium",
        "SemiBold", "Bold", "ExtraBold", "Black",
    };

    private static readonly ImageSource? AppIcon = TryLoadAppIcon();

    public ProfileEditorControl()
    {
        InitializeComponent();

        FontFamilyBox.ItemsSource = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        FontWeightBox.ItemsSource = WeightNames;

        // Refresh the preview whenever any field changes (these events bubble).
        AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => RefreshPreview()));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => RefreshPreview()));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => RefreshPreview()));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => RefreshPreview()));
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

        RefreshPreview();
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

    private void RefreshPreview()
    {
        if (PreviewHost is null) return;
        try
        {
            var p = ToProfile();

            PreviewHost.Background = Brush(p.BackgroundColor);
            PreviewHost.CornerRadius = new CornerRadius(p.CornerRadius);
            PreviewHost.Opacity = Math.Clamp(p.WindowOpacity, 0.1, 1.0);

            var stack = new StackPanel();
            stack.Children.Add(SampleRow(p, "Visual Studio Code — Program.cs", "Code", selected: true));
            stack.Children.Add(SampleRow(p, "Google Chrome — AltTabCustom", "chrome", selected: false));
            PreviewHost.Child = stack;
        }
        catch
        {
            // Preview is cosmetic; never let it throw while typing.
        }
    }

    private static UIElement SampleRow(DisplayProfile p, string title, string process, bool selected)
    {
        var border = new Border
        {
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(8),
            Width = p.ItemWidth,
            Height = p.ItemHeight,
            Background = selected ? Brush(p.SelectionColor) : Brushes.Transparent,
        };

        var grid = new Grid { Margin = new Thickness(10, 0, 10, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var img = new Image
        {
            Source = AppIcon,
            Width = p.IconSize,
            Height = p.IconSize,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(img, 0);
        grid.Children.Add(img);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 1);
        var family = new FontFamily(string.IsNullOrWhiteSpace(p.FontFamily) ? "Segoe UI" : p.FontFamily);
        text.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = Brush(p.TextColor),
            FontFamily = family,
            FontSize = p.FontSize,
            FontWeight = ParseWeight(p.FontWeight),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (p.ShowProcessName)
        {
            text.Children.Add(new TextBlock
            {
                Text = process,
                Foreground = Brush(p.SubTextColor),
                FontFamily = family,
                FontSize = p.ProcessFontSize,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        grid.Children.Add(text);

        border.Child = grid;
        return border;
    }

    private static readonly FontWeightConverter WeightConverter = new();

    private static FontWeight ParseWeight(string name)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(name) && WeightConverter.ConvertFromString(name) is FontWeight w)
                return w;
        }
        catch { /* fall through */ }
        return FontWeights.Normal;
    }

    private static Brush Brush(string hex)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex.Trim())!);
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    private static ImageSource? TryLoadAppIcon()
    {
        try
        {
            return new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        }
        catch
        {
            return null;
        }
    }
}
