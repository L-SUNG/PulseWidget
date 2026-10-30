using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using PulseWidget.Native;

internal static class Program
{
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern void NotifyWinEvent(uint type, IntPtr window, int objectId, int childId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Taskbar.Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);

    [STAThread]
    private static int Main(string[] args)
    {
        int recoveryCheck = Array.IndexOf(args, "--exercise-live-recovery");
        if (recoveryCheck >= 0 && recoveryCheck + 1 < args.Length) return LiveRecoveryCheck.Run(args[recoveryCheck + 1]);
        if (Array.IndexOf(args, "--observe-live") >= 0) return ObserveLive();
        if (Array.IndexOf(args, "--host-window") >= 0)
        {
            var hostApp = new Application(); hostApp.Run(CreateWindow("Pulse test foreign taskbar")); return 0;
        }
        int exitCode = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int render = Array.IndexOf(args, "--render-ui");
        if (render >= 0 && render + 1 < args.Length) { UiFeatureTests.RenderPreviews(args[render + 1]); return 0; }
        app.Startup += async (_, _) =>
        {
            // These two tiny test windows stand in for the shell and widget.
            // No user taskbar, foreground window, settings or Explorer process is modified.
            var shell = CreateWindow("Pulse test taskbar");
            var overlay = CreateWindow("Pulse test overlay");
            TaskbarEvents? listener = null;
            try
            {
                PercentPositionTests.Run(Check);
                UiFeatureTests.Run(Check);
                await SessionRecoveryTests.Run(Check);
                shell.Show(); overlay.Show();
                var sh = new WindowInteropHelper(shell).Handle;
                var wh = new WindowInteropHelper(overlay).Handle;
                int events = 0;
                Action restore = () => { events++; Taskbar.PlaceNative(wh, sh, 20, 20, 100, 32, false); };
                restore();
                Check(Taskbar.IsAbove(wh, sh), "initial overlay order");
                var foreground = GetForegroundWindow();
                listener = new TaskbarEvents(wh, () => sh, restore, includeOwnProcess: true);

                for (int i = 0; i < 5; i++)
                {
                    SetWindowPos(sh, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                    Check(!Taskbar.IsAbove(wh, sh), "reproduce taskbar covering overlay");
                    var timer = Stopwatch.StartNew();
                    while (!Taskbar.IsAbove(wh, sh) && timer.ElapsedMilliseconds < 500) await Task.Delay(5);
                    Check(Taskbar.IsAbove(wh, sh), $"event restores layer without periodic timer ({timer.ElapsedMilliseconds} ms)");
                    Check(IsWindowVisible(wh), "overlay never hidden during reorder");
                    Check(GetForegroundWindow() == foreground, "layer restoration preserves keyboard focus");
                }
                Check(!Taskbar.PlaceNative(wh, sh, 20, 20, 100, 32, false), "unchanged layout performs no window update");
                Taskbar.PlaceNative(wh, sh, 20, 20, 100, 32, true);
                Check(!IsWindowVisible(wh), "intentional full-screen/auto-hide remains available");
                Taskbar.PlaceNative(wh, sh, 20, 20, 100, 32, false);
                Check(IsWindowVisible(wh), "overlay can be shown again");
                var screen = new Taskbar.Rect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
                var taskbarBounds = new Taskbar.Rect { Left = 0, Top = 1032, Right = 1920, Bottom = 1080 };
                var placement = Taskbar.GetPlacement(taskbarBounds, screen, 546, 42, 10, false, true, true);
                Taskbar.PlaceNative(wh, sh, placement.X, placement.Y, 546, 42, placement.Hidden);
                Check(IsWindowVisible(wh) && placement.Y == 1035, "monitor-sized foreground does not hide always-visible widget");
                var autoHidden = new Taskbar.Rect { Left = 0, Top = 1078, Right = 1920, Bottom = 1126 };
                placement = Taskbar.GetPlacement(autoHidden, screen, 546, 42, 10, false, true, false);
                Taskbar.PlaceNative(wh, sh, placement.X, placement.Y, 546, 42, placement.Hidden);
                Check(IsWindowVisible(wh) && placement.Y + 42 == screen.Bottom, "auto-hidden taskbar keeps widget fully inside screen");
                var optionalHide = Taskbar.GetPlacement(taskbarBounds, screen, 546, 42, 10, false, false, true);
                Check(optionalHide.Hidden, "user can opt back into full-screen hiding");
                Check(Taskbar.GetPlacement(autoHidden, screen, 546, 42, 10, false, false, false).Hidden, "optional hiding follows auto-hidden taskbar");
                var secondary = new Taskbar.Rect { Left = -1920, Top = -1080, Right = 0, Bottom = 0 };
                var secondaryBar = new Taskbar.Rect { Left = -1920, Top = -2, Right = 0, Bottom = 46 };
                placement = Taskbar.GetPlacement(secondaryBar, secondary, 546, 42, 100, false, true, true);
                Check(placement.X == -546 && placement.Y == -42 && !placement.Hidden, "always-visible placement respects negative monitor coordinates");
                placement = Taskbar.GetPlacement(secondaryBar, secondary, 546, 42, 50, false, true, false);
                Check(placement.X == -1233, "50 percent centers within a monitor with a negative origin");
                var right = new Taskbar.Rect { Left = 1920, Top = 0, Right = 5760, Bottom = 2160 };
                var rightBar = new Taskbar.Rect { Left = 1920, Top = 2064, Right = 5760, Bottom = 2160 };
                placement = Taskbar.GetPlacement(rightBar, right, 1092, 84, 50, false, true, false);
                Check(placement.X == 3294 && placement.Y == 2070, "4K monitor at 200 percent DPI retains the same center alignment");
                placement = Taskbar.GetPlacement(rightBar, right, 1092, 84, 100, true, true, false);
                Check(placement.X == 4668 && placement.Y == 1972, "100 percent aligns right edge in above-taskbar mode too");
                listener.Dispose(); listener = null;
                await Task.Delay(20);
                int before = events;
                NotifyWinEvent(0x8004, sh, 0, 0);
                await Task.Delay(50);
                Check(events == before, "closing removes native event hooks");
                var originalOwner = Taskbar.GetParent(wh);
                Check(Taskbar.SetHost(wh, sh), "widget can share taskbar parent layer");
                Taskbar.PlaceHosted(wh, sh, 20, 20, 100, 32, false);
                Check(Taskbar.GetParent(wh) == sh && IsWindowVisible(wh), "hosted widget remains visible");
                Check(!Taskbar.PlaceHosted(wh, sh, 20, 20, 100, 32, false), "unchanged hosted layout avoids redundant updates");
                SetWindowPos(sh, new IntPtr(-1), 30, 40, 0, 0, 0x0011);
                Taskbar.PlaceHosted(wh, sh, 30, 40, 100, 32, false);
                GetWindowRect(wh, out var hostedBounds);
                Check(hostedBounds.Left == 30 && hostedBounds.Top == 40, "hosted placement converts screen to parent coordinates");
                Taskbar.SetHost(wh, IntPtr.Zero);
                Taskbar.PlaceNative(wh, sh, 20, 20, 100, 32, false);
                Check(Taskbar.GetParent(wh) == originalOwner && IsWindowVisible(wh), "above-taskbar mode restores original WPF window ownership");
                Check(GetForegroundWindow() == foreground, "hosting does not steal keyboard focus");
                using var foreign = Process.Start(new ProcessStartInfo(System.IO.Path.Combine(AppContext.BaseDirectory, "PulseWidget.WindowTests.exe"), "--host-window") { UseShellExecute = false, CreateNoWindow = true })!;
                try
                {
                    IntPtr parent = IntPtr.Zero;
                    var wait = Stopwatch.StartNew();
                    while (parent == IntPtr.Zero && wait.ElapsedMilliseconds < 5000)
                    { await Task.Delay(50); parent = FindWindow(null, "Pulse test foreign taskbar"); }
                    Check(parent != IntPtr.Zero && Taskbar.SetHost(wh, parent), "can attach to a separate taskbar process");
                    Taskbar.PlaceHosted(wh, parent, 20, 20, 100, 32, false);
                    var source = HwndSource.FromHwnd(wh);
                    foreign.Kill(); await foreign.WaitForExitAsync(); await Task.Delay(100);
                    Check(!Taskbar.IsAlive(wh), "foreign taskbar exit can be detected even without native notification");
                    source.Dispose();
                    Taskbar.ForgetHost(wh);
                    overlay = CreateWindow("Pulse test replacement overlay"); overlay.Show(); wh = new WindowInteropHelper(overlay).Handle;
                    Check(Taskbar.SetHost(wh, sh), "widget can reattach after taskbar recreation");
                    Taskbar.PlaceHosted(wh, sh, 30, 40, 100, 32, false);
                    Check(IsWindowVisible(wh), "reattached widget is visible");
                    Taskbar.SetHost(wh, IntPtr.Zero);
                }
                finally { if (!foreign.HasExited) foreign.Kill(); }
                Console.WriteLine("Window regression tests passed."); exitCode = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { listener?.Dispose(); overlay.Close(); shell.Close(); app.Shutdown(); }
        };
        app.Run(); return exitCode;
    }
    // Read-only observation while a human or Computer Use opens the real Start
    // menu. This does not activate windows or send any input.
    private static int ObserveLive()
    {
        string? previous = null;
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(25))
        {
            var widget = FindWindow(null, "Pulse Widget");
            if (widget == IntPtr.Zero) widget = FindWindowEx(Taskbar.Handle, IntPtr.Zero, null, "Pulse Widget");
            var foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out var processId);
            string process;
            try { using var p = Process.GetProcessById((int)processId); process = p.ProcessName; }
            catch { process = "unknown"; }
            GetWindowRect(widget, out var rect);
            GetWindowRect(Taskbar.Handle, out var barRect);
            var hit = WindowFromPoint(new Point { X = (rect.Left + rect.Right) / 2, Y = (rect.Top + rect.Bottom) / 2 });
            GetWindowThreadProcessId(hit, out var hitId);
            string covering;
            try { using var p = Process.GetProcessById((int)hitId); covering = p.ProcessName; }
            catch { covering = "unknown"; }
            var wc = GetWindowDpiAwarenessContext(widget); var tc = GetWindowDpiAwarenessContext(Taskbar.Handle);
            string state = $"WidgetVisible={IsWindowVisible(widget)} Foreground={process} AtWidgetCenter={covering} Hosted={Taskbar.GetParent(widget) == Taskbar.Handle} AboveTaskbar={Taskbar.IsAbove(widget, Taskbar.Handle)} Dpi={wc}/{tc} Awareness={GetAwarenessFromDpiAwarenessContext(wc)}/{GetAwarenessFromDpiAwarenessContext(tc)} Bounds={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}";
            state += $" TaskbarBounds={barRect.Left},{barRect.Top},{barRect.Right},{barRect.Bottom}";
            if (state != previous) { Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {state}"); previous = state; }
            Thread.Sleep(100);
        }
        return 0;
    }
    private static Window CreateWindow(string name) => new()
    {
        Title = name, Width = 100, Height = 32, Left = 20, Top = 20,
        WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Topmost = true, Opacity = .01
    };
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL " + message);
        Console.WriteLine("PASS " + message);
    }
}
