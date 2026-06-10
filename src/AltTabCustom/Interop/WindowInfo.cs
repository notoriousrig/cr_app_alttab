using System.Windows.Media;

namespace AltTabCustom.Interop;

/// <summary>
/// A single switchable top-level window, as shown in the switcher overlay.
/// </summary>
public sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public uint ProcessId { get; init; }
    public ImageSource? Icon { get; set; }
    public bool IsMinimized { get; init; }

    public override string ToString() => $"{Title} ({ProcessName})";
}
