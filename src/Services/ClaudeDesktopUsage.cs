using PulseWidget.Core;

namespace PulseWidget.Services;

public sealed class ClaudeDesktopUsage
{
    private readonly string[] paths;
    private readonly Dictionary<string, ProviderUsage> lastRead = new(StringComparer.OrdinalIgnoreCase);

    public ClaudeDesktopUsage(IEnumerable<string>? historyPaths = null) => paths = (historyPaths ?? FindHistoryPaths()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IEnumerable<string> FindHistoryPaths()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "plan-usage-history.json");
        // Store/MSIX installations redirect the Desktop app's roaming data here.
        string packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        string[] installed;
        try { installed = Directory.Exists(packages) ? Directory.GetDirectories(packages, "Claude_*") : Array.Empty<string>(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { installed = Array.Empty<string>(); }
        foreach (var package in installed) yield return Path.Combine(package, "LocalCache", "Roaming", "Claude", "plan-usage-history.json");
    }

    public ProviderUsage? Read(DateTimeOffset now)
    {
        foreach (string path in paths)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length > 4 * 1024 * 1024) throw new IOException("History exceeds size limit.");
                using var doc = JsonDocument.Parse(stream);
                var usage = ClaudeDesktopHistory.Parse(doc.RootElement, now);
                if (usage is null) throw new JsonException("Unsupported or empty history.");
                lastRead[path] = usage;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                if (lastRead.TryGetValue(path, out var previous))
                    lastRead[path] = previous with { Message = "Desktop 기록 읽기 실패 · 마지막 수신값 유지" };
            }
        }
        return lastRead.Values.OrderByDescending(value => value.UpdatedAt).FirstOrDefault();
    }
}
