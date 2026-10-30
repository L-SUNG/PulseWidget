namespace PulseWidget.Core;

public record UsageWindow(double Used, DateTimeOffset? ResetsAt, DateTimeOffset? ObservedAt = null)
{
    public double Remaining => Math.Clamp(100 - Used, 0, 100);
    public bool IsExpired(DateTimeOffset now) => ResetsAt is { } reset && reset <= now;
    public bool IsStale(DateTimeOffset now, DateTimeOffset? fallback) => (ObservedAt ?? fallback) is not { } at || now - at > TimeSpan.FromMinutes(10);
    public string Compact(DateTimeOffset now) => IsExpired(now) ? "—" : $"{Used:0}%";
    public string Pair(DateTimeOffset now) => IsExpired(now) ? "— / —" : $"{Used:0}% / {Remaining:0}%";
}

public record ProviderUsage(UsageWindow? Weekly, UsageWindow? FiveHour, DateTimeOffset? UpdatedAt, string Message)
{
    public static ProviderUsage Empty(string message) => new(null, null, null, message);
    public bool IsStale(DateTimeOffset now) => UpdatedAt is null || now - UpdatedAt > TimeSpan.FromMinutes(10);
}

public enum WorkState { Unknown, Idle, Working, Waiting }
public record Activity(WorkState State, int Count = 0)
{
    public string Label => State switch { WorkState.Working => Count > 1 ? $"작업중 {Count}" : "작업중", WorkState.Waiting => "승인 대기", WorkState.Idle => "대기중", _ => "확인 필요" };
}

public static class UsageParser
{
    public static ProviderUsage Codex(JsonElement root, DateTimeOffset now)
    {
        if (root.TryGetProperty("result", out var result)) root = result;
        JsonElement bucket = default;
        // Never silently substitute a model-specific bucket for the shared Codex quota.
        if (root.TryGetProperty("rateLimitsByLimitId", out var map) && map.ValueKind == JsonValueKind.Object)
            map.TryGetProperty("codex", out bucket);
        if (bucket.ValueKind != JsonValueKind.Object && root.TryGetProperty("rateLimits", out var legacy))
        {
            if (!legacy.TryGetProperty("limitId", out var id) || id.ValueKind == JsonValueKind.Null || id.GetString() == "codex") bucket = legacy;
        }
        UsageWindow? week = null, shortWindow = null;
        if (bucket.ValueKind == JsonValueKind.Object)
            foreach (var name in new[] { "primary", "secondary" })
                if (bucket.TryGetProperty(name, out var window) && window.ValueKind == JsonValueKind.Object &&
                    window.TryGetProperty("windowDurationMins", out var duration) && duration.ValueKind == JsonValueKind.Number && duration.TryGetInt32(out var mins))
                {
                    if (mins == 10080) week = ReadWindow(window, "usedPercent", "resetsAt");
                    if (mins == 300) shortWindow = ReadWindow(window, "usedPercent", "resetsAt");
                }
        return new(week, shortWindow, now, week is null ? "이 계정의 주간 한도 정보가 제공되지 않았습니다." : "Codex 계정에서 갱신");
    }

    public static ProviderUsage Claude(JsonElement root, DateTimeOffset now)
    {
        var updated = root.TryGetProperty("updatedAt", out var t) && t.ValueKind == JsonValueKind.String && t.TryGetDateTimeOffset(out var date) ? date : now;
        if (!root.TryGetProperty("rate_limits", out var limits) || limits.ValueKind != JsonValueKind.Object)
            return ProviderUsage.Empty("저장된 사용량이 없습니다. 상태줄 연동을 확인하세요.");
        UsageWindow? Read(string key)
        {
            if (!limits.TryGetProperty(key, out var w) || ReadWindow(w, "used_percentage", "resets_at") is not { } value) return null;
            var observed = w.TryGetProperty("updatedAt", out var u) && u.ValueKind == JsonValueKind.String && u.TryGetDateTimeOffset(out var at) ? at : updated;
            return value with { ObservedAt = observed };
        }
        var week = Read("seven_day"); var five = Read("five_hour");
        return week is null && five is null ? ProviderUsage.Empty("저장된 사용량이 없습니다. 상태줄 연동을 확인하세요.")
            : new(week, five, updated, "상태줄 수신값 · 재실행 후에도 유지됩니다.");
    }

    private static UsageWindow? ReadWindow(JsonElement w, string usedKey, string resetKey)
    {
        if (w.ValueKind != JsonValueKind.Object || !w.TryGetProperty(usedKey, out var used) ||
            used.ValueKind != JsonValueKind.Number || !used.TryGetDouble(out var pct) || !double.IsFinite(pct)) return null;
        DateTimeOffset? reset = null;
        if (w.TryGetProperty(resetKey, out var r) && r.ValueKind == JsonValueKind.Number && r.TryGetInt64(out var seconds))
            try { reset = DateTimeOffset.FromUnixTimeSeconds(seconds); } catch (ArgumentOutOfRangeException) { }
        return new(Math.Clamp(pct, 0, 100), reset);
    }
}
