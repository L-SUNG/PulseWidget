using System.Windows.Forms;
namespace PulseWidget.Native;

internal static class Displays
{
    internal static Screen[] All => Screen.AllScreens.OrderByDescending(s => s.Primary).ThenBy(s => s.DeviceName, StringComparer.OrdinalIgnoreCase).ToArray();
    internal static Screen Resolve(string? name) => All.FirstOrDefault(s => string.Equals(s.DeviceName, name, StringComparison.OrdinalIgnoreCase)) ?? Screen.PrimaryScreen ?? All[0];
    internal static string Label(Screen screen)
    {
        int number = Array.FindIndex(All, s => s.DeviceName == screen.DeviceName) + 1;
        var primary = Screen.PrimaryScreen?.Bounds ?? screen.Bounds;
        string position = screen.Primary ? "주 화면" : screen.Bounds.Right <= primary.Left ? "왼쪽" : screen.Bounds.Left >= primary.Right ? "오른쪽" : screen.Bounds.Bottom <= primary.Top ? "위쪽" : "아래쪽";
        return $"디스플레이 {number} · {screen.Bounds.Width} × {screen.Bounds.Height} · {position}";
    }
}
