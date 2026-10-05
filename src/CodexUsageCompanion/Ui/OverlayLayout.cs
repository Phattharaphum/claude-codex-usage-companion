using Avalonia;

namespace CodexUsageCompanion.Ui;

internal static class OverlayLayout
{
    internal static Size FitWindow(double contentHeight, PixelSize workingArea, double scaling, int margin)
    {
        var availableWidth = Math.Max(1, workingArea.Width / scaling - 2 * margin);
        var availableHeight = Math.Max(1, workingArea.Height / scaling - 2 * margin);
        return new Size(Math.Min(440, availableWidth), Math.Min(contentHeight, availableHeight));
    }

    internal static PixelPoint ConstrainPosition(PixelPoint position, PixelRect workingArea, PixelSize size) =>
        new(Math.Clamp(position.X, workingArea.X, Math.Max(workingArea.X, workingArea.Right - size.Width)),
            Math.Clamp(position.Y, workingArea.Y, Math.Max(workingArea.Y, workingArea.Bottom - size.Height)));
}
