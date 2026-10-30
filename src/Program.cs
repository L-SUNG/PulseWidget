using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using PulseWidget.Core;
using PulseWidget.Services;

namespace PulseWidget;
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        int recoverIndex = Array.IndexOf(args, "--recover-after");
        if (recoverIndex >= 0 && recoverIndex + 1 < args.Length && int.TryParse(args[recoverIndex + 1], out var previousId))
        {
            try { using var previous = System.Diagnostics.Process.GetProcessById(previousId); if (!previous.WaitForExit(10000)) return 1; }
            catch (ArgumentException) { } // Previous widget has already exited.
        }
        if (args.Contains("--diagnose")) return DiagnoseAsync(args).GetAwaiter().GetResult();
        using var singleton = new Mutex(true, "Local\\PulseWidget.UI", out bool first);
        if (!first) return 0;
        // The tiny, mostly static layered widget does not need a GPU surface.
        // Avoid stale hardware render targets across RDP/local adapter switches.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Directory.CreateDirectory(Settings.DataDirectory);
            File.AppendAllText(Path.Combine(Settings.DataDirectory, "errors.log"), $"{DateTimeOffset.Now:O} {e.Exception.GetType().Name}: {e.Exception.Message}\n");
            e.Handled = true;
        };
        using var controller = new WidgetController();
        app.Startup += (_, _) => controller.Start();
        app.Run(); return 0;
    }
    private static async Task<int> DiagnoseAsync(string[] args)
    {
        using var client = new CodexClient();
        var monitor = new SystemMonitor(); monitor.Read(); await Task.Delay(600);
        ProviderUsage usage;
        try { usage = await client.ReadAsync(Settings.Load().CodexPath); }
        catch (Exception e) { usage = ProviderUsage.Empty(e is TimeoutException ? "timeout" : e.Message); }
        var activity = new ActivityMonitor();
        var output = new { OpenAI = usage, Claude = activity.ReadClaudeUsage(), OpenAIActivity = activity.ReadCodex(DateTimeOffset.UtcNow), ClaudeActivity = activity.ReadClaude(DateTimeOffset.UtcNow), PC = monitor.Read(), Taskbar = Native.Taskbar.Bounds() };
        int index = Array.IndexOf(args, "--output");
        string path = index >= 0 && args.Length > index + 1 ? args[index + 1] : Path.Combine(Settings.DataDirectory, "diagnostics.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        return usage.UpdatedAt is null ? 1 : 0;
    }
}
