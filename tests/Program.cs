global using System.Text.Json;
using PulseWidget.Core;
using PulseWidget.Services;

int passed = 0;
void Test(string name, Action test) { test(); Console.WriteLine("PASS " + name); passed++; }
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
ProviderUsage Codex(string json) { using var d = JsonDocument.Parse(json); return UsageParser.Codex(d.RootElement, DateTimeOffset.UtcNow); }
ProviderUsage Claude(string json) { using var d = JsonDocument.Parse(json); return UsageParser.Claude(d.RootElement, DateTimeOffset.UtcNow); }
ProviderUsage? Desktop(string json) { using var d = JsonDocument.Parse(json); return ClaudeDesktopHistory.Parse(d.RootElement, DateTimeOffset.UtcNow); }
Test("Shared bucket and weekly duration win over map order", () => {
    var p = Codex("""{"rateLimitsByLimitId":{"other":{"primary":{"usedPercent":99,"windowDurationMins":10080}},"codex":{"primary":{"usedPercent":31,"windowDurationMins":10080},"secondary":{"usedPercent":7,"windowDurationMins":300}}}}""");
    Check(p.Weekly?.Used == 31 && p.Weekly.Remaining == 69 && p.FiveHour?.Used == 7);
});
Test("Legacy bucket with null optional values", () => {
    var p = Codex("""{"rateLimits":{"limitId":null,"primary":{"usedPercent":null,"windowDurationMins":300,"resetsAt":null},"secondary":{"usedPercent":0,"windowDurationMins":10080,"resetsAt":null}}}""");
    Check(p.Weekly?.Used == 0 && p.Weekly.Remaining == 100 && p.FiveHour is null);
});
Test("Missing weekly cannot be replaced by a 5 hour quota", () => Check(Codex("""{"rateLimits":{"primary":{"usedPercent":20,"windowDurationMins":300},"secondary":null}}""").Weekly is null));
Test("Unknown bucket does not become global usage", () => Check(Codex("""{"rateLimits":{"limitId":"other","primary":{"usedPercent":20,"windowDurationMins":10080}}}""").Weekly is null));
Test("Null duration and malformed percentages remain unknown", () => Check(Codex("""{"rateLimits":{"primary":{"usedPercent":"20","windowDurationMins":10080},"secondary":{"usedPercent":10,"windowDurationMins":null}}}""").Weekly is null));
Test("Claude independent windows and fractional quotas", () => {
    var p = Claude("""{"rate_limits":{"five_hour":{"used_percentage":23.5,"resets_at":2000000000},"seven_day":{"used_percentage":41.2,"resets_at":2000600000}}}""");
    Check(p.FiveHour?.Used == 23.5 && Math.Abs(p.Weekly!.Remaining - 58.8) < .001);
});
Test("Missing Claude window is unknown, not zero", () => { var p = Claude("""{"rate_limits":{"five_hour":null}}"""); Check(p.Weekly is null && p.FiveHour is null); });
Test("Null Claude fields do not throw", () => Check(Claude("""{"updatedAt":null,"rate_limits":{"seven_day":{"used_percentage":null,"resets_at":null}}}""").Weekly is null));
Test("Reset windows never display inferred fresh quotas", () => { var w = new UsageWindow(95, DateTimeOffset.UtcNow.AddMinutes(-1)); Check(w.Compact(DateTimeOffset.UtcNow) == "—"); });
Test("Out of bounds usage is clamped", () => { var p = Claude("""{"rate_limits":{"seven_day":{"used_percentage":120},"five_hour":{"used_percentage":-10}}}"""); Check(p.Weekly?.Used == 100 && p.Weekly.Remaining == 0 && p.FiveHour?.Used == 0); });
Test("Stale usage retains observation timestamp", () => { var p = Claude("""{"updatedAt":"2020-01-01T00:00:00Z","rate_limits":{"seven_day":{"used_percentage":15}}}"""); Check(p.IsStale(DateTimeOffset.UtcNow)); });
Test("Fresh short window does not make cached weekly data fresh", () => {
    var p = Claude(JsonSerializer.Serialize(new { updatedAt = DateTimeOffset.UtcNow, rate_limits = new {
        seven_day = new { used_percentage = 15, updatedAt = "2020-01-01T00:00:00Z" },
        five_hour = new { used_percentage = 5, updatedAt = DateTimeOffset.UtcNow } } }));
    Check(!p.IsStale(DateTimeOffset.UtcNow) && p.Weekly!.IsStale(DateTimeOffset.UtcNow, p.UpdatedAt) && !p.FiveHour!.IsStale(DateTimeOffset.UtcNow, p.UpdatedAt));
});
Test("Desktop v2 percentages are not multiplied by 100", () => {
    var p = Desktop("""{"version":2,"samples":[{"t":1793155495481,"org":"fixture-a","u":{"fh":4,"sd":2,"so":99}}]}""");
    Check(p?.Weekly?.Used == 2 && p.Weekly.Remaining == 98 && p.FiveHour?.Used == 4 && p.FiveHour.Remaining == 96);
    Check(p!.UpdatedAt == DateTimeOffset.FromUnixTimeMilliseconds(1793155495481) && p.Weekly!.ObservedAt == p.UpdatedAt && p.Weekly.ResetsAt is null);
});
Test("Desktop v1 zero and fractional percentages are valid", () => {
    var p = Desktop("""{"version":1,"samples":[{"t":1793155495481,"fh":0,"sd":2.5}]}""");
    Check(p?.FiveHour?.Used == 0 && p.FiveHour.Remaining == 100 && p.Weekly?.Used == 2.5);
});
Test("Newest Desktop snapshot wins regardless of order and never mixes organizations", () => {
    var p = Desktop("""{"version":2,"samples":[{"t":1793155495481,"org":"fixture-b","u":{"fh":7}},{"t":1793154595483,"org":"fixture-a","u":{"fh":90,"sd":80}}]}""");
    Check(p?.FiveHour?.Used == 7 && p.Weekly is null);
});
Test("Empty latest Desktop snapshot cannot revive another account's quota", () => {
    var p = Desktop("""{"version":2,"samples":[{"t":1793154595483,"org":"fixture-a","u":{"fh":90,"sd":80}},{"t":1793155495481,"org":"fixture-b","u":{}}]}""");
    Check(p is not null && p.FiveHour is null && p.Weekly is null && p.UpdatedAt == DateTimeOffset.FromUnixTimeMilliseconds(1793155495481));
});
Test("Desktop rejects malformed values and unknown formats", () => {
    Check(Desktop("""{"version":"2","samples":[]}""") is null && Desktop("""{"version":3,"samples":[]}""") is null);
    var p = Desktop("""{"version":2,"samples":[{"t":1793155495481,"u":{"fh":101,"sd":"2"}}]}""");
    Check(p is not null && p.FiveHour is null && p.Weekly is null);
});
Test("Future or invalid Desktop timestamps do not become fresh readings", () => {
    var p = Desktop("""{"version":2,"samples":[{"t":9223372036854775807,"u":{"fh":20}},{"t":4102444800000,"u":{"fh":30}}]}""");
    Check(p is null);
});

