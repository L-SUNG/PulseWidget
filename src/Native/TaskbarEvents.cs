using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PulseWidget.Native;

// Out-of-context WinEvents run on the registering WPF UI thread. No Explorer
// injection, foreign window ownership, or faster polling is required.
internal sealed class TaskbarEvents : IDisposable
{
    private const uint Foreground = 0x0003, ObjectShow = 0x8002, ObjectReorder = 0x8004, ObjectLocationChange = 0x800B;
    private readonly IntPtr widget;
    private readonly Func<IntPtr> getTaskbar;
    private readonly Action refresh;
    private readonly WinEventProc callback;
    private readonly List<IntPtr> hooks = new();
    private bool refreshing, disposed;

    internal TaskbarEvents(IntPtr widget, Func<IntPtr> getTaskbar, Action refresh, bool includeOwnProcess = false)
    {
        this.widget = widget; this.getTaskbar = getTaskbar; this.refresh = refresh;
        callback = OnEvent; // Keep the unmanaged callback alive for the hook lifetime.
        try
        {
            Register(Foreground, Foreground, includeOwnProcess);
            Register(ObjectShow, ObjectReorder, includeOwnProcess);
            Register(ObjectLocationChange, ObjectLocationChange, includeOwnProcess);
        }
        catch { Dispose(); throw; }
    }
    private void Register(uint first, uint last, bool includeOwnProcess)
    {
        var hook = SetWinEventHook(first, last, IntPtr.Zero, callback, 0, 0, includeOwnProcess ? 0u : 2u);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "작업 표시줄 표시 상태를 감시할 수 없습니다.");
        hooks.Add(hook);
    }
    private void OnEvent(IntPtr hook, uint type, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if (disposed || refreshing || window == IntPtr.Zero || window == widget) return;
        if (type != Foreground)
        {
            if (childId != 0 || (objectId != 0 && objectId != -4)) return;
            var taskbar = getTaskbar();
            bool shellChanged = window == taskbar || (taskbar != IntPtr.Zero && IsChild(taskbar, window));
            // Z-order changes can be reported on the desktop container instead
            // of the particular taskbar window being raised.
            bool desktopReordered = type == ObjectReorder && window == GetDesktopWindow();
            if (!shellChanged && !desktopReordered) return;
        }
        refreshing = true;
        try { refresh(); }
        finally { refreshing = false; }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var hook in hooks) UnhookWinEvent(hook);
        hooks.Clear();
        GC.KeepAlive(callback);
    }
    private delegate void WinEventProc(IntPtr hook, uint type, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
}
