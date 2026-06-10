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
    private ImageSource? _icon;

    public SwitcherItem(WindowInfo window, string query = "")
    {
        Window = window;
        Query = query;
        _icon = window.Icon;
    }

    public WindowInfo Window { get; }

    /// <summary>Current search query, used to highlight the matched substring.</summary>
    public string Query { get; }

    public string Title => Window.Title;
    public string ProcessName => Window.ProcessName;
    public IntPtr Handle => Window.Handle;

    /// <summary>
    /// The icon. May be null initially and filled in asynchronously (icons are
    /// loaded off the keyboard-hook hot path) — hence the change notification.
    /// </summary>
    public ImageSource? Icon
    {
        get => _icon;
        private set
        {
            _icon = value;
            OnPropertyChanged();
        }
    }

    public void SetIcon(ImageSource? icon)
    {
        Window.Icon = icon;
        Icon = icon;
    }

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