var temporary = Path.Combine(Path.GetTempPath(), "PulseTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(temporary, "sessions"));
var oldHome = Environment.GetEnvironmentVariable("CODEX_HOME");
Environment.SetEnvironmentVariable("CODEX_HOME", temporary);
try
{
    var claudeRoot = Path.Combine(temporary, "claude");
    Directory.CreateDirectory(Path.Combine(claudeRoot, "sessions"));
    var claudeUsage = Path.Combine(claudeRoot, "usage.json");
    Test("Hooks alone are reported as activity-only connection", () => {
        File.WriteAllText(Path.Combine(claudeRoot, "sessions", "fixture.json"), JsonSerializer.Serialize(new { state = "idle", updatedAt = DateTimeOffset.UtcNow }));
        Check(new ActivityMonitor(temporary, Array.Empty<string>()).ReadClaudeUsage().Message.Contains("작업 상태만 연결됨"));
    });
    Test("Widget restart restores saved quota without a new Claude turn", () => {
        File.WriteAllText(claudeUsage, """{"updatedAt":"2026-10-27T01:00:00Z","rate_limits":{"seven_day":{"used_percentage":41.2,"resets_at":2000600000},"five_hour":{"used_percentage":23.5,"resets_at":2000000000}}}""");
        var first = new ActivityMonitor(temporary, Array.Empty<string>()).ReadClaudeUsage();
        var restarted = new ActivityMonitor(temporary, Array.Empty<string>()).ReadClaudeUsage();
        Check(first == restarted && restarted.Weekly?.Used == 41.2 && restarted.FiveHour?.Used == 23.5);
    });
    Test("Temporary cache read failure retains last values and timestamp", () => {
        var reader = new ActivityMonitor(temporary, Array.Empty<string>()); var before = reader.ReadClaudeUsage();
        File.WriteAllText(claudeUsage, "{invalid"); var after = reader.ReadClaudeUsage();
        Check(after.Weekly == before.Weekly && after.FiveHour == before.FiveHour && after.UpdatedAt == before.UpdatedAt);
    });
    var historyPath = Path.Combine(temporary, "plan-usage-history.json");
    Test("Desktop usage loads on startup without bridge or new inference", () => {
        File.WriteAllText(historyPath, """{"version":2,"samples":[{"t":1793155495481,"u":{"fh":4,"sd":2}}]}""");
        var first = new ActivityMonitor(temporary, new[] { historyPath }).ReadClaudeUsage();
        var restarted = new ActivityMonitor(temporary, new[] { historyPath }).ReadClaudeUsage();
        Check(first == restarted && restarted.Weekly?.Used == 2 && restarted.FiveHour?.Used == 4 && restarted.Message.Contains("Desktop"));
    });
    Test("Desktop file updates are picked up and partial writes retain old observation time", () => {
        var reader = new ClaudeDesktopUsage(new[] { historyPath }); var first = reader.Read(DateTimeOffset.UtcNow)!;
        File.WriteAllText(historyPath, "{partial"); var partial = reader.Read(DateTimeOffset.UtcNow)!;
        Check(partial.Weekly == first.Weekly && partial.UpdatedAt == first.UpdatedAt && partial.Message.Contains("실패"));
        File.WriteAllText(historyPath, """{"version":2,"samples":[{"t":1793155495482,"u":{"fh":5,"sd":3}}]}""");
        var updated = reader.Read(DateTimeOffset.UtcNow)!;
        Check(updated.Weekly?.Used == 3 && updated.FiveHour?.Used == 5 && updated.UpdatedAt > first.UpdatedAt);
    });
    var monitor = new ActivityMonitor();
    var a = Path.Combine(temporary, "sessions", "a.jsonl");
    var b = Path.Combine(temporary, "sessions", "b.jsonl");
    string Event(string type) => JsonSerializer.Serialize(new { type = "event_msg", payload = new { type } }) + "\n";
    Test("Concurrent Codex sessions remain working when one completes", () => {
        File.WriteAllText(a, Event("task_started") + Event("task_complete")); File.WriteAllText(b, Event("task_started"));
        Check(monitor.ReadCodex(DateTimeOffset.UtcNow) is { State: WorkState.Working, Count: 1 });
    });
    Test("Terminal event survives trailing token statistics", () => {
        File.AppendAllText(b, Event("turn_aborted") + Event("token_count")); File.SetLastWriteTimeUtc(b, DateTime.UtcNow.AddSeconds(1));
        Check(monitor.ReadCodex(DateTimeOffset.UtcNow).State == WorkState.Idle);
    });
    Test("Truncated final log line does not break monitoring", () => {
        File.WriteAllText(b, Event("task_started") + "{\"type\":"); File.SetLastWriteTimeUtc(b, DateTime.UtcNow.AddSeconds(2));
        Check(monitor.ReadCodex(DateTimeOffset.UtcNow).State == WorkState.Working);
    });
    Test("Abandoned sessions become unknown instead of eternal working", () => {
        File.SetLastWriteTimeUtc(b, DateTime.UtcNow.AddMinutes(-20));
        Check(monitor.ReadCodex(DateTimeOffset.UtcNow).State == WorkState.Unknown);
    });
    Test("Tail reader skips a partial giant JSON line", () => {
        File.WriteAllText(b, new string('x', 300000) + "\n" + Event("task_complete")); File.SetLastWriteTimeUtc(b, DateTime.UtcNow.AddSeconds(3));
        Check(monitor.ReadCodex(DateTimeOffset.UtcNow).State == WorkState.Idle);
    });
}
finally
{
    Environment.SetEnvironmentVariable("CODEX_HOME", oldHome);
    Directory.Delete(temporary, recursive: true);
}
Test("Native PC counters are within physical bounds", () => {
    var m = new SystemMonitor(); m.Read(); Thread.Sleep(120); var s = m.Read();
    Check(s.Cpu is >= 0 and <= 100 && s.Memory is >= 0 and <= 100 && s.UsedGb <= s.TotalGb && s.TotalGb > 0);
});
Test("Legacy settings keep primary-only display and existing theme", () => {
    var s = JsonSerializer.Deserialize<Settings>("""{"Offset":882,"ThemeId":"apple-dark"}""")!; s.Normalize();
    Check(!s.ShowOnAllDisplays && s.DisplayDeviceName is null && s.Offset == 882 && s.ResolveTheme().Id == "apple-dark");
});
Test("Custom colors survive serialization and preserve selected display", () => {
    var s = new Settings { ThemeId = CustomTheme.Id, ShowOnAllDisplays = true, DisplayDeviceName = @"\\.\DISPLAY2", Offset = 882,
        CustomTheme = new() { BaseThemeId = "apple-dark", Colors = new() { ["Surface"] = "#123456", ["Claude"] = "#fedcba" } } };
    var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s))!; restored.Normalize();
    Check(restored.ResolveTheme().Id == CustomTheme.Id && restored.ResolveTheme().Surface == "#123456" && restored.ResolveTheme().Claude == "#FEDCBA");
    Check(restored.ResolveTheme().WidgetRadius == ThemeCatalog.Find("apple-dark").WidgetRadius && restored.ShowOnAllDisplays && restored.DisplayDeviceName == s.DisplayDeviceName && restored.Offset == 882);
});
Test("Malformed custom colors fall back independently without losing other settings", () => {
    var s = JsonSerializer.Deserialize<Settings>("""{"ThemeId":"custom","Offset":500,"CustomTheme":{"BaseThemeId":"paper","Colors":{"Surface":"transparent","Text":"#112233","Panel":"#00112233","Accent":null,"FontSize":"20"}}}""")!; s.Normalize();
    var t = s.ResolveTheme(); Check(t.Surface == ThemeCatalog.Find("paper").Surface && t.Text == "#112233" && s.Offset == 500 && s.CustomTheme.Colors.Count == 1);
    var missing = JsonSerializer.Deserialize<Settings>("""{"ThemeId":"custom","CustomTheme":null}""")!; missing.Normalize(); Check(missing.ResolveTheme().Id == "custom");
});
Test("Editing a custom draft cannot mutate saved colors before apply", () => {
    var original = new CustomTheme { Colors = new() { ["Surface"] = "#123456" } }; var draft = original.Clone(); draft.Colors["Surface"] = "#ABCDEF";
    Check(original.Resolve().Surface == "#123456" && draft.Resolve().Surface == "#ABCDEF");
});
Test("Single display selects the requested monitor regardless of enumeration order", () => {
    var selected = DisplaySelection.Select(new[] { "left", "primary", "right" }, "primary", "right", false); Check(selected.SequenceEqual(new[] { "right" }));
});
Test("All displays produces one target per connected monitor", () => {
    var selected = DisplaySelection.Select(new[] { "left", "primary", "RIGHT", "right" }, "primary", "left", true); Check(selected.Count == 3 && selected.Contains("primary"));
});
Test("Disconnected preferred monitor temporarily falls back, reconnect restores choice", () => {
    const string preferred = "left";
    Check(DisplaySelection.Select(new[] { "primary", "right" }, "primary", preferred, false).Single() == "primary");
    Check(DisplaySelection.Select(new[] { "left", "primary", "right" }, "primary", preferred, false).Single() == preferred);
    Check(DisplaySelection.Select(Array.Empty<string>(), null, preferred, true).Count == 0);
});
Test("Percentage edges and center align at different resolutions and DPI scales", () => {
    foreach (var (screen, widget) in new[] { (1920, 546), (2560, 682), (3840, 1092) }) {
        Check(HorizontalPosition.ToPixels(screen, widget, 0) == 0);
        Check(HorizontalPosition.ToPixels(screen, widget, 100) + widget == screen);
        Check(Math.Abs(HorizontalPosition.ToPixels(screen, widget, 50) + widget / 2.0 - screen / 2.0) <= .5);
        foreach (double percent in new[] { 12.5, 25, 75, 87.5 })
            Check(Math.Abs(HorizontalPosition.ToPixels(screen, widget, percent) - (screen - widget) * percent / 100) <= .5);
    }
});
Test("Narrow displays and out-of-range percentages cannot place widget off the left edge", () => {
    Check(HorizontalPosition.ToPixels(400, 546, 100) == 0 && HorizontalPosition.ToPixels(546, 546, 50) == 0);
    Check(HorizontalPosition.ToPixels(1920, 546, -25) == 0 && HorizontalPosition.ToPixels(1920, 546, 250) == 1374);
    Check(HorizontalPosition.ToPixels(1920, 546, double.NaN) == 0);
});
Test("Legacy DIP position migrates once and preserves its physical position at 125 percent DPI", () => {
    var s = new Settings { Offset = 274.4954860149543, Scale = 1, ShowOnAllDisplays = true };
    Check(s.MigratePosition(2880, 1.25));
    var percent = s.PositionPercent!.Value;
    Check(HorizontalPosition.ToPixels(2880, 682, percent) == 343);
    Check(!s.MigratePosition(1920, 1) && s.PositionPercent == percent);
    Check(HorizontalPosition.ToPixels(1920, 546, percent) != (int)s.Offset);
});
Test("Legacy offsets beyond the screen migrate to the corresponding edge", () => {
    var s = new Settings { Offset = 5000 }; s.MigratePosition(1920, 1); Check(s.PositionPercent == 100);
    var narrow = new Settings { Offset = 100 }; narrow.MigratePosition(400, 1); Check(narrow.PositionPercent == 0);
});
Test("Saved percentage takes precedence over legacy pixels across restarts", () => {
    var s = new Settings { Offset = 164, PositionPercent = 73.25, ThemeId = "claude-dark", ShowOnAllDisplays = true };
    var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s))!; restored.Normalize();
    Check(!restored.MigratePosition(3840, 2) && restored.PositionPercent == 73.25 && restored.ShowOnAllDisplays && restored.ThemeId == "claude-dark");
});
Test("Invalid percentage normalization retains the other preferences", () => {
    var s = new Settings { PositionPercent = 120, ThemeId = "apple-dark" }; s.Normalize(); Check(s.PositionPercent == 100 && s.ThemeId == "apple-dark");
    s.PositionPercent = double.NaN; s.Normalize(); Check(s.PositionPercent is null && s.ThemeId == "apple-dark");
});
Console.WriteLine($"{passed} tests passed.");
