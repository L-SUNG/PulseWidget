using System.Collections.Concurrent;
using System.Diagnostics;
using PulseWidget.Core;

namespace PulseWidget.Services;

public sealed class CodexClient : IDisposable
{
    private Process? process;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> pending = new();
    private int nextId;
    private readonly SemaphoreSlim gate = new(1);
    private bool initialized;
    private bool disposed;

    public async Task<ProviderUsage> ReadAsync(string? configuredPath)
    {
        await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (process is null || process.HasExited)
            {
                StopProcess();
                var exe = Locate(configuredPath) ?? throw new InvalidOperationException("Codex 실행 파일을 찾을 수 없습니다. 설정에서 경로를 선택하세요.");
                process = new Process { StartInfo = new(exe) { Arguments = "app-server --listen stdio://", UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
                process.Start();
                var running = process;
                _ = DrainAsync(running);
                _ = DrainErrorsAsync(running);
                initialized = false;
            }
            if (!initialized)
            {
                await RequestAsync("initialize", new { clientInfo = new { name = "pulse_widget", title = "Pulse Widget", version = "1.0.0" }, capabilities = new { experimentalApi = false } });
                await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\"}");
                initialized = true;
            }
            var response = await RequestAsync("account/rateLimits/read", null);
            return UsageParser.Codex(response, DateTimeOffset.UtcNow);
        }
        catch { StopProcess(); throw; }
        finally { gate.Release(); }
    }

    private async Task<JsonElement> RequestAsync(string method, object? parameters)
    {
        int id = Interlocked.Increment(ref nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id] = completion;
        try
        {
            await process!.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters }));
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally { pending.TryRemove(id, out _); }
    }

    private async Task DrainAsync(Process running)
    {
        try
        {
            while (await running.StandardOutput.ReadLineAsync() is { } line)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("id", out var token) || !token.TryGetInt32(out var id) || !pending.TryRemove(id, out var request)) continue;
                    if (root.TryGetProperty("error", out _)) request.TrySetException(new InvalidOperationException("Codex 사용량 조회 실패 — Codex 로그인 상태를 확인하세요."));
                    else if (root.TryGetProperty("result", out var result)) request.TrySetResult(result.Clone());
                }
                catch (JsonException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException) { }
        finally
        {
            if (ReferenceEquals(process, running))
                foreach (var pair in pending) if (pending.TryRemove(pair.Key, out var request)) request.TrySetException(new IOException("Codex 연결이 종료되었습니다."));
        }
    }
    private static async Task DrainErrorsAsync(Process process)
    {
        try { while (await process.StandardError.ReadLineAsync() is not null) { } } catch { }
    }

    public static string? Locate(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (File.Exists(Path.Combine(dir, "codex.exe"))) return Path.Combine(dir, "codex.exe");
        var bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(bundled))
            return Directory.EnumerateFiles(bundled, "codex.exe", SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        return null;
    }
    private void StopProcess()
    {
        try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); } catch { }
        process?.Dispose(); process = null; initialized = false;
    }
    public void Dispose() { disposed = true; StopProcess(); }
}
