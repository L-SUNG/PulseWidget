using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using PulseWidget.Core;
using PulseWidget.Native;
using PulseWidget.Services;

namespace PulseWidget;
public partial class WidgetWindow : Window
{
    private readonly WidgetController controller;
    private IDisposable? taskbarEvents;
    private DetailWindow? detail;
    private IntPtr nativeHandle;
    private bool closing;
    private double appliedScale = double.NaN;
    public string DeviceName { get; }
    internal bool NativeAlive => nativeHandle == IntPtr.Zero || Taskbar.IsAlive(nativeHandle);
    public WidgetWindow(WidgetController controller, string deviceName)
    {
        this.controller = controller; DeviceName = deviceName;
        InitializeComponent();
        if (Environment.GetCommandLineArgs().Contains("--inspect-ui")) ShowInTaskbar = true;
        Strip.MouseLeftButtonUp += (_, _) => ToggleDetail();
        Strip.ContextMenu = controller.CreateMenu(this);
        SourceInitialized += (_, _) =>
        {
            nativeHandle = new WindowInteropHelper(this).Handle;
            Taskbar.Setup(this);
            taskbarEvents = Taskbar.Watch(this, ApplyPosition, DeviceName);
            ApplyPosition();
        };
        Closed += (_, _) =>
        {
            taskbarEvents?.Dispose(); detail?.Close(); Taskbar.ForgetHost(nativeHandle);
            if (!closing) controller.RestartForRecovery("taskbar-exit");
        };
    }
    public void ApplyPosition()
    {
        if (closing) return;
        var settings = controller.Settings;
        if (appliedScale != settings.Scale)
        { appliedScale = settings.Scale; Width = 546 * settings.Scale; Height = 42 * settings.Scale; }
        if (Strip.LayoutTransform is not ScaleTransform transform || transform.ScaleX != settings.Scale || transform.ScaleY != settings.Scale)
            Strip.LayoutTransform = new ScaleTransform(settings.Scale, settings.Scale);
        if (nativeHandle != IntPtr.Zero) Taskbar.Place(this, settings, DeviceName);
    }
    public void CloseForUpdate()
    {
        if (closing) return;
        closing = true; taskbarEvents?.Dispose(); detail?.Close();
        // Detach a live HWND before closing; never alter another process's HWND.
        if (nativeHandle != IntPtr.Zero && Taskbar.IsAlive(nativeHandle)) Taskbar.SetHost(nativeHandle, IntPtr.Zero);
        Close();
    }
    public void Render(ProviderUsage open, ProviderUsage claude, Activity oa, Activity ca, SystemMonitor.Snapshot? pc)
    {
        Strip.Render(open, claude, oa, ca, pc); detail?.Update(open, claude, oa, ca, pc);
    }
    public void ThemeChanged() => detail?.SelectTheme(controller.Settings.ThemeId);
    public void PositionChanged() => detail?.SetPosition(controller.Settings.PositionPercent ?? HorizontalPosition.DefaultPercent);
    public void ToggleDetail()
    {
        if (detail?.IsVisible == true) { detail.Hide(); return; }
        if (detail is null)
        {
            detail = new DetailWindow(controller.Settings, controller.SaveSettings, controller.InstallClaude, controller.Refresh, controller.SetTheme,
                () => controller.EditColors(this), controller.CreateDisplayMenu, DeviceName) { Owner = this };
            if (Environment.GetCommandLineArgs().Contains("--inspect-ui")) detail.ShowInTaskbar = true;
            detail.Closed += (_, _) => detail = null;
        }
        detail.Show(); Taskbar.PositionPopup(detail, this, DeviceName); detail.Activate();
        controller.Refresh();
    }
}
