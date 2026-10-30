namespace PulseWidget.Core;

public static class ClaudeDesktopHistory
{
    // Desktop's plan-usage-history v2 maps five_hour -> fh, seven_day -> sd.
    // These are percentages, not token counts or fractions. Reset times are not stored.
    public static ProviderUsage? Parse(JsonElement root, DateTimeOffset now)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("version", out var v) ||
            v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version is not (1 or 2) ||
            !root.TryGetProperty("samples", out var samples) || samples.ValueKind != JsonValueKind.Array) return null;
        JsonElement latest = default;
        DateTimeOffset? observed = null;
        foreach (var sample in samples.EnumerateArray())
        {
            if (sample.ValueKind != JsonValueKind.Object || !sample.TryGetProperty("t", out var time) ||
                time.ValueKind != JsonValueKind.Number || !time.TryGetInt64(out var milliseconds)) continue;
            DateTimeOffset at;
            try { at = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds); }
            catch (ArgumentOutOfRangeException) { continue; }
            if (at > now.AddMinutes(2) || at < DateTimeOffset.UnixEpoch) continue;
            if (observed is null || at >= observed) { latest = sample; observed = at; }
        }
        if (observed is null) return null;
        // Use one complete snapshot from the last observed organization. Never fill
        // missing fields from older samples, which may belong to a different account.
        var values = latest;
        if (version == 2 && (!latest.TryGetProperty("u", out values) || values.ValueKind != JsonValueKind.Object))
            return new(null, null, observed, "Desktop 최신 기록에 사용량이 없습니다.");
        UsageWindow? Read(string key) => values.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number &&
            p.TryGetDouble(out var used) && double.IsFinite(used) && used >= 0 && used <= 100
            ? new(used, null, observed) : null;
        var week = Read("sd"); var five = Read("fh");
        return new(week, five, observed, week is null && five is null ? "Desktop 최신 기록에 사용량이 없습니다."
            : "Claude Desktop 마지막 기록 · 초기화 시각 미제공");
    }
}
