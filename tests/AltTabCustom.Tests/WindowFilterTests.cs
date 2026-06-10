using AltTabCustom.Core;
using AltTabCustom.Interop;
using Xunit;

namespace AltTabCustom.Tests;

public class WindowFilterTests
{
    private static WindowInfo W(int handle, string title, string process) =>
        new() { Handle = new IntPtr(handle), Title = title, ProcessName = process };

    private static readonly List<WindowInfo> Sample = new()
    {
        W(1, "Inbox — Outlook", "OUTLOOK"),
        W(2, "Calendar — Outlook", "OUTLOOK"),
        W(3, "GitHub — Chrome", "chrome"),
        W(4, "Docs — Chrome", "chrome"),
        W(5, "Program.cs — Code", "Code"),
    };

    [Fact]
    public void NoProcessOrText_ReturnsEverything()
    {
        var result = WindowFilter.Apply(Sample, processName: null, text: null);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void ProcessFilter_KeepsOnlyThatApp()
    {
        var result = WindowFilter.Apply(Sample, processName: "chrome", text: null);
        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.Equal("chrome", w.ProcessName));
    }

    [Fact]
    public void ProcessFilter_IsCaseInsensitive()
    {
        var result = WindowFilter.Apply(Sample, processName: "OuTlOoK", text: null);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ProcessAndText_CombineWithAnd()
    {
        // Within Outlook only, narrow to titles/process containing "calendar".
        var result = WindowFilter.Apply(Sample, processName: "OUTLOOK", text: "calendar");
        Assert.Single(result);
        Assert.Equal(new IntPtr(2), result[0].Handle);
    }

    [Fact]
    public void TextOnly_MatchesTitleOrProcessAcrossApps()
    {
        var result = WindowFilter.Apply(Sample, processName: null, text: "chrome");
        Assert.Equal(2, result.Count); // both matched on process name
    }

    [Fact]
    public void PreservesInputOrder()
    {
        var result = WindowFilter.Apply(Sample, processName: "OUTLOOK", text: null);
        Assert.Equal(new IntPtr(1), result[0].Handle);
        Assert.Equal(new IntPtr(2), result[1].Handle);
    }
}
