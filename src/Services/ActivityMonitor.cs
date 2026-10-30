using PulseWidget.Core;

namespace PulseWidget.Services;

public sealed class ActivityMonitor
{
    private readonly Dictionary<string, (DateTime Changed, WorkState State)> logs = new(StringComparer.OrdinalIgnoreCase);
    private readonly string dataDirectory;
    private readonly ClaudeDesktopUsage desktopUsage;
    private ProviderUsage? lastClaudeUsage;
    public ActivityMonitor(string? dataDirectory = null, IEnumerable<string>? desktopHistoryPaths = null)
    {
        this.dataDirectory = dataDirectory ?? Settings.DataDirectory;
        desktopUsage = new ClaudeDesktopUsage(desktopHistoryPaths);
    }

    public Activity ReadCodex(DateTimeOffset now)
    {
        var root = Path.Combine(Settings.CodexDirectory, "sessions");
        if (!Directory.Exists(root)) return new(WorkState.Unknown);
        try
        {
            var files = Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories)
                .Select(p => new FileInfo(p)).Where(f => now.UtcDateTime - f.LastWriteTimeUtc < TimeSpan.FromDays(2))
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(200).ToArray();
            var current = files.Select(f => f.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var key in logs.Keys.Where(k => !current.Contains(k)).ToArray()) logs.Remove(key);
            foreach (var file in files)
            {
                if (logs.TryGetValue(file.FullName, out var old) && old.Changed == file.LastWriteTimeUtc) continue;
                WorkState state = WorkState.Unknown;
                bool hasTerminal = false;
                foreach (var line in ReadTail(file.FullName))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var r = doc.RootElement;
                        if (!r.TryGetProperty("type", out var kind) || kind.GetString() != "event_msg" || !r.TryGetProperty("payload", out var payload) || !payload.TryGetProperty("type", out var t)) continue;
                        switch (t.GetString())
                        {
                            case "task_started": state = WorkState.Working; hasTerminal = false; break;
                            case "task_complete": case "task_completed": case "turn_aborted": state = WorkState.Idle; hasTerminal = true; break;
                            case "item_completed": case "token_count": if (!hasTerminal) state = WorkState.Working; break;
                        }
                    }
                    catch (JsonException) { }
                }
                logs[file.FullName] = (file.LastWriteTimeUtc, state);
            }
            var active = logs.Values.Count(v => v.State == WorkState.Working && now.UtcDateTime - v.Changed < TimeSpan.FromMinutes(15));
            if (active > 0) return new(WorkState.Working, active);
            // An abandoned or interrupted log is not proof of an idle agent.
            if (logs.Values.Any(v => v.State == WorkState.Working)) return new(WorkState.Unknown);
            return new(WorkState.Idle);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(WorkState.Unknown); }
    }

    public static IEnumerable<string> ReadTail(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        bool truncated = stream.Length > 262144;
        if (truncated) stream.Seek(-262144, SeekOrigin.End);
        using var reader = new StreamReader(stream);
        if (truncated) reader.ReadLine();
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines;
    }

    public Activity ReadClaude(DateTimeOffset now)
    {
        var root = Path.Combine(dataDirectory, "claude", "sessions");
        if (!Directory.Exists(root)) return new(WorkState.Unknown);
        int working = 0, waiting = 0; bool any = false, uncertain = false;
        foreach (var path in Directory.EnumerateFiles(root, "*.json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var r = doc.RootElement;
                if (!r.TryGetProperty("updatedAt", out var u) || !u.TryGetDateTimeOffset(out var updated) || now - updated > TimeSpan.FromDays(1)) continue;
                any = true;
                var state = r.GetProperty("state").GetString();
                if (state == "working" && now - updated < TimeSpan.FromMinutes(15)) working++;
                else if (state == "waiting") waiting++;
                else if (state == "working") uncertain = true;
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException) { }
        }
        return working > 0 ? new(WorkState.Working, working) : waiting > 0 ? new(WorkState.Waiting, waiting) : new(uncertain || !any ? WorkState.Unknown : WorkState.Idle);
    }

    public ProviderUsage ReadClaudeUsage()
    {
        if (desktopUsage.Read(DateTimeOffset.UtcNow) is { } desktop) return desktop;
        var path = Path.Combine(dataDirectory, "claude", "usage.json");
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var usage = UsageParser.Claude(doc.RootElement, DateTimeOffset.UtcNow);
            if (usage.Weekly is not null || usage.FiveHour is not null) return lastClaudeUsage = usage;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException) { }
        if (lastClaudeUsage is not null) return lastClaudeUsage with { Message = "캐시 읽기 실패 · 마지막 수신값을 유지합니다." };
        return ProviderUsage.Empty(ReadClaude(DateTimeOffset.UtcNow).State != WorkState.Unknown
            ? "작업 상태만 연결됨 · Desktop 사용량 기록 대기\nClaude Desktop의 사용량 메뉴를 열어 확인하세요."
            : "Desktop 사용량 기록 대기\nClaude Desktop에 로그인하고 사용량을 확인하세요.");
    }
}
