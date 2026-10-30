using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Receives Claude's stdin locally. Store only quota numbers, timestamps and state;
// never retain prompts, transcripts, tokens, working directories or tool inputs.
try
{
    using var doc = JsonDocument.Parse(Console.In.ReadToEnd());
    var root = doc.RootElement;
    string directory = Environment.GetEnvironmentVariable("PULSE_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PulseWidget");
    string claude = Path.Combine(directory, "claude");
    Directory.CreateDirectory(Path.Combine(claude, "sessions"));
    string session = root.TryGetProperty("session_id", out var s) ? s.GetString() ?? "unknown" : "unknown";
    string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session)))[..24];
    string statePath = Path.Combine(claude, "sessions", key + ".json");
    string mode = args.FirstOrDefault() ?? "hook";
    if (mode == "statusline")
    {
        var receivedAt = DateTimeOffset.UtcNow;
        var filtered = ReadWindows(root, receivedAt, fromCache: false);
        // A missing rate_limits field before the first response must not erase a
        // valid snapshot received from another concurrent Claude session.
        if (filtered.Count > 0)
        {
            using var mutex = new Mutex(false, "Local\\PulseWidget.ClaudeUsage");
            bool held = false;
            try
            {
                try { held = mutex.WaitOne(1500); } catch (AbandonedMutexException) { held = true; }
                if (held)
                {
                    string usagePath = Path.Combine(claude, "usage.json");
                    var merged = new JsonObject();
                    try
                    {
                        using var previous = JsonDocument.Parse(File.ReadAllText(usagePath));
                        var oldTime = ReadTime(previous.RootElement, File.GetLastWriteTimeUtc(usagePath));
                        merged = ReadWindows(previous.RootElement, oldTime, fromCache: true);
                    }
                    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
                    foreach (var entry in filtered) merged[entry.Key] = entry.Value!.DeepClone();
                    AtomicWrite(usagePath, new JsonObject { ["updatedAt"] = receivedAt, ["rate_limits"] = merged });
                }
            }
            finally { if (held) mutex.ReleaseMutex(); }
        }
        string Format(string name) => filtered[name]?["used_percentage"] is JsonValue value && value.TryGetValue<double>(out var p) ? $"{p:0}%" : "—";
        Console.Write($"Claude · 주간 {Format("seven_day")} · 5h {Format("five_hour")}");
    }
    else
    {
        string eventName = root.TryGetProperty("hook_event_name", out var e) ? e.GetString() ?? "" : "";
        string? state = eventName switch
        {
            "UserPromptSubmit" or "PreToolUse" or "PostToolUse" or "PostToolUseFailure" => "working",
            "PermissionRequest" => "waiting",
            "Notification" when root.TryGetProperty("notification_type", out var n) && n.GetString() == "permission_prompt" => "waiting",
            "Stop" or "StopFailure" or "SessionEnd" or "SessionStart" => "idle",
            _ => null
        };
        if (state is not null) AtomicWrite(statePath, new JsonObject { ["state"] = state, ["updatedAt"] = DateTimeOffset.UtcNow });
    }
}
catch { /* Monitoring must never block or fail a Claude turn. */ }

static DateTimeOffset ReadTime(JsonElement root, DateTimeOffset fallback) =>
    root.ValueKind == JsonValueKind.Object && root.TryGetProperty("updatedAt", out var t) &&
    t.ValueKind == JsonValueKind.String && t.TryGetDateTimeOffset(out var at) ? at : fallback;

static JsonObject ReadWindows(JsonElement root, DateTimeOffset receivedAt, bool fromCache)
{
    var result = new JsonObject();
    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("rate_limits", out var limits) || limits.ValueKind != JsonValueKind.Object) return result;
    foreach (string name in new[] { "five_hour", "seven_day" })
    {
        if (!limits.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object ||
            !window.TryGetProperty("used_percentage", out var pct) || pct.ValueKind != JsonValueKind.Number ||
            !pct.TryGetDouble(out var used) || !double.IsFinite(used)) continue;
        var clean = new JsonObject { ["used_percentage"] = Math.Clamp(used, 0, 100),
            ["updatedAt"] = fromCache ? ReadTime(window, receivedAt) : receivedAt };
        if (window.TryGetProperty("resets_at", out var reset) && reset.ValueKind == JsonValueKind.Number && reset.TryGetInt64(out var seconds))
        {
            try { DateTimeOffset.FromUnixTimeSeconds(seconds); clean["resets_at"] = seconds; }
            catch (ArgumentOutOfRangeException) { }
        }
        result[name] = clean;
    }
    return result;
}

static void AtomicWrite(string path, JsonNode data)
{
    string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try { File.WriteAllText(temporary, data.ToJsonString()); File.Move(temporary, path, true); }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
}
