using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PulseWidget.Core;
using PulseWidget.Services;

namespace PulseWidget;

public sealed class DetailWindow : Window
{
    private readonly DetailView view;
    public DetailWindow(Settings settings, Action save, Action connect, Action refresh, Action<string> changeTheme,
        Action? editColors = null, Func<ContextMenu>? displayMenu = null, string? deviceName = null)
    {
        Title = "Pulse · 사용량 상세"; Width = 402; Height = 740; WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; AllowsTransparency = true;
        Background = Brushes.Transparent;
        Content = view = new DetailView(settings, save, connect, refresh, changeTheme, editColors, displayMenu, Hide);
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Hide(); };
        Deactivated += (_, _) => Hide();
    }
    public void SelectTheme(string id) => view.SelectTheme(id);
    public void SetPosition(double percent) => view.SetPosition(percent);
    public void Update(ProviderUsage open, ProviderUsage claude, Activity oa, Activity ca, SystemMonitor.Snapshot? pc) => view.Update(open, claude, oa, ca, pc);
}
