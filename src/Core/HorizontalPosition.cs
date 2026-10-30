namespace PulseWidget.Core;

public static class HorizontalPosition
{
    public const double DefaultPercent = 10;
    public static double Clamp(double percent) => double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : 0;

    // The percentage describes the available travel, so 50% centers the widget
    // and 100% keeps its right edge inside the display, including at mixed DPI.
    public static int ToPixels(int displayWidth, int widgetWidth, double percent) =>
        (int)Math.Round(Math.Max(0, displayWidth - widgetWidth) * Clamp(percent) / 100);

    public static double FromPixels(int displayWidth, int widgetWidth, double offset)
    {
        int travel = Math.Max(0, displayWidth - widgetWidth);
        return travel == 0 || !double.IsFinite(offset) ? 0 : Clamp(offset / travel * 100);
    }
}
