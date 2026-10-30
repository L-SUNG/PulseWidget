using System.Windows.Automation;
using System.Windows.Controls;
using PulseWidget.Core;

namespace PulseWidget;

internal static class UsageBar
{
    public static bool Update(ProgressBar bar, UsageWindow? window, DateTimeOffset? received, DateTimeOffset now, string title)
    {
        bool valid = window is not null && double.IsFinite(window.Used) && !window.IsExpired(now);
        bool stale = valid && window!.IsStale(now, received);
        bar.Value = valid ? Math.Clamp(window!.Used, 0, 100) : 0;
        bar.Opacity = valid ? stale ? .65 : 1 : .3;
        string text = !valid ? window?.IsExpired(now) == true ? "초기화 시각 경과 · 새 데이터 대기" : "사용량 미수신"
            : $"사용 {window!.Used:0.#}% · 남음 {window.Remaining:0.#}%" + (stale ? " · 오래된 수신값 *" : "");
        bar.ToolTip = title + " · " + text;
        AutomationProperties.SetName(bar, title);
        AutomationProperties.SetHelpText(bar, text);
        return valid;
    }
}
