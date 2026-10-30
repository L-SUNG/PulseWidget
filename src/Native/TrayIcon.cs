using System.Drawing;
using System.Runtime.InteropServices;

namespace PulseWidget.Native;
public static class TrayIcon
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    public static Icon Create(string surface = "#181D27", string accent = "#9DE9D3")
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(ColorTranslator.FromHtml(surface));
            using var pen = new Pen(ColorTranslator.FromHtml(accent), 3);
            g.DrawLines(pen, new[] { new Point(3, 18), new Point(9, 18), new Point(13, 7), new Point(19, 26), new Point(23, 14), new Point(29, 14) });
        }
        var handle = bitmap.GetHicon();
        try { using var original = Icon.FromHandle(handle); return (Icon)original.Clone(); }
        finally { DestroyIcon(handle); }
    }
}
