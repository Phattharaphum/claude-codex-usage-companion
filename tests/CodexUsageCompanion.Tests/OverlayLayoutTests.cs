using Avalonia;
using CodexUsageCompanion.Ui;
using Xunit;

namespace CodexUsageCompanion.Tests;

public sealed class OverlayLayoutTests
{
    [Fact]
    public void SmallQuotaDashboardKeepsItsCompactSize()
    {
        var size = OverlayLayout.FitWindow(320, new PixelSize(1920, 1040), 1, 16);

        Assert.Equal(new Size(440, 320), size);
    }

    [Fact]
    public void TallDashboardFitsWorkingAreaAtHighDpi()
    {
        var size = OverlayLayout.FitWindow(1000, new PixelSize(1280, 680), 1.5, 16);

        Assert.Equal(440, size.Width);
        Assert.Equal(680 / 1.5 - 32, size.Height);
        Assert.True(size.Height * 1.5 <= 680 - 48);
    }

    [Fact]
    public void NarrowMonitorKeepsWindowWithinBothScreenEdges()
    {
        var size = OverlayLayout.FitWindow(320, new PixelSize(800, 600), 2, 16);

        Assert.Equal(new Size(368, 268), size);
    }

    [Fact]
    public void EnlargingBottomOverlayKeepsItsCardsOnScreen()
    {
        var position = OverlayLayout.ConstrainPosition(new PixelPoint(1464, 704),
            new PixelRect(0, 0, 1920, 1040), new PixelSize(440, 800));

        Assert.Equal(new PixelPoint(1464, 240), position);
    }

    [Fact]
    public void PositionChosenByDraggingIsPreservedWhileWindowFits()
    {
        var position = OverlayLayout.ConstrainPosition(new PixelPoint(-1500, 250),
            new PixelRect(-1920, 0, 1920, 1080), new PixelSize(440, 480));

        Assert.Equal(new PixelPoint(-1500, 250), position);
    }

    [Fact]
    public void RemovedMonitorPositionIsMovedIntoRemainingWorkingArea()
    {
        var position = OverlayLayout.ConstrainPosition(new PixelPoint(-1500, -100),
            new PixelRect(0, 24, 1920, 1016), new PixelSize(440, 480));

        Assert.Equal(new PixelPoint(0, 24), position);
    }
}
