using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PulseWidget.Native;

internal static class SessionRecoveryTests
{
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);

    internal static async Task Run(Action<bool, string> check)
    {
        using var process = Process.GetCurrentProcess();
        var session = new IntPtr(process.SessionId);
        int recovered = 0;
        string last = "";
        using var monitor = new SessionRecovery(reason => { last = reason; recovered++; return true; }, TimeSpan.FromMilliseconds(120));
        check(monitor.IsRegistered, "registers native notifications for this Windows session");
        void Session(int change) => PostMessage(monitor.Handle, 0x02B1, new IntPtr(change), session);
        void Display() => PostMessage(monitor.Handle, 0x007E, IntPtr.Zero, IntPtr.Zero);

        PostMessage(monitor.Handle, 0x02B1, new IntPtr(8), new IntPtr(process.SessionId + 1));
        await Task.Delay(220);
        check(recovered == 0, "another user's session does not restart the widget");
        Session(4); Display(); await Task.Delay(220);
        check(recovered == 0, "remote disconnect waits for reconnection instead of restarting");
        Session(3); Display(); Session(8); Session(15);
        await Until(() => recovered == 1);
        await Task.Delay(220);
        check(recovered == 1 && last == "session-15", "remote reconnect burst produces one delayed recovery");

        Session(8); Session(7); Display(); await Task.Delay(220);
        check(recovered == 1, "locking cancels a pending recovery and ignores display changes");
        Session(1); Session(8); await Until(() => recovered == 2);
        check(last == "session-8", "return to local console and unlock recover without an HWND failure");
        Display(); PostMessage(monitor.Handle, 0x031E, IntPtr.Zero, IntPtr.Zero);
        await Until(() => recovered == 3);
        check(last == "composition-change", "hidden top-level receiver handles display/composition changes");

        PostMessage(monitor.Handle, 0x0218, new IntPtr(4), IntPtr.Zero);
        Display(); await Task.Delay(220);
        check(recovered == 3, "suspend suppresses recovery while the display is unavailable");
        PostMessage(monitor.Handle, 0x0218, new IntPtr(18), IntPtr.Zero);
        PostMessage(monitor.Handle, 0x0218, new IntPtr(7), IntPtr.Zero);
        await Until(() => recovered == 4);
        check(last == "resume", "resume notifications are coalesced into one recovery");

        Session(8); await Task.Delay(20);
        var handle = monitor.Handle; monitor.Dispose(); await Task.Delay(220);
        check(recovered == 4 && !IsWindow(handle) && !monitor.IsRegistered, "dispose cancels recovery, unregisters notifications and destroys receiver");

        int attempts = 0;
        using var retry = new SessionRecovery(_ => ++attempts >= 2, TimeSpan.FromMilliseconds(80));
        PostMessage(retry.Handle, 0x02B1, new IntPtr(8), session);
        await Until(() => attempts >= 2); await Task.Delay(200);
        check(attempts == 2, "failed recovery retries, then stops after success");
    }

    private static async Task Until(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition() && timeout.ElapsedMilliseconds < 3000) await Task.Delay(15);
        if (!condition()) throw new Exception("Session recovery callback timed out");
    }
}
