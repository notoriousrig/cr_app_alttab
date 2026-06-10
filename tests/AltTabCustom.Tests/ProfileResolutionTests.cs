using AltTabCustom.Settings;
using Xunit;

namespace AltTabCustom.Tests;

public class ProfileResolutionTests
{
    [Fact]
    public void WidthAboveThreshold_UsesDocked()
    {
        var s = new AppSettings { LargeDisplayMinWidth = 2560 };
        Assert.Same(s.Docked, s.ResolveProfile(3440));
    }

    [Fact]
    public void WidthAtThreshold_UsesDocked()
    {
        var s = new AppSettings { LargeDisplayMinWidth = 2560 };
        Assert.Same(s.Docked, s.ResolveProfile(2560));
    }

    [Fact]
    public void WidthBelowThreshold_UsesLaptop()
    {
        var s = new AppSettings { LargeDisplayMinWidth = 2560 };
        Assert.Same(s.Laptop, s.ResolveProfile(1920));
    }

    [Fact]
    public void WhenDisabled_AlwaysUsesDocked()
    {
        var s = new AppSettings { EnableDisplayProfiles = false };
        Assert.Same(s.Docked, s.ResolveProfile(800));
    }
}
