using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PulseWidget.Native;

// Keep a hidden top-level receiver: the visible widget is an Explorer child
// and does not reliably receive display/composition broadcast messages.
internal sealed class SessionRecovery : IDisposable
{
    internal const string WindowTitle = "Pulse session recovery";
    private const int SessionChanged = 0x02B1, DisplayChanged = 0x007E,
        CompositionChanged = 0x031E, PowerBroadcast = 0x0218;
    private readonly HwndSource source;
    private readonly DispatcherTimer settle, registration;
    private readonly Func<string, bool> recover;
    private readonly int sessionId;
    private string reason = "";
    private bool blocked, disposed;
    internal bool IsRegistered { get; private set; }
    internal IntPtr Handle => source.Handle;

    internal SessionRecovery(Func<string, bool> recover, TimeSpan? settleDelay = null)
    {
        this.recover = recover;
        using var process = Process.GetCurrentProcess(); sessionId = process.SessionId;
        source = new HwndSource(new HwndSourceParameters(WindowTitle)
        {
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP, no WS_VISIBLE
            ExtendedWindowStyle = 0x08000080, // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            Width = 0, Height = 0
        });
        settle = new DispatcherTimer(DispatcherPriority.Background, source.Dispatcher)
            { Interval = settleDelay ?? TimeSpan.FromSeconds(2) };
        registration = new DispatcherTimer(DispatcherPriority.Background, source.Dispatcher)
            { Interval = TimeSpan.FromSeconds(5) };
        settle.Tick += Recover;
        registration.Tick += (_, _) => Register();
        source.AddHook(OnMessage);
        Register();
    }

    private void Register()
    {
        if (disposed || IsRegistered) return;
        IsRegistered = WTSRegisterSessionNotification(source.Handle, 0); // this session only
        // RDS dependencies may still be starting during Windows logon.
        if (IsRegistered) registration.Stop(); else registration.Start();
    }

    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (disposed) return IntPtr.Zero;
        long change = wParam.ToInt64();
        if (message == SessionChanged && lParam.ToInt64() == sessionId)
        {
            switch (change)
            {
                case 2: case 4: case 6: case 7: // disconnect, logoff, lock
                    blocked = true; settle.Stop(); reason = ""; break;
                case 1: case 3: case 5: case 8: case 15: // connect, logon, unlock, desktop ready
                    blocked = false; Request($"session-{change}"); break;
                case 9: Request("remote-control"); break;
            }
        }
        else if (message == DisplayChanged) Request("display-change");
        else if (message == CompositionChanged) Request("composition-change");
        else if (message == PowerBroadcast)
        {
            if (change == 4) { blocked = true; settle.Stop(); reason = ""; } // suspend
            else if (change is 7 or 18) { blocked = false; Request("resume"); }
        }
        return IntPtr.Zero; // Never consume Windows' own handling.
    }

    private void Request(string trigger)
    {
        if (blocked) return;
        reason = trigger;
        settle.Stop(); settle.Start(); // One recovery after the event burst settles.
    }

    private void Recover(object? sender, EventArgs args)
    {
        settle.Stop();
        if (disposed || blocked || reason.Length == 0) return;
        if (recover(reason)) reason = "";
        else if (!disposed) settle.Start(); // A failed launch must not abandon recovery.
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; settle.Stop(); registration.Stop();
        if (IsRegistered) WTSUnRegisterSessionNotification(source.Handle);
        IsRegistered = false;
        source.RemoveHook(OnMessage); source.Dispose();
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSRegisterSessionNotification(IntPtr window, uint flags);
    [DllImport("wtsapi32.dll")]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr window);
}
