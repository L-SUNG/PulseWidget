using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using PulseWidget.Core;
using PulseWidget.Native;

// Explicit opt-in integration check: only the specified widget receives fake
// notifications. This does not connect/disconnect, lock or unlock Windows.
internal static class LiveRecoveryCheck
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Taskbar.Rect rect);

    internal static int Run(string executable)
    {
        try
        {
            var receiver = FindWindow(null, SessionRecovery.WindowTitle);
            Require(receiver != IntPtr.Zero, "live session receiver exists");
            GetWindowThreadProcessId(receiver, out uint pid);
            using var old = Process.GetProcessById((int)pid);
            string expectedPath = Path.GetFullPath(executable);
            Require(string.Equals(old.MainModule!.FileName, expectedPath, StringComparison.OrdinalIgnoreCase), "receiver belongs to the explicitly selected widget executable");
            string settings = File.ReadAllText(Settings.SettingsPath);
            using var startup = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            var run = startup?.GetValue("PulseWidget");
            var oldWidget = FindWidget(); GetWindowRect(oldWidget, out var before);
            var session = new IntPtr(old.SessionId);
            Console.WriteLine("Simulating notification delivery to Pulse only; Windows session is unchanged.");
            PostMessage(receiver, 0x02B1, new IntPtr(4), session);
            PostMessage(receiver, 0x007E, IntPtr.Zero, IntPtr.Zero);
            foreach (int change in new[] { 1, 8, 15 }) PostMessage(receiver, 0x02B1, new IntPtr(change), session);
            Require(old.WaitForExit(12000), "old widget exits after the event burst");
            IntPtr widget = IntPtr.Zero;
            var timeout = Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < 12000)
            {
                widget = FindWidget(); GetWindowThreadProcessId(widget, out var currentPid);
                if (widget != IntPtr.Zero && currentPid != pid && IsWindowVisible(widget)) break;
                Thread.Sleep(50);
            }
            Require(widget != IntPtr.Zero && IsWindowVisible(widget), "replacement widget becomes visible");
            GetWindowThreadProcessId(widget, out var nextPid);
            using var next = Process.GetProcessById((int)nextPid);
            Require(nextPid != pid && string.Equals(next.MainModule!.FileName, expectedPath, StringComparison.OrdinalIgnoreCase), "recovery starts the same installed executable");
            Thread.Sleep(5000); // Ensure no replay/restart loop after initialization.
            Require(!next.HasExited, "replacement remains running without a recovery loop");
            var siblings = Process.GetProcessesByName("PulseWidget");
            try { Require(siblings.Count(p => string.Equals(p.MainModule!.FileName, expectedPath, StringComparison.OrdinalIgnoreCase)) == 1, "exactly one installed widget remains"); }
            finally { foreach (var process in siblings) process.Dispose(); }
            GetWindowRect(widget, out var after);
            Require(before.Left == after.Left && before.Top == after.Top && before.Right == after.Right && before.Bottom == after.Bottom, "window position and size survive recovery");
            Require(settings == File.ReadAllText(Settings.SettingsPath), "all saved settings remain byte-for-byte unchanged");
            Require(Equals(run, startup?.GetValue("PulseWidget")), "Windows automatic startup entry remains unchanged");
            receiver = FindWindow(null, SessionRecovery.WindowTitle); GetWindowThreadProcessId(receiver, out var listenerPid);
            Require(listenerPid == nextPid, "replacement registers its own session listener");
            Console.WriteLine($"Recovered PID {pid} -> {nextPid}; Hosted={Taskbar.GetParent(widget) == Taskbar.Handle}; Bounds={after.Left},{after.Top},{after.Right},{after.Bottom}");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static IntPtr FindWidget()
    {
        var window = FindWindow(null, "Pulse Widget");
        return window != IntPtr.Zero ? window : FindWindowEx(Taskbar.Handle, IntPtr.Zero, null, "Pulse Widget");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL " + message);
        Console.WriteLine("PASS " + message);
    }
}
