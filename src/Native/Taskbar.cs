using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using PulseWidget.Core;

namespace PulseWidget.Native;

public static class Taskbar
{
    private static readonly Dictionary<IntPtr, (long Style, IntPtr Owner)> hostOrigins = new();
    private static readonly Dictionary<string, double> fallbackScales = new();
    private static string hostStatus = "unattached", lastTrace = "";
    private const uint NoSize = 0x0001, NoMove = 0x0002, NoZOrder = 0x0004,
        NoActivate = 0x0010, ShowWindow = 0x0040, HideWindow = 0x0080, NoOwnerZOrder = 0x0200;
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? title);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "IsWindow")] internal static extern bool IsAlive(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr window, IntPtr parent);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int maxCount);
    internal static IntPtr Handle => FindWindow("Shell_TrayWnd", null);
    internal static IntPtr HandleFor(string? deviceName)
    {
        string name = Displays.Resolve(deviceName).DeviceName;
        foreach (string className in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
            for (var h = FindWindowEx(IntPtr.Zero, IntPtr.Zero, className, null); h != IntPtr.Zero; h = FindWindowEx(IntPtr.Zero, h, className, null))
                if (string.Equals(System.Windows.Forms.Screen.FromHandle(h).DeviceName, name, StringComparison.OrdinalIgnoreCase)) return h;
        return IntPtr.Zero;
    }
    internal static void ForgetHost(IntPtr window) => hostOrigins.Remove(window);
    public static IDisposable Watch(Window window, Action refresh, string? deviceName = null) =>
        new TaskbarEvents(new WindowInteropHelper(window).Handle, () => HandleFor(deviceName), refresh);
    public static (Rect Bounds, double Scale) Bounds(string? deviceName = null)
    {
        var screen = Displays.Resolve(deviceName);
        var handle = HandleFor(screen.DeviceName);
        double scale = Math.Max(96, GetDpiForWindow(handle)) / 96d;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var bounds))
        {
            if (!fallbackScales.TryGetValue(screen.DeviceName, out scale))
            {
                // A hidden window gives GetDpiForWindow the destination monitor's DPI,
                // even when Windows does not show a taskbar on that monitor.
                using var probe = new HwndSource(new HwndSourceParameters("Pulse DPI probe")
                { PositionX = screen.Bounds.Left + 1, PositionY = screen.Bounds.Top + 1, Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000080 });
                scale = Math.Max(96, GetDpiForWindow(probe.Handle)) / 96d;
                fallbackScales[screen.DeviceName] = scale;
            }
            bounds = new() { Left = screen.Bounds.Left, Right = screen.Bounds.Right, Top = screen.Bounds.Bottom - (int)Math.Round(48 * scale), Bottom = screen.Bounds.Bottom };
        }
        return (bounds, scale);
    }
    public static void Setup(Window window)
    {
        var h = new WindowInteropHelper(window).Handle;
        SetWindowLongPtr(h, -20, new IntPtr(GetWindowLongPtr(h, -20).ToInt64() | 0x80 | 0x08000000)); // tool window, no activate
    }
    public static void Place(Window window, Settings settings, string? deviceName = null)
    {
        var (r, dpi) = Bounds(deviceName);
        var targetScreen = Displays.Resolve(deviceName);
        // WPF feeds rounded native sizes back into Width/Height (e.g. 41.6 or
        // 42.4 at 125%). Always place from the canonical size, not that feedback.
        int w = (int)Math.Round(546 * settings.Scale * dpi), h = (int)Math.Round(42 * settings.Scale * dpi);
        // Start/search shell surfaces can have monitor-sized bounds. In always
        // visible mode, their activation must never hide the widget.
        var foreground = GetForegroundWindow();
        bool fullscreen = false;
        if (!settings.AlwaysVisible && foreground != IntPtr.Zero)
        {
            var className = new StringBuilder(256); GetClassName(foreground, className, className.Capacity);
            if (className.ToString() is not ("Progman" or "WorkerW" or "Shell_TrayWnd") && GetWindowRect(foreground, out var f))
            {
                var monitor = System.Windows.Forms.Screen.FromHandle(foreground).Bounds;
                fullscreen = monitor == targetScreen.Bounds && f.Left <= monitor.Left && f.Top <= monitor.Top && f.Right >= monitor.Right && f.Bottom >= monitor.Bottom;
            }
        }
        var screen = targetScreen.Bounds;
        var placement = GetPlacement(r, new Rect { Left = screen.Left, Top = screen.Top, Right = screen.Right, Bottom = screen.Bottom },
            w, h, settings.PositionPercent ?? HorizontalPosition.DefaultPercent, settings.AboveTaskbar, settings.AlwaysVisible, fullscreen);
        var handle = new WindowInteropHelper(window).Handle;
        var taskbar = HandleFor(deviceName);
        bool host = !settings.AboveTaskbar && taskbar != IntPtr.Zero && IsWindowVisible(taskbar) &&
            placement.Y >= r.Top && placement.Y + h <= r.Bottom && r.Top < screen.Bottom - 3;
        if (SetHost(handle, host ? taskbar : IntPtr.Zero))
            PlaceHosted(handle, taskbar, placement.X, placement.Y, w, h, placement.Hidden);
        else PlaceNative(handle, taskbar, placement.X, placement.Y, w, h, placement.Hidden);
        if (Environment.GetCommandLineArgs().Contains("--trace-window"))
        {
            string trace = $"Device={targetScreen.DeviceName} HostRequested={host} Status={hostStatus} Parent={GetParent(handle)} Taskbar={taskbar} Hidden={placement.Hidden} Logical={window.Width:R},{window.Height:R} Target={placement.X},{placement.Y},{w},{h}";
            if (trace != lastTrace)
            {
                lastTrace = trace;
                try { Directory.CreateDirectory(Settings.DataDirectory); File.AppendAllText(Path.Combine(Settings.DataDirectory, "window-host.log"), $"{DateTimeOffset.Now:O} {trace}\n"); } catch (IOException) { }
            }
        }
    }

    internal static void PositionPopup(Window popup, Window anchor, string deviceName)
    {
        var screen = Displays.Resolve(deviceName).Bounds;
        var (bar, dpi) = Bounds(deviceName);
        popup.MaxHeight = Math.Max(100, screen.Height / dpi - 20);
        popup.MaxWidth = Math.Max(100, screen.Width / dpi - 20);
        int width = (int)Math.Round(Math.Min(popup.Width, popup.MaxWidth) * dpi), height = (int)Math.Round(Math.Min(popup.Height, popup.MaxHeight) * dpi);
        GetWindowRect(new WindowInteropHelper(anchor).Handle, out var widget);
        int x = Math.Clamp(widget.Left, screen.Left + 8, Math.Max(screen.Left + 8, screen.Right - width - 8));
        int y = Math.Max(screen.Top + 8, Math.Min(widget.Top, bar.Top) - height - 10);
        SetWindowPos(new WindowInteropHelper(popup).Handle, new IntPtr(-1), x, y, width, height, NoActivate | NoOwnerZOrder);
    }

    // A taskbar child shares Explorer's shell layer when Start/search raises the
    // taskbar above ordinary topmost windows. Only our own HWND is modified.
    internal static bool SetHost(IntPtr window, IntPtr parent)
    {
        const long child = 0x40000000L, popup = 0x80000000L;
        long style = GetWindowLongPtr(window, -16).ToInt64();
        bool isChild = (style & child) != 0;
        if (parent == IntPtr.Zero)
        {
            if (isChild)
            {
                SetParent(window, IntPtr.Zero);
                var original = hostOrigins.GetValueOrDefault(window, ((style & ~child) | popup, IntPtr.Zero));
                SetWindowLongPtr(window, -16, new IntPtr(original.Item1));
                SetWindowLongPtr(window, -8, original.Item2);
                hostOrigins.Remove(window);
                SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, NoMove | NoSize | NoActivate | NoOwnerZOrder | 0x0020);
            }
            return false;
        }
        if (isChild && GetParent(window) == parent) return true;
        // Do not trigger Windows' cross-process DPI-awareness reset.
        if (!AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(window), GetWindowDpiAwarenessContext(parent)))
        { hostStatus = "DPI mismatch"; SetHost(window, IntPtr.Zero); return false; }
        if (!isChild) hostOrigins[window] = (style, GetWindow(window, 4));
        SetWindowLongPtr(window, -16, new IntPtr((style & ~popup) | child));
        SetParent(window, parent);
        int error = Marshal.GetLastWin32Error();
        if (GetParent(window) != parent)
        { hostStatus = $"SetParent failed: {error}"; SetWindowLongPtr(window, -16, new IntPtr(style)); if (!isChild) hostOrigins.Remove(window); return false; }
        hostStatus = "attached";
        SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, NoMove | NoSize | NoActivate | NoOwnerZOrder | 0x0020);
        return true;
    }

    internal static bool PlaceHosted(IntPtr window, IntPtr parent, int x, int y, int width, int height, bool hidden)
    {
        if (hidden) return PlaceNative(window, parent, x, y, width, height, true);
        GetWindowRect(window, out var current);
        bool moved = current.Left != x || current.Top != y;
        bool resized = current.Right - current.Left != width || current.Bottom - current.Top != height;
        bool ordered = GetWindow(window, 3) == IntPtr.Zero;
        bool visible = IsWindowVisible(window);
        if (visible && !moved && !resized && ordered) return false;
        var point = new Point { X = x, Y = y }; ScreenToClient(parent, ref point);
        uint flags = NoActivate | NoOwnerZOrder;
        if (!moved) flags |= NoMove;
        if (!resized) flags |= NoSize;
        if (ordered) flags |= NoZOrder;
        if (!visible) flags |= ShowWindow;
        return SetWindowPos(window, IntPtr.Zero, point.X, point.Y, width, height, flags);
    }

    internal static (int X, int Y, bool Hidden) GetPlacement(Rect taskbar, Rect screen, int width, int height,
        double percent, bool aboveTaskbar, bool alwaysVisible, bool fullscreen)
    {
        int x = screen.Left + HorizontalPosition.ToPixels(screen.Right - screen.Left, width, percent);
        int y = aboveTaskbar ? taskbar.Top - height - 8 : taskbar.Top + Math.Max(0, (taskbar.Bottom - taskbar.Top - height) / 2);
        bool autoHidden = taskbar.Top >= screen.Bottom - 3;
        if (alwaysVisible) y = Math.Clamp(y, screen.Top, Math.Max(screen.Top, screen.Bottom - height));
        return (x, y, !alwaysVisible && (fullscreen || autoHidden));
    }

    // Keep the overlay above the taskbar without activating it or raising its
    // owner/popups. Do not repeatedly show/resize an already-correct window.
    internal static bool PlaceNative(IntPtr handle, IntPtr taskbar, int x, int y, int w, int h, bool hidden)
    {
        bool visible = IsWindowVisible(handle);
        if (hidden)
        {
            if (!visible) return false;
            return SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, NoActivate | NoOwnerZOrder | NoMove | NoSize | NoZOrder | HideWindow);
        }
        GetWindowRect(handle, out var current);
        bool moved = current.Left != x || current.Top != y;
        bool resized = current.Right - current.Left != w || current.Bottom - current.Top != h;
        bool topmost = (GetWindowLongPtr(handle, -20).ToInt64() & 0x8) != 0;
        bool ordered = topmost && (taskbar == IntPtr.Zero || IsAbove(handle, taskbar));
        if (visible && !moved && !resized && ordered) return false;
        uint flags = NoActivate | NoOwnerZOrder;
        if (!moved) flags |= NoMove;
        if (!resized) flags |= NoSize;
        if (ordered) flags |= NoZOrder;
        if (!visible) flags |= ShowWindow;
        return SetWindowPos(handle, new IntPtr(-1), x, y, w, h, flags);
    }

    internal static bool IsAbove(IntPtr window, IntPtr other)
    {
        // Walk only the windows above the taskbar, not the entire desktop tree.
        var seen = new HashSet<IntPtr>();
        for (var current = GetWindow(other, 3); current != IntPtr.Zero && seen.Add(current); current = GetWindow(current, 3))
            if (current == window) return true;
        return false;
    }
}
