using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using AltTabCustom.Interop;

namespace AltTabCustom.UI;

/// <summary>
/// View model for one row in the switcher. Wraps a <see cref="WindowInfo"/> and
/// tracks whether it is the current selection (so the template can highlight it).
/// </summary>
public sealed class SwitcherItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public SwitcherItem(WindowInfo window, string query = "")
    {
        Window = window;
        Query = query;
    }

    public WindowInfo Window { get; }

    /// <summary>Current search query, used to highlight the matched substring.</summary>
    public string Query { get; }

    public string Title => Window.Title;
    public string ProcessName => Window.ProcessName;
    public ImageSource? Icon => Window.Icon;
    public IntPtr Handle => Window.Handle;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
