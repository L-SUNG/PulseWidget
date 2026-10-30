using System.Runtime.InteropServices;

namespace PulseWidget.Services;

public sealed class SystemMonitor
{
    private ulong previousIdle, previousKernel, previousUser;
    public record Snapshot(double? Cpu, double Memory, double UsedGb, double TotalGb);
    public Snapshot Read()
    {
        double? cpu = null;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            if (previousKernel != 0 && kernel >= previousKernel && user >= previousUser && idle >= previousIdle)
            {
                var total = kernel - previousKernel + user - previousUser;
                if (total > 0) cpu = Math.Clamp(100d * (total - Math.Min(total, idle - previousIdle)) / total, 0, 100);
            }
            previousIdle = idle; previousKernel = kernel; previousUser = user;
        }
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory)) throw new InvalidOperationException("메모리 사용량을 읽을 수 없습니다.");
        return new(cpu, memory.Load, (memory.TotalPhysical - memory.AvailablePhysical) / 1073741824d, memory.TotalPhysical / 1073741824d);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual; }
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll")][return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
