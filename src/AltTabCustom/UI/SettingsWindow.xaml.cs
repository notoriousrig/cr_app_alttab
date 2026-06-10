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

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
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
        };

        SettingsSaved?.Invoke(s);
        Close();
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
