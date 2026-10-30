using System.Windows.Controls;
using PulseWidget.Core;
using PulseWidget.Services;
using PulseWidget.Theming;
namespace PulseWidget;
public partial class WidgetStrip : UserControl
{
    public WidgetStrip() => InitializeComponent();
    public void Render(ProviderUsage openAi, ProviderUsage claude, Activity openAiActivity, Activity claudeActivity, SystemMonitor.Snapshot? system)
    {
        var now = DateTimeOffset.UtcNow;
        string Format(UsageWindow? w, ProviderUsage p) => (w?.Compact(now) ?? "—") + (w is not null && !w.IsExpired(now) && w.IsStale(now, p.UpdatedAt) ? "*" : "");
        string Remaining(UsageWindow? w) => w is null || w.IsExpired(now) ? "—" : $"{w.Remaining:0}%";
        CodexStatus.Text = "● " + openAiActivity.Label;
        ClaudeStatus.Text = "● " + (claudeActivity.State == WorkState.Unknown && claude.UpdatedAt is null ? "연결 대기" : claudeActivity.Label);
        ThemeManager.Bind(CodexStatus, TextBlock.ForegroundProperty, ThemeManager.StateRole(openAiActivity));
        ThemeManager.Bind(ClaudeStatus, TextBlock.ForegroundProperty, ThemeManager.StateRole(claudeActivity));
        CodexNumbers.Text = $"주 {Format(openAi.Weekly, openAi)} / 잔여 {Remaining(openAi.Weekly)}";
        ClaudeNumbers.Text = claude.Weekly is null && claude.FiveHour is null ? "사용량 미수신 · 클릭하여 연결 확인" : $"주 {Format(claude.Weekly, claude)} / 잔여 {Remaining(claude.Weekly)} · 5h {Format(claude.FiveHour, claude)}";
        UsageBar.Update(CodexWeekBar, openAi.Weekly, openAi.UpdatedAt, now, "Codex 주간");
        UsageBar.Update(ClaudeWeekBar, claude.Weekly, claude.UpdatedAt, now, "Claude 주간");
        UsageBar.Update(ClaudeFiveBar, claude.FiveHour, claude.UpdatedAt, now, "Claude 5시간");
        CpuNumber.Text = system?.Cpu is { } cpu ? $"CPU  {cpu:0}%" : "CPU  —";
        MemNumber.Text = system is null ? "MEM  —" : $"MEM  {system.Memory:0}%";
        Surface.ToolTip = $"OpenAI · {openAiActivity.Label}\n{openAi.Message}\nClaude · {claudeActivity.Label}\n{claude.Message}\n* 마지막 수신값 (10분 이상 경과)\n클릭: 상세 보기 · 우클릭: 위치와 설정";

    }
}
