using System.Runtime.InteropServices;
using System.Windows.Media;

namespace AltTabCustom.Interop;

/// <summary>
/// Applies an acrylic (blur-behind) backdrop to a window via the undocumented
/// SetWindowCompositionAttribute API. Best-effort and fully guarded: if the API
/// is unavailable or fails, the window simply keeps its normal background.
/// </summary>
internal static class AcrylicHelper
{
    private enum AccentState
    {
        Disabled = 0,
        AcrylicBlurBehind = 4,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor; // 0xAABBGGRR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private const int WCA_ACCENT_POLICY = 19;

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    public static void Apply(IntPtr hwnd, bool enable, Color tint)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            var accent = new AccentPolicy
            {
                AccentState = (int)(enable ? AccentState.AcrylicBlurBehind : AccentState.Disabled),
                AccentFlags = 2, // draw the blur over the whole client area
                GradientColor = ToAbgr(tint),
                AnimationId = 0,
            };

            int size = Marshal.SizeOf<AccentPolicy>();
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size,
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch
        {
            // Acrylic is purely cosmetic; never let it break the overlay.
        }
    }

    private static uint ToAbgr(Color c)
        => (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R);
}
