using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AltTabCustom.Interop;
using AltTabCustom.Settings;
using static AltTabCustom.Interop.NativeMethods;

namespace AltTabCustom.UI;

public partial class SwitcherWindow : Window
{
    private readonly List<SwitcherItem> _items = new();
    private int _selectedIndex = -1;
    private AppSettings _settings = new();

    private IntPtr _targetMonitor;

    /// <summary>Raised when the user activates an item (click or commit).</summary>
    public event Action<WindowInfo>? ItemActivated;

    /// <summary>Raised when the user asks to close an item (middle-click).</summary>
    public event Action<WindowInfo>? ItemCloseRequested;

    public SwitcherWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Make the overlay a no-activate tool window so it never steals focus
        // and never shows up in its own Alt+Tab list.
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        long ex = GetWindowLongPtrSafe(hwnd, GWL_EXSTYLE);
        ex |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
        SetWindowLongPtrSafe(hwnd, GWL_EXSTYLE, ex);
    }

    public bool IsShowing { get; private set; }

    public IReadOnlyList<SwitcherItem> Items => _items;
    public int SelectedIndex => _selectedIndex;

    public WindowInfo? SelectedWindow =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex].Window : null;

    /// <summary>
    /// Populate, style, position and display the switcher.
    /// </summary>
    public void ShowSwitcher(IReadOnlyList<WindowInfo> windows, int selectedIndex,
        AppSettings settings, IntPtr targetMonitorWindow)
    {
        _settings = settings;
        _targetMonitor = targetMonitorWindow;
        ApplyTheme(settings);
        SetSearchText(string.Empty);

        PopulateItems(windows, selectedIndex);

        IsShowing = true;
        Visibility = Visibility.Visible;
        Show();

        // The items panel is realized during the layout pass triggered by Show();
        // apply the column count once it exists.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
            () => ApplyColumns(_settings.Columns));

        SetSelection(selectedIndex);
        PositionOnMonitor(_targetMonitor);
    }

    /// <summary>
    /// Replace the items while the switcher is already open (used by live
    /// search filtering and by closing windows from the list).
    /// </summary>
    public void UpdateItems(IReadOnlyList<WindowInfo> windows, int selectedIndex, string searchText)
    {
        SetSearchText(searchText);
        PopulateItems(windows, selectedIndex);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
            () => ApplyColumns(_settings.Columns));
        SetSelection(selectedIndex);
        // Size changes with the filtered count, so re-center after layout settles.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
            () => PositionOnMonitor(_targetMonitor));
    }

    private void PopulateItems(IReadOnlyList<WindowInfo> windows, int selectedIndex)
    {
        _items.Clear();
        foreach (var w in windows)
            _items.Add(new SwitcherItem(w));

        ItemsHost.ItemsSource = null;
        ItemsHost.ItemsSource = _items;

        ConstrainHeight(_settings);
    }

    private void SetSearchText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            SearchBar.Visibility = Visibility.Collapsed;
            SearchText.Text = string.Empty;
        }
        else
        {
            SearchText.Text = "🔍  " + text; // magnifier glyph + query
            SearchBar.Visibility = Visibility.Visible;
        }
    }

    public void HideSwitcher()
    {
        IsShowing = false;
        Hide();
    }

    public void MoveSelection(int delta)
    {
        if (_items.Count == 0) return;
        int next = (_selectedIndex + delta) % _items.Count;
        if (next < 0) next += _items.Count;
        SetSelection(next);
    }

    private void SetSelection(int index)
    {
        if (_items.Count == 0) { _selectedIndex = -1; return; }
        index = Math.Clamp(index, 0, _items.Count - 1);

        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            _items[_selectedIndex].IsSelected = false;

        _selectedIndex = index;
        _items[_selectedIndex].IsSelected = true;

        BringSelectedIntoView();
    }

    private void BringSelectedIntoView()
    {
        if (_selectedIndex < 0) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            if (ItemsHost.ItemContainerGenerator.ContainerFromIndex(_selectedIndex) is FrameworkElement fe)
                fe.BringIntoView();
        });
    }

    private void Item_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_settings.ClickToActivate) return;
        if (sender is FrameworkElement { DataContext: SwitcherItem item })
            ItemActivated?.Invoke(item.Window);
    }

    private void Item_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle-click closes a window without switching to it.
        if (e.ChangedButton != MouseButton.Middle) return;
        if (sender is FrameworkElement { DataContext: SwitcherItem item })
        {
            e.Handled = true;
            ItemCloseRequested?.Invoke(item.Window);
        }
    }

    private void ConstrainHeight(AppSettings settings)
    {
        int columns = Math.Max(1, settings.Columns);
        int rows = (int)Math.Ceiling(Math.Max(1, settings.MaxVisibleItems) / (double)columns);
        // Item height + its 4px top/bottom margins.
        Scroller.MaxHeight = rows * (settings.ItemHeight + 8);
    }

    private void PositionOnMonitor(IntPtr targetWindow)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        uint dpi = GetDpiForWindow(hwnd);
        double scale = dpi == 0 ? 1.0 : dpi / 96.0;

        double pxW = ActualWidth * scale;
        double pxH = ActualHeight * scale;

        var screen = targetWindow != IntPtr.Zero
            ? System.Windows.Forms.Screen.FromHandle(targetWindow)
            : System.Windows.Forms.Screen.PrimaryScreen!;
        var area = screen.WorkingArea; // pixels

        int x = area.X + (int)((area.Width - pxW) / 2);
        int y = area.Y + (int)((area.Height - pxH) / 2);

        SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0,
            SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    private void ApplyTheme(AppSettings s)
    {
        var res = Resources;
        res["BackgroundBrush"] = Frozen(ColorFromHex(s.BackgroundColor));
        res["SelectionBrush"] = Frozen(ColorFromHex(s.SelectionColor));
        res["TextBrush"] = Frozen(ColorFromHex(s.TextColor));
        res["SubTextBrush"] = Frozen(ColorFromHex(s.SubTextColor));

        res["CornerRadiusValue"] = new CornerRadius(s.CornerRadius);
        res["WindowOpacityValue"] = Math.Clamp(s.WindowOpacity, 0.1, 1.0);

        res["ItemWidthValue"] = s.ItemWidth;
        res["ItemHeightValue"] = s.ItemHeight;
        res["IconSizeValue"] = s.IconSize;

        res["ItemFontFamily"] = new FontFamily(string.IsNullOrWhiteSpace(s.FontFamily) ? "Segoe UI" : s.FontFamily);
        res["ItemFontSize"] = s.FontSize;
        res["ItemFontWeight"] = ParseWeight(s.FontWeight);
        res["ProcessFontSize"] = s.ProcessFontSize;
        res["SubTextVisibility"] = s.ShowProcessName ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyColumns(int columns)
    {
        // The UniformGrid is realized from the ItemsPanelTemplate during layout,
        // so apply this after the panel exists (see ShowSwitcher's dispatch).
        if (FindDescendant<UniformGrid>(ItemsHost) is UniformGrid grid)
            grid.Columns = Math.Max(1, columns);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t) return t;
            var found = FindDescendant<T>(child);
            if (found is not null) return found;
        }
        return null;
    }

    private static readonly FontWeightConverter WeightConverter = new();

    private static FontWeight ParseWeight(string name)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(name) && WeightConverter.ConvertFromString(name) is FontWeight w)
                return w;
        }
        catch
        {
            // Unknown weight name — fall back below.
        }
        return FontWeights.Normal;
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Color ColorFromHex(string hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return Colors.Black;
            return (Color)ColorConverter.ConvertFromString(hex.Trim())!;
        }
        catch
        {
            return Colors.Black;
        }
    }

    // Never let Alt-Tabbing accidentally close the app; closing is handled by the tray.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Allow real shutdown only when the app is exiting.
        if (!Application.Current.Dispatcher.HasShutdownStarted && IsShowing)
        {
            e.Cancel = true;
            HideSwitcher();
        }
        base.OnClosing(e);
    }
}
