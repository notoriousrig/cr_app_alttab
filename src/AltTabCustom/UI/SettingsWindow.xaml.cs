using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using AltTabCustom.Settings;
using Forms = System.Windows.Forms;

namespace AltTabCustom.UI;

public partial class SettingsWindow : Window
{
    /// <summary>Raised with the new settings when the user clicks Save.</summary>
    public event Action<AppSettings>? SettingsSaved;

    private readonly ObservableCollection<IconRule> _rules = new();
    private bool _loadingRule;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        RulesList.ItemsSource = _rules;
        LoadInto(current);
        Loaded += (_, _) => UpdateScreenHint();
    }

    private void LoadInto(AppSettings s)
    {
        DockedEditor.LoadFrom(s.Docked);
        LaptopEditor.LoadFrom(s.Laptop);

        EnableProfilesBox.IsChecked = s.EnableDisplayProfiles;
        ThresholdBox.Text = s.LargeDisplayMinWidth.ToString(CultureInfo.InvariantCulture);

        ShowDelayBox.Text = s.ShowDelayMs.ToString(CultureInfo.InvariantCulture);
        StartWithWindowsBox.IsChecked = s.StartWithWindows;
        PreventAltMenuBox.IsChecked = s.PreventAltMenu;
        ClickToActivateBox.IsChecked = s.ClickToActivate;
        AcrylicBox.IsChecked = s.AcrylicBackground;

        _rules.Clear();
        foreach (var rule in s.IconRules ?? new())
            _rules.Add(rule.Clone()); // clone so Cancel doesn't mutate live settings
        RulesList.SelectedIndex = -1;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var d = new AppSettings();
        var s = new AppSettings
        {
            StartWithWindows = StartWithWindowsBox.IsChecked == true,
            PreventAltMenu = PreventAltMenuBox.IsChecked == true,
            ClickToActivate = ClickToActivateBox.IsChecked == true,
            ShowDelayMs = FieldParse.Int(ShowDelayBox.Text, d.ShowDelayMs, 0, 2000),
            AcrylicBackground = AcrylicBox.IsChecked == true,

            EnableDisplayProfiles = EnableProfilesBox.IsChecked == true,
            LargeDisplayMinWidth = FieldParse.Dbl(ThresholdBox.Text, d.LargeDisplayMinWidth, 400, 20000),

            Docked = DockedEditor.ToProfile(),
            Laptop = LaptopEditor.ToProfile(),

            IconRules = _rules.Select(r => r.Clone()).ToList(),
        };

        SettingsSaved?.Invoke(s);
        Close();
    }

    // ---- Icon rules tab ----

    private IconRule? SelectedRule => RulesList.SelectedItem as IconRule;

    private void RulesList_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var rule = SelectedRule;
        RuleEditor.IsEnabled = rule is not null;
        RemoveRuleButton.IsEnabled = rule is not null;
        if (rule is null) return;

        _loadingRule = true;
        FieldCombo.SelectedIndex = rule.Field == RuleField.Title ? 0 : 1;
        MatchCombo.SelectedIndex = (int)rule.Match;
        PatternBox.Text = rule.Pattern;
        IconPathBox.Text = rule.IconPath;
        RuleEnabledBox.IsChecked = rule.Enabled;
        _loadingRule = false;
    }

    private void RuleDetail_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingRule || SelectedRule is not { } rule) return;

        rule.Field = FieldCombo.SelectedIndex == 1 ? RuleField.ProcessName : RuleField.Title;
        rule.Match = (RuleMatch)Math.Max(0, MatchCombo.SelectedIndex);
        rule.Pattern = PatternBox.Text;
        rule.IconPath = IconPathBox.Text;
        rule.Enabled = RuleEnabledBox.IsChecked == true;
        RulesList.Items.Refresh(); // update the summary text
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = new IconRule();
        _rules.Add(rule);
        RulesList.SelectedItem = rule;
        PatternBox.Focus();
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRule is { } rule) _rules.Remove(rule);
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose an icon",
            Filter = "Images (*.ico;*.png;*.jpg;*.bmp;*.gif)|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog(this) == true)
            IconPathBox.Text = dlg.FileName; // TextChanged writes it back to the rule
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => LoadInto(new AppSettings());

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Show the effective width of this window's monitor to help pick a threshold.</summary>
    private void UpdateScreenHint()
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            var screen = hwnd != IntPtr.Zero ? Forms.Screen.FromHandle(hwnd) : Forms.Screen.PrimaryScreen!;
            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            if (scale <= 0) scale = 1;
            double effective = screen.Bounds.Width / scale;

            double threshold = FieldParse.Dbl(ThresholdBox.Text, 2560, 400, 20000);
            string which = effective >= threshold ? "Docked" : "Laptop";
            CurrentScreenHint.Text =
                $"This screen is ≈ {effective:0} px effective wide — currently the “{which}” profile.";
        }
        catch
        {
            CurrentScreenHint.Text = string.Empty;
        }
    }
}
