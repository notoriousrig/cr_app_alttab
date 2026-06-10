using AltTabCustom.Core;
using AltTabCustom.Interop;
using Xunit;

namespace AltTabCustom.Tests;

public class MruOrderTests
{
    private static WindowInfo W(int handle, string title = "w") =>
        new() { Handle = new IntPtr(handle), Title = title };

    [Fact]
    public void Touch_OrdersMostRecentFirst()
    {
        var mru = new MruOrder();
        mru.Touch(new IntPtr(1));
        mru.Touch(new IntPtr(2));
        mru.Touch(new IntPtr(3)); // 3 is now most recent

        var ordered = mru.Order(new List<WindowInfo> { W(1), W(2), W(3) });

        Assert.Equal(new IntPtr(3), ordered[0].Handle);
        Assert.Equal(new IntPtr(2), ordered[1].Handle);
        Assert.Equal(new IntPtr(1), ordered[2].Handle);
    }

    [Fact]
    public void Retouch_MovesWindowToFront()
    {
        var mru = new MruOrder();
        mru.Touch(new IntPtr(1));
        mru.Touch(new IntPtr(2));
        mru.Touch(new IntPtr(1)); // 1 refocused -> front

        var ordered = mru.Order(new List<WindowInfo> { W(2), W(1) });

        Assert.Equal(new IntPtr(1), ordered[0].Handle);
        Assert.Equal(new IntPtr(2), ordered[1].Handle);
    }

    [Fact]
    public void UntrackedWindows_KeepOriginalOrder_AfterTracked()
    {
        var mru = new MruOrder();
        mru.Touch(new IntPtr(5)); // only 5 is known

        var ordered = mru.Order(new List<WindowInfo> { W(1), W(5), W(2) });

        Assert.Equal(new IntPtr(5), ordered[0].Handle); // tracked first
        Assert.Equal(new IntPtr(1), ordered[1].Handle); // then incoming order
        Assert.Equal(new IntPtr(2), ordered[2].Handle);
    }
}
